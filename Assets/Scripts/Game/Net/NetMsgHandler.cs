/*--------------------------------------------------------------
 * File: NetMsgHandler.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/15 10:00:00
 *--------------------------------------------------------------
 */

using System;
using Google.Protobuf;

/// <summary>
/// 网络协议处理层（热更）：
/// 订阅 Framework 的 NetMgr 传输层，负责协议消息的封装/解析/分发。
/// 连接建立后自动用本地登录态做 UDP 认证（UdpLoginReq/Ack，GameServer 校验 JWT 绑定 playerId）。
/// 新增协议类型：在对应 .proto 加 message + MsgType 枚举值，这里加 case 分支即可
/// </summary>
public class NetMsgHandler : ManagerBase
{
    /// <summary>UDP 认证是否通过（未认证连接在服务器侧的操作会被丢弃）</summary>
    public bool IsUdpAuthed { get; private set; }

    /// <summary>服务器认证返回的 playerId（与 HTTP 登录账号一致）</summary>
    public int UdpPlayerId { get; private set; }

    /// <summary>UDP 认证结果（成功/失败都触发，看 resp.ErrorCode 区分）</summary>
    public event Action<NetMsg.UdpLoginResp> OnUdpLoginResp;

    /// <summary>
    /// 收到服务器帧数据（FrameSyncMgr 订阅消费）
    /// </summary>
    public event Action<NetMsg.FrameData> OnFrameData;

    public override void OnInit()
    {
        // 订阅传输层的原始数据事件
        //CoreMgr.Net.OnRawData += OnRawDataReceived;

        // 连接建立后自动做 UDP 认证（用 HTTP 登录保存的 token）
        // CoreMgr.Net.OnConnected += OnNetConnected;
        // CoreMgr.Net.OnDisconnected += OnNetDisconnected;
    }

    /// <summary>
    /// 连接建立：发送本地 token 认证
    /// </summary>
    private void OnNetConnected()
    {
        IsUdpAuthed = false;
        UdpPlayerId = 0;

        if (!GameMgr.Account.IsLoggedIn)
        {
            Log.Warning("未登录（无本地 token），UDP 认证跳过，请先完成 HTTP 登录");
            return;
        }

        SendUdpLogin(GameMgr.Account.Token);
        Log.Info("已发送 UDP 认证请求");
    }

    /// <summary>
    /// 断开：清认证状态
    /// </summary>
    private void OnNetDisconnected()
    {
        IsUdpAuthed = false;
        UdpPlayerId = 0;
    }

    /// <summary>
    /// 发送 UDP 认证（token 来自 HTTP 登录，AccountMgr 本地保存）
    /// </summary>
    public void SendUdpLogin(string token)
    {
        Send(NetMsg.MsgType.MsgUdpLoginReq, new NetMsg.UdpLoginReq { Token = token });
    }

    /// <summary>
    /// 收到认证回复
    /// </summary>
    private void HandleUdpLoginResp(NetMsg.UdpLoginResp resp)
    {
        IsUdpAuthed = resp.ErrorCode == NetMsg.ErrorCode.ErrorNone;
        UdpPlayerId = resp.PlayerId;

        if (IsUdpAuthed)
            Log.Info("UDP 认证成功, playerId:", resp.PlayerId);
        else
            Log.Error("UDP 认证失败, 错误码:", resp.ErrorCode, "（token 无效或已过期，请重新 HTTP 登录）");

        OnUdpLoginResp?.Invoke(resp);
    }

    /// <summary>
    /// 发送 proto 消息（自动包成信封交给传输层）
    /// </summary>
    /// <typeparam name="T">proto 消息类型</typeparam>
    /// <param name="msgType">消息类型（NetMsg.MsgType 枚举）</param>
    /// <param name="msg">消息内容</param>
    public void Send<T>(NetMsg.MsgType msgType, T msg) where T : IMessage
    {
        var envelope = new NetMsg.NetMsgEnvelope
        {
            MsgType = msgType,
            Payload = msg.ToByteString(),
        };
        //CoreMgr.Net.SendRaw(envelope.ToByteArray());
    }

    /// <summary>
    /// 收到原始字节：解析信封并按类型分发
    /// </summary>
    private void OnRawDataReceived(byte[] raw)
    {
        try
        {
            var envelope = NetMsg.NetMsgEnvelope.Parser.ParseFrom(raw);
            HandleEnvelope(envelope);
        }
        catch (Exception e)
        {
            Log.Error("解析 proto 消息失败:", e.Message);
        }
    }

    /// <summary>
    /// 按 msg_type 分发处理
    /// </summary>
    private void HandleEnvelope(NetMsg.NetMsgEnvelope envelope)
    {
        // FrameData 频率高（20 帧/秒），不打印日志避免刷屏
        if (envelope.MsgType != NetMsg.MsgType.MsgFrameData)
            Log.Info("收到服务器消息, 类型:", envelope.MsgType);

        switch (envelope.MsgType)
        {
            case NetMsg.MsgType.MsgFrameData:
                OnFrameData?.Invoke(NetMsg.FrameData.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgUdpLoginResp:
                HandleUdpLoginResp(NetMsg.UdpLoginResp.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgRoomResp:
                OnRoomResp?.Invoke(NetMsg.RoomResp.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgRoomStatePush:
                OnRoomStatePush?.Invoke(NetMsg.RoomStatePush.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgLeaveRoomResp:
                OnLeaveRoomResp?.Invoke(NetMsg.LeaveRoomResp.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgMatchResp:
                OnMatchResp?.Invoke(NetMsg.MatchResp.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgReadyResp:
                OnReadyResp?.Invoke(NetMsg.ReadyResp.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgStartGamePush:
                OnStartGamePush?.Invoke(NetMsg.StartGamePush.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgGameEndResp:
                OnGameEndResp?.Invoke(NetMsg.GameEndResp.Parser.ParseFrom(envelope.Payload));
                break;

            case NetMsg.MsgType.MsgGameEndPush:
                OnGameEndPush?.Invoke(NetMsg.GameEndPush.Parser.ParseFrom(envelope.Payload));
                break;

            default:
                Log.Warning("未知消息类型:", envelope.MsgType);
                break;
        }
    }

    /// <summary>房间请求回复（建房/加房结果，RoomMgr 订阅）</summary>
    public event Action<NetMsg.RoomResp> OnRoomResp;

    /// <summary>房间状态推送（成员列表变化，RoomMgr 订阅）</summary>
    public event Action<NetMsg.RoomStatePush> OnRoomStatePush;

    /// <summary>退出房间回复（RoomMgr 订阅）</summary>
    public event Action<NetMsg.LeaveRoomResp> OnLeaveRoomResp;

    /// <summary>匹配回复（受理回执/撮合成功，RoomMgr 订阅）</summary>
    public event Action<NetMsg.MatchResp> OnMatchResp;

    /// <summary>就绪回复（RoomMgr 订阅）</summary>
    public event Action<NetMsg.ReadyResp> OnReadyResp;

    /// <summary>开战推送（全员就绪：起始帧+随机种子，RoomMgr 订阅）</summary>
    public event Action<NetMsg.StartGamePush> OnStartGamePush;

    /// <summary>游戏结束回执（RoomMgr 订阅）</summary>
    public event Action<NetMsg.GameEndResp> OnGameEndResp;

    /// <summary>游戏结束推送（结束帧+结果，RoomMgr 订阅）</summary>
    public event Action<NetMsg.GameEndPush> OnGameEndPush;

    public override void OnSceneExit()
    {
        // 取消订阅，避免泄漏
        //CoreMgr.Net.OnRawData -= OnRawDataReceived;
    }
}
