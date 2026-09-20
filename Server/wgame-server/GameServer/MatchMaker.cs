using LiteNetLib;

namespace GameServer;

/// <summary>
/// 匹配器（阶段 6 简化版：两人即撮合，无段位/评分）：
/// - 队列只收已认证且不在房间的连接
/// - 后入队者触发撮合：队列满 2 人时创建房间，把两人放入，各自回 MatchResp{MATCHED}
/// - 取消匹配/断线自动出队
/// 线程说明：与网络事件同在主循环线程（PollEvents 串行调用），无并发问题
/// </summary>
public sealed class MatchMaker
{
    private readonly NetServer _net;
    private readonly SessionMgr _sessions;
    private readonly RoomMgr _rooms;

    /// <summary>匹配队列（入队顺序）</summary>
    private readonly Queue<NetPeer> _queue = new();

    public MatchMaker(NetServer net, SessionMgr sessions, RoomMgr rooms)
    {
        _net = net;
        _sessions = sessions;
        _rooms = rooms;
    }

    /// <summary>出队并通知（取消/断线/进房时调用），静默处理不在队列的情况</summary>
    public void Dequeue(NetPeer peer)
    {
        if (!_queue.Contains(peer)) return;

        // Queue 无 Remove，重建（队列极小，代价可忽略）
        var rebuilt = new Queue<NetPeer>();
        while (_queue.Count > 0)
        {
            var p = _queue.Dequeue();
            if (p != peer) rebuilt.Enqueue(p);
        }
        while (rebuilt.Count > 0)
            _queue.Enqueue(rebuilt.Dequeue());
    }

    /// <summary>
    /// 开始匹配：入队并尝试撮合（队列满 2 人时立即撮合）
    /// </summary>
    public void Enqueue(NetPeer peer, int playerId)
    {
        _queue.Enqueue(peer);
        Console.WriteLine($"[匹配] playerId={playerId} 入队, 队列 {_queue.Count} 人");

        if (_queue.Count >= 2)
            TryMatchmake();
    }

    /// <summary>撮合：取队首两人建房间</summary>
    private void TryMatchmake()
    {
        while (_queue.Count >= 2)
        {
            var a = _queue.Dequeue();
            var b = _queue.Dequeue();

            // 连接可能已断（断线事件清队兜底，此处再防一手：ConnectionState 网络层状态判断）
            var aAlive = a.ConnectionState == ConnectionState.Connected;
            var bAlive = b.ConnectionState == ConnectionState.Connected;
            if (!aAlive || !bAlive)
            {
                if (aAlive) _queue.Enqueue(a);
                if (bAlive) _queue.Enqueue(b);
                continue;
            }

            var room = _rooms.CreateRoom();
            _rooms.JoinRoom(room, a, _sessions.GetPlayerId(a));
            _rooms.JoinRoom(room, b, _sessions.GetPlayerId(b));

            Console.WriteLine($"[匹配] 撮合成功 -> 房间{room.Id}");

            // 各自回撮合成功（成员推送由房间推送统一发）
            _net.SendTo(a, NetMsg.MsgType.MsgMatchResp, new NetMsg.MatchResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNone,
                MatchState = NetMsg.MatchState.Matched,
                RoomId = room.Id,
            });
            _net.SendTo(b, NetMsg.MsgType.MsgMatchResp, new NetMsg.MatchResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNone,
                MatchState = NetMsg.MatchState.Matched,
                RoomId = room.Id,
            });

            room.PushState();
        }
    }
}
