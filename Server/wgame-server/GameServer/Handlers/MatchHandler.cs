using LiteNetLib;

namespace GameServer;

/// <summary>
/// 匹配消息处理：MatchReq（开始匹配入队 / 取消匹配出队），撮合由 MatchMaker 完成
/// </summary>
public sealed class MatchHandler
{
    private readonly NetServer _net;
    private readonly SessionMgr _sessions;
    private readonly RoomMgr _rooms;
    private readonly MatchMaker _matchMaker;

    public MatchHandler(MsgDispatcher dispatcher, NetServer net, SessionMgr sessions,
        RoomMgr rooms, MatchMaker matchMaker)
    {
        _net = net;
        _sessions = sessions;
        _rooms = rooms;
        _matchMaker = matchMaker;

        dispatcher.Register(NetMsg.MsgType.MsgMatchReq, NetMsg.MatchReq.Parser, OnMatchReq);
    }

    /// <summary>
    /// 匹配请求：开始匹配（入队，队列满 2 人时撮合）/ 取消匹配（出队）
    /// </summary>
    private void OnMatchReq(NetPeer peer, NetMsg.MatchReq req)
    {
        // 前置校验：已认证
        if (!_sessions.TryGetPlayerId(peer, out var playerId))
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgMatchResp, new NetMsg.MatchResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorUnauthorized,
            });
            return;
        }

        if (req.Cancel)
        {
            // 取消匹配
            _matchMaker.Dequeue(peer);
            Console.WriteLine($"[匹配] playerId={playerId} 取消匹配");
            _net.SendTo(peer, NetMsg.MsgType.MsgMatchResp, new NetMsg.MatchResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNone,
                MatchState = NetMsg.MatchState.Cancelled,
            });
            return;
        }

        // 前置校验：不在房间（匹配的目的就是进房，已进房则拒绝）
        if (_rooms.IsInRoom(peer))
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgMatchResp, new NetMsg.MatchResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorAlreadyInRoom,
            });
            return;
        }

        // 入队并尝试撮合（撮合成功时 MatchMaker 会直接回 MATCHED 的 MatchResp）
        _matchMaker.Enqueue(peer, playerId);
        // 受理回执（若已撮合，客户端会先收到 MATCHED，此回执只是补充）
        _net.SendTo(peer, NetMsg.MsgType.MsgMatchResp, new NetMsg.MatchResp
        {
            ErrorCode = NetMsg.ErrorCode.ErrorNone,
            MatchState = NetMsg.MatchState.Matching,
        });
    }
}
