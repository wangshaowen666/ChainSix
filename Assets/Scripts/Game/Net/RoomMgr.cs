/*--------------------------------------------------------------
 * File: RoomMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/19 18:30:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

/// <summary>
/// 房间管理器（热更）：
/// - 建房/加入/退出（服务器校验并维护成员，本类只管请求与状态镜像）
/// - 订阅 NetMsgHandler 的房间事件，维护 CurRoomId/MemberIds 本地镜像
/// - 断线清状态（重连后需重新建房/加入）
/// 阶段 6 后续：匹配逻辑（两人凑齐自动开房）在此扩展
/// </summary>
public class RoomMgr : ManagerBase
{
    /// <summary>当前所在房间号（0 = 不在房间）</summary>
    public int CurRoomId { get; private set; }

    /// <summary>当前房间成员 playerId 列表（服务器推送镜像，不在房间时为空）</summary>
    public List<int> MemberIds { get; } = new();

    /// <summary>是否在房间内</summary>
    public bool IsInRoom => CurRoomId != 0;

    /// <summary>是否在匹配队列中（撮合成功后自动置 false）</summary>
    public bool IsMatching { get; private set; }

    /// <summary>本端是否已就绪（开战后置 false）</summary>
    public bool IsReady { get; private set; }

    /// <summary>战斗起始帧号与随机种子（收到开战推送时填充，未开局为 0）</summary>
    public int StartFrame { get; private set; }
    public long Seed { get; private set; }

    /// <summary>参战玩家列表（开战推送下发，playerId 升序；BattleLogic 按此初始化）</summary>
    public List<int> BattlePlayerIds { get; } = new();

    /// <summary>是否处于战斗结束态（收到 GameEndPush 置 true，重新开局/退房/断线清 false）</summary>
    public bool IsBattleOver { get; private set; }

    /// <summary>建房/加房结果（成功失败都触发，看 resp.ErrorCode）</summary>
    public event Action<NetMsg.RoomResp> OnRoomResp;

    /// <summary>退出房间结果</summary>
    public event Action<NetMsg.LeaveRoomResp> OnLeaveRoomResp;

    /// <summary>房间成员变化（服务器推送，含自己加入/他人加入/他人退出）</summary>
    public event Action<NetMsg.RoomStatePush> OnRoomStateChanged;

    public override void OnInit()
    {
        GameMgr.NetMsg.OnRoomResp += HandleRoomResp;
        GameMgr.NetMsg.OnRoomStatePush += HandleRoomStatePush;
        GameMgr.NetMsg.OnLeaveRoomResp += HandleLeaveRoomResp;
        GameMgr.NetMsg.OnMatchResp += HandleMatchResp;
        GameMgr.NetMsg.OnReadyResp += HandleReadyResp;
        GameMgr.NetMsg.OnStartGamePush += HandleStartGamePush;
        GameMgr.NetMsg.OnGameEndPush += HandleGameEndPush;
        //CoreMgr.Net.OnDisconnected += OnDisconnected;
    }

    /// <summary>就绪（房间内发送；全员就绪时服务器广播 StartGamePush）</summary>
    public void Ready()
    {
        if (!IsInRoom)
        {
            Log.Warning("未在房间内，无法就绪");
            return;
        }
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgReadyReq, new NetMsg.ReadyReq());
    }

    /// <summary>上报游戏结束（TdView 本地模拟判定 GameOver 后调用；
    /// 双端同帧判定都会上报，服务器以第一份为准广播 GameEndPush）</summary>
    public void ReportGameEnd(int endFrame)
    {
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgGameEndReq, new NetMsg.GameEndReq { EndFrame = endFrame });
    }

    /// <summary>开始匹配（进队列，两人凑齐自动开房）</summary>
    public void StartMatch()
    {
        if (IsInRoom)
        {
            Log.Warning("已在房间内，无需匹配");
            return;
        }
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgMatchReq, new NetMsg.MatchReq { Cancel = false });
    }

    /// <summary>取消匹配</summary>
    public void CancelMatch()
    {
        if (!IsMatching) return;
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgMatchReq, new NetMsg.MatchReq { Cancel = true });
    }

    /// <summary>创建新房间（成功后 CurRoomId 为新房间号）</summary>
    public void CreateRoom()
    {
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgRoomReq, new NetMsg.RoomReq { RoomId = 0 });
    }

    /// <summary>加入指定房间</summary>
    public void JoinRoom(int roomId)
    {
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgRoomReq, new NetMsg.RoomReq { RoomId = roomId });
    }

    /// <summary>退出当前房间</summary>
    public void LeaveRoom()
    {
        if (!IsInRoom) return;
        GameMgr.NetMsg.Send(NetMsg.MsgType.MsgLeaveRoomReq, new NetMsg.LeaveRoomReq());
    }

    /// <summary>建房/加房结果：成功则记录房间号</summary>
    private void HandleRoomResp(NetMsg.RoomResp resp)
    {
        if (resp.ErrorCode == NetMsg.ErrorCode.ErrorNone)
        {
            CurRoomId = resp.RoomId;
            Log.Info("房间操作成功, 房间号:", resp.RoomId);
        }
        else
        {
            Log.Error("房间操作失败, 错误码:", resp.ErrorCode);
        }
        OnRoomResp?.Invoke(resp);
    }

    /// <summary>房间状态推送：更新成员镜像</summary>
    private void HandleRoomStatePush(NetMsg.RoomStatePush push)
    {
        CurRoomId = push.RoomId;
        MemberIds.Clear();
        MemberIds.AddRange(push.PlayerIds);
        Log.Info("[房间", push.RoomId, "] 成员:", string.Join(",", push.PlayerIds));
        OnRoomStateChanged?.Invoke(push);
    }

    /// <summary>退出房间结果：成功则清状态</summary>
    private void HandleLeaveRoomResp(NetMsg.LeaveRoomResp resp)
    {
        if (resp.ErrorCode == NetMsg.ErrorCode.ErrorNone)
        {
            CurRoomId = 0;
            MemberIds.Clear();
            IsReady = false;
            IsBattleOver = false;
            StartFrame = 0;
            Seed = 0;
            BattlePlayerIds.Clear();
            Log.Info("已退出房间");
        }
        else
        {
            Log.Error("退出房间失败, 错误码:", resp.ErrorCode);
        }
        OnLeaveRoomResp?.Invoke(resp);
    }

    /// <summary>
    /// 匹配回复（按 match_state 区分状态）：
    /// MATCHING=开始匹配受理（进入匹配态）；MATCHED=撮合成功（进房）；CANCELLED=取消成功
    /// </summary>
    private void HandleMatchResp(NetMsg.MatchResp resp)
    {
        if (resp.ErrorCode != NetMsg.ErrorCode.ErrorNone)
        {
            IsMatching = false;
            Log.Error("匹配失败, 错误码:", resp.ErrorCode);
        }
        else
        {
            switch (resp.MatchState)
            {
                case NetMsg.MatchState.Matched:
                    IsMatching = false;
                    CurRoomId = resp.RoomId;
                    Log.Info("匹配成功! 房间号:", resp.RoomId);
                    break;
                case NetMsg.MatchState.Matching:
                    IsMatching = true;
                    Log.Info("匹配中...");
                    break;
                case NetMsg.MatchState.Cancelled:
                    IsMatching = false;
                    Log.Info("匹配已取消");
                    break;
                default:
                    IsMatching = false;
                    Log.Info("匹配状态:", resp.MatchState);
                    break;
            }
        }
        OnMatchResp?.Invoke(resp);
    }

    /// <summary>匹配状态变化（受理/取消/撮合成功）</summary>
    public event Action<NetMsg.MatchResp> OnMatchResp;

    /// <summary>就绪回复</summary>
    public event Action<NetMsg.ReadyResp> OnReadyResp;

    /// <summary>开战推送（全员就绪：起始帧+随机种子；此时客户端应进入战斗）</summary>
    public event Action<NetMsg.StartGamePush> OnStartGamePush;

    /// <summary>游戏结束推送（结束帧+结果；房间已回等待中，可再就绪重开）</summary>
    public event Action<NetMsg.GameEndPush> OnGameEndPush;

    /// <summary>就绪回执</summary>
    private void HandleReadyResp(NetMsg.ReadyResp resp)
    {
        if (resp.ErrorCode == NetMsg.ErrorCode.ErrorNone)
        {
            IsReady = true;
            Log.Info("已就绪, 等待其他玩家...");
        }
        else
        {
            Log.Error("就绪失败, 错误码:", resp.ErrorCode);
        }
        OnReadyResp?.Invoke(resp);
    }

    /// <summary>开战推送：记录起始帧与种子（进房对局锚点，TdView 用）</summary>
    private void HandleStartGamePush(NetMsg.StartGamePush push)
    {
        IsReady = false;
        IsBattleOver = false;
        StartFrame = push.StartFrame;
        Seed = push.Seed;
        BattlePlayerIds.Clear();
        BattlePlayerIds.AddRange(push.PlayerIds);
        Log.Info("开战! 起始帧:", push.StartFrame, "种子:", push.Seed, "参战:", string.Join(",", push.PlayerIds));
        OnStartGamePush?.Invoke(push);
    }

    /// <summary>游戏结束推送：房间已回等待中。
    /// 先触发事件再清锚点（订阅者回调内可读 StartFrame 计算对局时长/波次）</summary>
    private void HandleGameEndPush(NetMsg.GameEndPush push)
    {
        IsBattleOver = true;
        IsReady = false;
        Log.Info("游戏结束! 结束帧:", push.EndFrame, "结果:", push.Result);
        OnGameEndPush?.Invoke(push);

        StartFrame = 0;
        Seed = 0;
        BattlePlayerIds.Clear();
    }

    /// <summary>断线：清房间与匹配状态（重连后需重新匹配/建房/加入）</summary>
    private void OnDisconnected()
    {
        CurRoomId = 0;
        MemberIds.Clear();
        IsMatching = false;
        IsReady = false;
        IsBattleOver = false;
        StartFrame = 0;
        Seed = 0;
        BattlePlayerIds.Clear();
    }

    public override void OnSceneExit()
    {
        // 断开时服务器会自动把本连接移出房间并通知剩余成员
        // 本地镜像随断线事件清理，这里不重复处理
    }
}
