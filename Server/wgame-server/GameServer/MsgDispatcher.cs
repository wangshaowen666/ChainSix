using Google.Protobuf;
using LiteNetLib;

namespace GameServer;

/// <summary>
/// 消息分发器：MsgType -> 处理器注册表。
/// 各 Handler 在构造时向本类注册消息，新增协议消息只需在对应 Handler 注册，
/// 分发器与 Program 均无需改动（取代集中式 switch-case 分发）
/// </summary>
public sealed class MsgDispatcher
{
    private readonly Dictionary<NetMsg.MsgType, Action<NetPeer, ByteString>> _handlers = new();

    /// <summary>注册消息处理器（传入该消息的 Parser，payload 自动解析后回调）</summary>
    public void Register<TMsg>(NetMsg.MsgType type, MessageParser<TMsg> parser, Action<NetPeer, TMsg> handler)
        where TMsg : IMessage<TMsg>
    {
        _handlers[type] = (peer, payload) => handler(peer, parser.ParseFrom(payload));
    }

    /// <summary>按信封 msg_type 分发；未注册的类型打警告</summary>
    public void Dispatch(NetPeer peer, NetMsg.NetMsgEnvelope envelope)
    {
        if (_handlers.TryGetValue(envelope.MsgType, out var handler))
            handler(peer, envelope.Payload);
        else
            Console.WriteLine($"[警告] 未知消息类型: {envelope.MsgType}");
    }
}
