using Google.Protobuf;
using LiteNetLib;

namespace GameServer;

/// <summary>
/// 帧1 房间：帧同步的隔离单位。
/// - 每个房间独立的帧号（从 1 开始），广播帧只发本房间成员
/// - 房间内成员变化（加入/退出/断线）时推送 RoomStatePush
/// - 单人即可有效（匹配逻辑阶段 6 后续实现，先手动建房/加入凑人）
/// </summary>
public class Room
{
    public const int MaxPlayers = 2;

    /// <summary>房间号（递增分配，全局唯一）</summary>
    public int Id { get; }

    /// <summary>房间成员（peer -> playerId），插入顺序即加入顺序</summary>
    private readonly Dictionary<NetPeer, int> _members = new();

    /// <summary>房间内已累积的待广播操作</summary>
    private readonly List<NetMsg.PlayerInput> _pendingInputs = new();

    /// <summary>房间独立帧号（每广播一帧 +1）</summary>
    public int FrameId { get; private set; }

    /// <summary>房间内已就绪成员（peer）</summary>
    private readonly HashSet<NetPeer> _readyPeers = new();

    /// <summary>是否已开局（游戏中；结束后回 false，可再次就绪重开）</summary>
    public bool IsStarted { get; private set; }

    /// <summary>战斗起始帧号（开局时确定，双端一致锚定）</summary>
    public int StartFrame { get; private set; }

    /// <summary>战斗随机种子（开局时确定）</summary>
    public long Seed { get; private set; }

    /// <summary>结算持久化（入账给成员）</summary>
    private readonly ProfileStore _store;

    public Room(int id, ProfileStore store)
    {
        Id = id;
        _store = store;
    }

    public int MemberCount => _members.Count;

    public bool IsFull => _members.Count >= MaxPlayers;

    public bool Contains(NetPeer peer) => _members.ContainsKey(peer);

    /// <summary>成员 playerId 列表（按加入顺序）</summary>
    public List<int> MemberIds()
    {
        var ids = new List<int>(_members.Count);
        foreach (var kv in _members)
            ids.Add(kv.Value);
        return ids;
    }

    /// <summary>加入房间（需已认证、房间未满）</summary>
    public bool Join(NetPeer peer, int playerId)
    {
        if (IsFull || _members.ContainsKey(peer)) return false;
        _members[peer] = playerId;
        return true;
    }

    /// <summary>移除成员（退出/断线）。房间清空时由调用方决定销毁</summary>
    public bool Remove(NetPeer peer)
    {
        _readyPeers.Remove(peer);
        return _members.Remove(peer);
    }

    /// <summary>标记就绪。返回 true 表示触发了全员就绪开战（推送由本方法发出）</summary>
    public bool SetReady(NetPeer peer)
    {
        if (IsStarted || !_members.ContainsKey(peer)) return false;
        _readyPeers.Add(peer);

        // 开战条件 = 就绪人数达到房间容量（2 人），而不是"当前成员数"：
        // 单人建房后就绪不能开局，必须等另一人加入并一起就绪
        if (_readyPeers.Count < MaxPlayers) return false;

        // 全员就绪：确定起始帧（当前帧 + 3 秒缓冲给客户端加载）与随机种子，广播开战
        IsStarted = true;
        StartFrame = FrameId + 60;
        Seed = DateTime.UtcNow.Ticks;

        // 参战玩家列表（升序，双端按同一顺序初始化逻辑层）
        var playerIds = MemberIds();
        playerIds.Sort();

        var push = new NetMsg.StartGamePush { StartFrame = StartFrame, Seed = Seed };
        push.PlayerIds.Add(playerIds);
        var envelope = new NetMsg.NetMsgEnvelope
        {
            MsgType = NetMsg.MsgType.MsgStartGamePush,
            Payload = push.ToByteString(),
        };
        var bytes = envelope.ToByteArray();
        foreach (var kv in _members)
            kv.Key.Send(bytes, DeliveryMethod.ReliableOrdered);

        Console.WriteLine($"[房间{Id}] 全员就绪开战! start_frame={StartFrame}, seed={Seed}");
        return true;
    }

