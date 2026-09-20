// /*--------------------------------------------------------------
//  * File: NetMgr.cs
//  * Author: Wsw
//  * Feedback: 614270423@qq.com
//  * Time: 2026/08/14 10:00:00 
//  *--------------------------------------------------------------
//  */
//
// using System;
// using LiteNetLib;
//
// /// <summary>
// /// 实时网络传输层（Framework，AOT）：
// /// 只负责连接管理、心跳、断线检测、原始字节收发。
// /// 具体协议（NetMsg 等）在热更层解析，这里不依赖任何协议。
// /// </summary>
// public class NetMgr : ManagerBase, IUpdateable
// {
//     private NetManager _netManager;
//     private NetPeer _serverPeer;
//
//     /// <summary>是否已连接服务器</summary>
//     public bool IsConnected =>
//         _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;
//
//     public event Action OnConnected;
//     public event Action OnDisconnected;
//
//     /// <summary>
//     /// 收到原始字节数据（由热更层订阅并解析协议）
//     /// 参数：byte[] 原始数据
//     /// </summary>
//     public event Action<byte[]> OnRawData;
//
//     /// <summary>
//     /// 连接服务器
//     /// </summary>
//     public void Connect(string ip, int port)
//     {
//         var listener = new EventBasedNetListener();
//
//         // 连接成功（客户端视角）
//         listener.PeerConnectedEvent += peer =>
//         {
//             _serverPeer = peer;
//             Log.Info("已连接服务器:", peer.Address, peer.Port);
//             OnConnected?.Invoke();
//         };
//
//         // 断开（含超时/主动断开）
//         listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
//         {
//             _serverPeer = null;
//             Log.Warning("与服务器断开:", disconnectInfo.Reason);
//             OnDisconnected?.Invoke();
//         };
//
//         // 收到数据：转发原始字节给订阅者（协议解析在热更层）
//         listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) =>
//         {
//             var raw = reader.GetRemainingBytes();
//             if (raw != null && raw.Length > 0)
//                 OnRawData?.Invoke(raw);
//             reader.Recycle();
//         };
//
//         _netManager = new NetManager(listener)
//         {
//             AutoRecycle = true,
//             // 心跳保活：LiteNetLib 内置 ping 机制，超过该时长未收到对方数据则判定断线
//             DisconnectTimeout = 10000,
//         };
//
//         _netManager.Start();
//         // connectKey：连接令牌（任务1服务器未校验，任务6匹配时再校验身份）
//         _netManager.Connect(ip, port, "wgame");
//         Log.Info("正在连接服务器:", ip, port);
//
//         // 注册到 UpdateMgr，每帧轮询网络事件
//         CoreMgr.Update.RegisterUpdate(this);
//     }
//
//     /// <summary>
//     /// 主动断开连接
//     /// </summary>
//     public void Disconnect()
//     {
//         _netManager?.Stop();
//         _netManager = null;
//         _serverPeer = null;
//         CoreMgr.Update.UnRegisterUpdate(this);
//     }
//
//     /// <summary>
//     /// 发送原始字节（由热更层把协议序列化好后传进来）
//     /// </summary>
//     public void SendRaw(byte[] data)
//     {
//         if (!IsConnected)
//         {
//             Log.Error("未连接服务器，无法发送消息");
//             return;
//         }
//         _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
//     }
//
//     /// <summary>
//     /// 每帧轮询网络事件（由 UpdateMgr 驱动）
//     /// </summary>
//     public void MyUpdate(float deltaTime, float realDeltaTime)
//     {
//         _netManager?.PollEvents();
//     }
//
//     public int Priority => 100;
//
//     /// <summary>
//     /// 场景退出时断开连接
//     /// </summary>
//     public override void OnSceneExit()
//     {
//         //Disconnect();
//     }
// }
