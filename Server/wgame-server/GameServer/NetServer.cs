using Google.Protobuf;
using LiteNetLib;

namespace GameServer;

/// <summary>
/// 网络服务：LiteNetLib 封装。
/// - 连接请求直接接受（认证在其后通过 UdpLoginReq 完成）
/// - 收到数据解析 proto 信封后交 MsgDispatcher 分发
/// - SendTo 统一打信封发送给指定连接
/// </summary>
public sealed class NetServer
{
    private readonly MsgDispatcher _dispatcher;
    private NetManager _net = null!;

    /// <summary>连接断开（主动断/超时/被踢），SessionMgr 订阅做会话清理</summary>
    public event Action<NetPeer, DisconnectInfo>? PeerDisconnected;

    public NetServer(MsgDispatcher dispatcher) => _dispatcher = dispatcher;

    public bool Start(int port)
    {
        var listener = new EventBasedNetListener();

        // 客户端连接请求 -> 接受（认证在其后通过 UdpLoginReq 完成）
        listener.ConnectionRequestEvent += request =>
        {
            request.Accept();
            Console.WriteLine($"[连接请求] 接受客户端: {request.RemoteEndPoint}");
        };

        listener.PeerConnectedEvent += peer =>
            Console.WriteLine($"[已连接] 客户端: {peer.Address}:{peer.Port}, 等待认证...");

        listener.PeerDisconnectedEvent += (peer, info) => PeerDisconnected?.Invoke(peer, info);

        // 收到客户端数据：解析 proto 信封并分发
        listener.NetworkReceiveEvent += (peer, reader, _, _) =>
        {
            var raw = reader.GetRemainingBytes();
            reader.Recycle();
            if (raw == null || raw.Length == 0) return;

            try
            {
                _dispatcher.Dispatch(peer, NetMsg.NetMsgEnvelope.Parser.ParseFrom(raw));
            }
            catch (Exception e)
            {
                Console.WriteLine($"[警告] 解析 proto 消息失败: {e.Message}");
            }
        };

        _net = new NetManager(listener)
        {
            AutoRecycle = true,
            DisconnectTimeout = 10000,
        };
        return _net.Start(port);
    }

    /// <summary>轮询网络事件（主循环驱动，事件回调在同一线程串行执行）</summary>
    public void PollEvents() => _net.PollEvents();

    /// <summary>发送 proto 消息到指定连接（自动包信封）</summary>
    public void SendTo(NetPeer peer, NetMsg.MsgType msgType, IMessage msg)
    {
        var envelope = new NetMsg.NetMsgEnvelope
        {
            MsgType = msgType,
            Payload = msg.ToByteString(),
        };
        peer.Send(envelope.ToByteArray(), DeliveryMethod.ReliableOrdered);
    }
}
