using LiteNetLib;

namespace GameServer;

/// <summary>
/// 房间管理器（服务器侧）：房间字典与连接归属的统一入口，
/// 匹配/消息处理等逻辑复用此入口，避免直接操作字典
/// </summary>
public sealed class RoomMgr
{
    /// <summary>房间号 -> 房间</summary>
    public Dictionary<int, Room> Rooms { get; } = new();

    /// <summary>连接 -> 所在房间</summary>
    public Dictionary<NetPeer, Room> PeerRooms { get; } = new();

    private readonly ProfileStore _store;
    private int _nextRoomId;

    public RoomMgr(ProfileStore store) => _store = store;

    /// <summary>创建房间（不加入成员）</summary>
    public Room CreateRoom()
    {
        var room = new Room(++_nextRoomId, _store);
        Rooms[room.Id] = room;
        return room;
    }

    /// <summary>加入房间（未校验前置条件，调用方保证已认证/不在房/房间未满）</summary>
    public void JoinRoom(Room room, NetPeer peer, int playerId)
    {
        room.Join(peer, playerId);
        PeerRooms[peer] = room;
    }

    /// <summary>连接是否已在房间</summary>
    public bool IsInRoom(NetPeer peer) => PeerRooms.ContainsKey(peer);

    /// <summary>
    /// 从房间移除连接（主动退出/断线/顶号共用）：推送剩余成员，房间空则销毁。
    /// 返回所在房间（供调用方打日志），未在房间时返回 null
    /// </summary>
    public Room? RemoveFromRoom(NetPeer peer)
    {
        if (!PeerRooms.TryGetValue(peer, out var room)) return null;

        room.Remove(peer);
        PeerRooms.Remove(peer);
        Console.WriteLine($"[房间{room.Id}] 移除: 剩余 {room.MemberCount} 人");

        if (room.MemberCount == 0)
        {
            Rooms.Remove(room.Id);
            Console.WriteLine($"[房间{room.Id}] 已空，销毁");
            return room;
        }

        room.PushState();
        return room;
    }

    /// <summary>
    /// 帧广播：遍历所有房间，各房间独立帧号打包自己的攒批操作发成员
    /// （空帧也发，保证帧号连续；房间空了就地销毁）
    /// </summary>
    public void BroadcastAllFrames()
    {
        // 遍历中销毁：先收集要移除的房间号
        List<int>? emptyRooms = null;
        foreach (var kv in Rooms)
        {
            if (kv.Value.BroadcastFrame())
            {
                emptyRooms ??= new List<int>();
                emptyRooms.Add(kv.Key);
            }
        }

        if (emptyRooms != null)
        {
            foreach (var id in emptyRooms)
                Rooms.Remove(id);
        }
    }
}
