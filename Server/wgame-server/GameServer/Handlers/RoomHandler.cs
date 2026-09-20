using LiteNetLib;

namespace GameServer;

/// <summary>
/// 房间消息处理：RoomReq（建房/加房）/ LeaveRoomReq（退房）/ ReadyReq（就绪开战）
/// </summary>
public sealed class RoomHandler
{
    private readonly NetServer _net;
    private readonly SessionMgr _sessions;
    private readonly RoomMgr _rooms;

    public RoomHandler(MsgDispatcher dispatcher, NetServer net, SessionMgr sessions, RoomMgr rooms)
    {
        _net = net;
        _sessions = sessions;
        _rooms = rooms;

        dispatcher.Register(NetMsg.MsgType.MsgRoomReq, NetMsg.RoomReq.Parser, OnRoomReq);
        dispatcher.Register(NetMsg.MsgType.MsgLeaveRoomReq, NetMsg.LeaveRoomReq.Parser, (peer, _) => OnLeaveRoom(peer));
        dispatcher.Register(NetMsg.MsgType.MsgReadyReq, NetMsg.ReadyReq.Parser, (peer, _) => OnReadyReq(peer));
    }

    /// <summary>
    /// 房间请求：room_id=0 创建新房间；room_id>0 加入指定房间。
    /// 前置条件：已通过 UDP 认证且当前不在房间中
    /// </summary>
    private void OnRoomReq(NetPeer peer, NetMsg.RoomReq req)
    {
        // 前置校验：已认证
        if (!_sessions.TryGetPlayerId(peer, out var playerId))
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgRoomResp, new NetMsg.RoomResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorUnauthorized,
            });
            return;
        }

        // 前置校验：未在其他房间
        if (_rooms.IsInRoom(peer))
        {
            Console.WriteLine($"[校验失败] playerId={playerId} 已在房间，拒绝建房/加房");
            _net.SendTo(peer, NetMsg.MsgType.MsgRoomResp, new NetMsg.RoomResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorAlreadyInRoom,
            });
            return;
        }

        if (req.RoomId == 0)
        {
            // 创建新房间
            var room = _rooms.CreateRoom();
            _rooms.JoinRoom(room, peer, playerId);
            Console.WriteLine($"[房间{room.Id}] 创建并加入: playerId={playerId}");
            _net.SendTo(peer, NetMsg.MsgType.MsgRoomResp, new NetMsg.RoomResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNone,
                RoomId = room.Id,
            });
            room.PushState();
        }
        else
        {
            // 加入指定房间
            if (!_rooms.Rooms.TryGetValue(req.RoomId, out var room))
            {
                _net.SendTo(peer, NetMsg.MsgType.MsgRoomResp, new NetMsg.RoomResp
                {
                    ErrorCode = NetMsg.ErrorCode.ErrorRoomNotFound,
                });
                return;
            }

            if (room.IsFull)
            {
                _net.SendTo(peer, NetMsg.MsgType.MsgRoomResp, new NetMsg.RoomResp
                {
                    ErrorCode = NetMsg.ErrorCode.ErrorRoomFull,
                });
                return;
            }

            _rooms.JoinRoom(room, peer, playerId);
            Console.WriteLine($"[房间{room.Id}] 加入: playerId={playerId}, 当前 {room.MemberCount} 人");
            _net.SendTo(peer, NetMsg.MsgType.MsgRoomResp, new NetMsg.RoomResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNone,
                RoomId = room.Id,
            });
            room.PushState();
        }
    }

    /// <summary>
    /// 退出房间：成员变化推送剩余成员；房间清空则销毁
    /// </summary>
    private void OnLeaveRoom(NetPeer peer)
    {
        if (_rooms.RemoveFromRoom(peer) == null)
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgLeaveRoomResp, new NetMsg.LeaveRoomResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNotInRoom,
            });
            return;
        }

        _net.SendTo(peer, NetMsg.MsgType.MsgLeaveRoomResp, new NetMsg.LeaveRoomResp
        {
            ErrorCode = NetMsg.ErrorCode.ErrorNone,
        });
    }

    /// <summary>
    /// 就绪请求：在房间内且未开局则标记就绪；全员就绪由 Room.SetReady 触发开战推送
    /// </summary>
    private void OnReadyReq(NetPeer peer)
    {
        if (!_rooms.PeerRooms.TryGetValue(peer, out var room))
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgReadyResp, new NetMsg.ReadyResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorNotInRoom,
            });
            return;
        }

        if (room.IsStarted)
        {
            _net.SendTo(peer, NetMsg.MsgType.MsgReadyResp, new NetMsg.ReadyResp
            {
                ErrorCode = NetMsg.ErrorCode.ErrorAlreadyInRoom,
            });
            return;
        }

        var playerId = _sessions.GetPlayerId(peer);
        Console.WriteLine($"[房间{room.Id}] playerId={playerId} 就绪");
        room.SetReady(peer);

        _net.SendTo(peer, NetMsg.MsgType.MsgReadyResp, new NetMsg.ReadyResp
        {
            ErrorCode = NetMsg.ErrorCode.ErrorNone,
        });
    }
}
