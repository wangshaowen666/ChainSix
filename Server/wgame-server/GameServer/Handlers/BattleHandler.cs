using LiteNetLib;

namespace GameServer;

/// <summary>
/// 战斗消息处理：PlayerInput（操作攒批）/ GameEndReq（结束上报）
/// </summary>
public sealed class BattleHandler
{
    private const int MaxOpType = 2;         // 合法操作类型上限（1=放置塔 2=升级塔，0 已废弃）
    private const int MaxInputsPerFrame = 8; // 每玩家每帧最多操作数（防刷）
    private const int MaxCoordX = 7;         // 格子 x 上限（与客户端 BattleLogic.MapW-1 一致）
    private const int MaxCoordY = 15;        // 格子 y 上限（与客户端 BattleLogic.MapH-1 一致）

    private readonly NetServer _net;
    private readonly SessionMgr _sessions;
    private readonly RoomMgr _rooms;

    public BattleHandler(MsgDispatcher dispatcher, NetServer net, SessionMgr sessions, RoomMgr rooms)
    {
        _net = net;
        _sessions = sessions;
        _rooms = rooms;

        dispatcher.Register(NetMsg.MsgType.MsgPlayerInput, NetMsg.PlayerInput.Parser, OnPlayerInput);
        dispatcher.Register(NetMsg.MsgType.MsgGameEndReq, NetMsg.GameEndReq.Parser, OnGameEndReq);
    }

    /// <summary>
    /// 收到玩家操作：校验（已认证 + 在房间内 + 操作合法）后攒进所在房间
    /// </summary>
    private void OnPlayerInput(NetPeer peer, NetMsg.PlayerInput input)
    {
        // 校验 0：必须先通过 UDP 认证（未认证连接的操作一律丢弃）
        if (!_sessions.TryGetPlayerId(peer, out var playerId))
        {
            Console.WriteLine($"[校验失败] 未认证连接 ({peer.Address}:{peer.Port}) 的操作，丢弃");
            return;
        }

        // 校验 1：必须先进入房间（未进房的操作无广播对象，丢弃）
        if (!_rooms.PeerRooms.TryGetValue(peer, out var room))
        {
            Console.WriteLine($"[校验失败] playerId={playerId} 不在房间内，操作丢弃");
            return;
        }

        // 校验 2：操作类型合法（非法 op_type 直接丢弃，防协议滥用）
        if (input.OpType < 0 || input.OpType > MaxOpType)
        {
            Console.WriteLine($"[校验失败] playerId={playerId} 非法 op_type={input.OpType}，丢弃");
            return;
        }

        // 校验 2.5：房间未开局不收操作（等待期伪造操作无意义，直接丢弃；逻辑层对坐标还有二次校验）
        if (!room.IsStarted)
        {
            Console.WriteLine($"[校验失败] playerId={playerId} 房间未开局，操作丢弃");
            return;
        }

        // 校验 2.6：操作参数范围（格子坐标越界直接丢弃，挡协议滥用/异常值）
        if (input.Param1 < 0 || input.Param1 > MaxCoordX || input.Param2 < 0 || input.Param2 > MaxCoordY)
        {
            Console.WriteLine($"[校验失败] playerId={playerId} 坐标越界 ({input.Param1},{input.Param2})，丢弃");
            return;
        }

        // 校验 3：单帧操作数防刷（正常操作远达不到该频率，超限视为刷包）
        if (room.CountInputsOf(playerId) >= MaxInputsPerFrame)
        {
            Console.WriteLine($"[校验失败] playerId={playerId} 单帧操作数超过 {MaxInputsPerFrame}，丢弃");
            return;
        }

        // player_id 由服务器填充（token 解析的真实 playerId），客户端无法伪造身份
        room.AddInput(new NetMsg.PlayerInput
        {
            OpType = input.OpType,
            Param1 = input.Param1,
            Param2 = input.Param2,
            PlayerId = playerId,
        });
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}][玩家操作] 房间{room.Id} playerId={playerId}, op_type={input.OpType}, param1={input.Param1}, param2={input.Param2}");
    }

    /// <summary>
    /// 游戏结束上报：客户端本地模拟判定结束后上报（双端同帧判定都会上报，Room 以第一份为准）
    /// </summary>
    private void OnGameEndReq(NetPeer peer, NetMsg.GameEndReq req)
    {
        if (!_rooms.PeerRooms.TryGetValue(peer, out var room))
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgGameEndResp, new NetMsg.GameEndResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNotInRoom,
            });
            return;
        }

        var playerId = _sessions.GetPlayerId(peer);
        var first = room.GameEnd(req.EndFrame);
        Console.WriteLine($"[房间{room.Id}] playerId={playerId} 上报游戏结束 end_frame={req.EndFrame}"
                          + (first ? "" : " (重复上报, 幂等忽略)"));

        // 无论是否首次，回执都成功（重复上报是双端同帧判定的正常竞争，不是错误）
        _net.SendTo(peer, NetMsg.MsgType.MsgGameEndResp, new NetMsg.GameEndResp
        {
            ErrorCode = NetMsg.ErrorCode.ErrorNone,
        });
    }
}
