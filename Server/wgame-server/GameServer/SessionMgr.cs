using LiteNetLib;

namespace GameServer;

/// <summary>
/// 会话管理：连接 -> 认证 playerId 绑定表。
/// - Bind 含顶号：同 playerId 新连接认证时同步清理并踢掉旧连接，
///   保证 playerId 任意时刻至多一个活跃连接（清理必须同步做，不能只 Disconnect
///   等异步断线事件，Disconnect 到事件触发之间的窗口会双连接并存）
/// - CleanupPeer 全归属清理（解绑 + 触发 PeerCleanedUp 让队列/房间各自清理），幂等
/// </summary>
public sealed class SessionMgr
{
    /// <summary>连接 -> 认证后的 playerId（未认证的连接不在表内）</summary>
    private readonly Dictionary<NetPeer, int> _peerPlayers = new();

    /// <summary>连接 -> 连续认证失败次数（成功清零；超限断开，防刷包）</summary>
    private readonly Dictionary<NetPeer, int> _authFails = new();

    /// <summary>连续认证失败上限，达到即断开连接</summary>
    private const int MaxAuthFails = 3;

    /// <summary>连接清理时触发（断线/顶号/换号共用），MatchMaker/RoomMgr 订阅做各自清理</summary>
    public event Action<NetPeer>? PeerCleanedUp;

    public SessionMgr(NetServer net)
    {
        net.PeerDisconnected += (peer, info) => CleanupPeer(peer, $"断线 {info.Reason}");
    }

    /// <summary>取连接绑定的 playerId（未认证返回 0）</summary>
    public int GetPlayerId(NetPeer peer)
        => _peerPlayers.TryGetValue(peer, out var id) ? id : 0;

    /// <summary>连接是否已认证（已认证则带出 playerId）</summary>
    public bool TryGetPlayerId(NetPeer peer, out int playerId)
        => _peerPlayers.TryGetValue(peer, out playerId);

    /// <summary>
    /// 认证成功后绑定身份（含顶号踢旧 + 本连接换号清理旧归属）
    /// </summary>
    public void Bind(NetPeer peer, int playerId)
    {
        // 顶号踢旧连接：同 playerId 已有其他连接 -> 同步清理 + 断开
        NetPeer? oldPeer = null;
        foreach (var kv in _peerPlayers)
            if (kv.Value == playerId && kv.Key != peer)
                oldPeer = kv.Key;
        if (oldPeer != null)
        {
            Console.WriteLine($"[顶号] playerId={playerId} 新连接挤掉旧连接 ({oldPeer.Address}:{oldPeer.Port})");
            CleanupPeer(oldPeer, "顶号");
            oldPeer.Disconnect(); // 触发的断线事件再走一次 CleanupPeer，幂等无害
        }

        // 本连接重复认证换账号：先清自己的旧归属（出队/退房）再绑新身份
        if (_peerPlayers.TryGetValue(peer, out var oldPid) && oldPid != playerId)
            CleanupPeer(peer, $"重复认证换号 {oldPid}->{playerId}");

        _peerPlayers[peer] = playerId;
        _authFails.Remove(peer); // 认证成功，清失败计数
    }

    /// <summary>
    /// 认证失败计数，连续达到 MaxAuthFails 断开连接（防伪造 token 刷包）
    /// </summary>
    public void OnAuthFail(NetPeer peer)
    {
        var count = (_authFails.TryGetValue(peer, out var c) ? c : 0) + 1;
        _authFails[peer] = count;
        Console.WriteLine($"[防刷] ({peer.Address}:{peer.Port}) 认证失败 {count}/{MaxAuthFails}");

        if (count >= MaxAuthFails)
        {
            CleanupPeer(peer, "认证失败超限");
            peer.Disconnect();
        }
    }

    /// <summary>
    /// 同步清理一个连接的全部归属：解绑 + 触发 PeerCleanedUp（出队/退房）。
    /// 断线事件与顶号踢旧连接共用此入口，幂等可重复调用
    /// </summary>
    public void CleanupPeer(NetPeer peer, string reason)
    {
        var hadAuth = _peerPlayers.TryGetValue(peer, out var pid);
        _peerPlayers.Remove(peer);
        _authFails.Remove(peer);
        PeerCleanedUp?.Invoke(peer);
        Console.WriteLine($"[清理连接] playerId={(hadAuth ? pid : -1)}, 原因: {reason}");
    }
}