    /// <summary>攒操作（校验由 GameServer 统一做）</summary>
    public void AddInput(NetMsg.PlayerInput input) => _pendingInputs.Add(input);

    /// <summary>
    /// 游戏结束（客户端上报触发，双端同帧判定都会上报，以第一份为准）：
    /// 校验结束帧 + 服务器计算奖励 -> 广播 GameEndPush 给全员 -> 房间重置回等待中
    /// （成员保留，可再次就绪重开）。
    /// 返回 true=首次上报（已广播）；false=未开局（重复上报，幂等忽略）。
    /// 阶段 7-2 在此扩展：奖励持久化到数据库
    /// </summary>
    public bool GameEnd(int endFrame)
    {
        if (!IsStarted) return false;

        // 结算：end_frame 校验（越界=作弊嫌疑，奖励 0 不入账）+ 奖励服务器推导（上报无决定力）
        var valid = Settlement.ValidateEndFrame(endFrame, StartFrame, FrameId, out var clamped);
        var reward = valid ? Settlement.CalcReward(StartFrame, clamped) : 0;

        // 结算入账（仅合法结算；首次上报一次，重复上报已在上面 return false 挡掉不会重复加钱）
        if (valid)
        {
            // 成员快照：入账名单在重置前收集，断线者同样到账
            _store.AddSettlement(MemberIds(), reward);
        }

        IsStarted = false;
        _readyPeers.Clear();
        _pendingInputs.Clear();
        StartFrame = 0;
        Seed = 0;

        var push = new NetMsg.GameEndPush
        {
            EndFrame = clamped,
            Result = NetMsg.GameResult.Defeat,
            GoldReward = reward,
        };
        var envelope = new NetMsg.NetMsgEnvelope
        {
            MsgType = NetMsg.MsgType.MsgGameEndPush,
            Payload = push.ToByteString(),
        };
        var bytes = envelope.ToByteArray();
        foreach (var kv in _members)
            kv.Key.Send(bytes, DeliveryMethod.ReliableOrdered);

        Console.WriteLine($"[房间{Id}] 游戏结束! end_frame={clamped}(上报{endFrame}), 奖励={reward}金币, 房间回到等待中(可再就绪重开)");
        return true;
    }

    /// <summary>单玩家本帧已攒操作数（防刷校验用）</summary>
    public int CountInputsOf(int playerId)
    {
        var count = 0;
        foreach (var i in _pendingInputs)
            if (i.PlayerId == playerId) count++;
        return count;
    }

    /// <summary>
    /// 广播一帧：打包攒批操作发本房间成员（空帧也发，帧号连续）。
    /// 返回是否房间已空（调用方据此销毁房间）
    /// </summary>
    public bool BroadcastFrame()
    {
        FrameId++;

        var frame = new NetMsg.FrameData { FrameId = FrameId };
        frame.Inputs.Add(_pendingInputs);
        _pendingInputs.Clear();

        var envelope = new NetMsg.NetMsgEnvelope
        {
            MsgType = NetMsg.MsgType.MsgFrameData,
            Payload = frame.ToByteString(),
        };
        var bytes = envelope.ToByteArray();
        foreach (var kv in _members)
            kv.Key.Send(bytes, DeliveryMethod.ReliableOrdered);

        return _members.Count == 0;
    }

    /// <summary>推送房间状态（成员变化时）</summary>
    public void PushState()
    {
        var push = new NetMsg.RoomStatePush { RoomId = Id };
        // 成员列表排序后推送，双端展示一致
        var ids = MemberIds();
        ids.Sort();
        push.PlayerIds.Add(ids);

        var envelope = new NetMsg.NetMsgEnvelope
        {
            MsgType = NetMsg.MsgType.MsgRoomStatePush,
            Payload = push.ToByteString(),
        };
        var bytes = envelope.ToByteArray();
        foreach (var kv in _members)
            kv.Key.Send(bytes, DeliveryMethod.ReliableOrdered);

        Console.WriteLine($"[房间{Id}] 推送状态: [{string.Join(",", ids)}] 就绪 {_readyPeers.Count}/{_members.Count}");
    }
}
