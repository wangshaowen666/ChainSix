using System.Security.Claims;
using LoginServer.Data;
using LoginServer.Services;

namespace LoginServer.Endpoints;

/// <summary>养成数据相关路由（proto 收发）</summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        // 获取养成数据（需要登录）
        app.MapGet(NetApi.GetData, async (HttpContext context, PlayerProfileRepository repo) =>
        {
            var playerId = GetPlayerId(context);
            if (playerId == null)
            {
                await ProtoHttp.WriteResp(context, new NetMsg.GetDataResp { ErrorCode = NetMsg.ErrorCode.ErrorUnauthorized }, 401);
                return;
            }

            var profile = await repo.GetByPlayerId(playerId.Value);
            if (profile == null)
            {
                await ProtoHttp.WriteResp(context, new NetMsg.GetDataResp { ErrorCode = NetMsg.ErrorCode.ErrorProfileNotFound }, 404);
                return;
            }

            await ProtoHttp.WriteResp(context, new NetMsg.GetDataResp
            {
                Gold = profile.Gold,
                StageProgress = profile.StageProgress,
                TowerLevels = profile.TowerLevels ?? "{}",
            });
        }).RequireAuthorization();
    }

    /// <summary>从已认证的 token 中提取玩家 Id</summary>
    private static int? GetPlayerId(HttpContext context)
    {
        var idStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return idStr != null && int.TryParse(idStr, out var id) ? id : null;
    }
}
