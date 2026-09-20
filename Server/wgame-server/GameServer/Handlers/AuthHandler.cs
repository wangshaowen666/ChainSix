namespace GameServer;

/// <summary>
/// 认证消息处理：UdpLoginReq（JWT 校验 + 会话绑定；顶号踢旧在 SessionMgr.Bind 内）
/// </summary>
public sealed class AuthHandler
{
    public AuthHandler(MsgDispatcher dispatcher, NetServer net, SessionMgr sessions, JwtValidator jwt)
    {
        dispatcher.Register(NetMsg.MsgType.MsgUdpLoginReq, NetMsg.UdpLoginReq.Parser, (peer, req) =>
        {
            if (jwt.Validate(req.Token, out var playerId))
            {
                // 绑定会话（含顶号踢旧连接/本连接换号清理旧归属）
                sessions.Bind(peer, playerId);
                Console.WriteLine($"[认证成功] playerId={playerId} ({peer.Address}:{peer.Port})");
                net.SendTo(peer, NetMsg.MsgType.MsgUdpLoginResp, new NetMsg.UdpLoginResp
                {
                    ErrorCode = NetMsg.ErrorCode.ErrorNone,
                    PlayerId = playerId,
                });
            }
            else
            {
                Console.WriteLine($"[认证失败] token 无效或已过期 ({peer.Address}:{peer.Port})");
                net.SendTo(peer, NetMsg.MsgType.MsgUdpLoginResp, new NetMsg.UdpLoginResp
                {
                    ErrorCode = NetMsg.ErrorCode.ErrorUnauthorized,
                });
                sessions.OnAuthFail(peer); // 连续失败超限由 SessionMgr 断开连接（防刷包）
            }
        });
    }
}
