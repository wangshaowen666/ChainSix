using System.Security.Claims;
using LoginServer.Data;
using LoginServer.Services;

namespace LoginServer.Endpoints;

/// <summary>认证相关路由（proto 收发）</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // 注册（无需登录）
        app.MapPost(NetApi.Register, async (HttpContext ctx, PlayerRepository repo,
            PlayerProfileRepository profileRepo, PasswordService pwd) =>
        {
            var req = await ProtoHttp.ReadReq<NetMsg.RegisterReq>(ctx);

            var existing = await repo.GetByUsername(req.Username);
            if (existing != null)
            {
                await ProtoHttp.WriteResp(ctx, new NetMsg.RegisterResp { ErrorCode = NetMsg.ErrorCode.ErrorUserExists }, 400);
                return;
            }

            var playerId = await repo.CreatePlayer(req.Username, pwd.Hash(req.Password));
            // 注册时自动创建默认养成数据
            await profileRepo.CreateDefault(playerId);

            Console.WriteLine($"新用户注册: {req.Username}");
            await ProtoHttp.WriteResp(ctx, new NetMsg.RegisterResp { ErrorCode = NetMsg.ErrorCode.ErrorNone });
        });

        // 登录（无需登录）
        app.MapPost(NetApi.Login, async (HttpContext ctx, PlayerRepository repo, PasswordService pwd, JwtService jwt) =>
        {
            var req = await ProtoHttp.ReadReq<NetMsg.LoginReq>(ctx);

            var player = await repo.GetByUsername(req.Username);
            if (player == null || string.IsNullOrEmpty(player.PasswordHash) ||
                !pwd.Verify(req.Password, player.PasswordHash))
            {
                await ProtoHttp.WriteResp(ctx, new NetMsg.LoginResp { ErrorCode = NetMsg.ErrorCode.ErrorWrongCredentials }, 401);
                return;
            }

            var token = jwt.GenerateToken(player.Id, player.Username ?? "");
            Console.WriteLine($"用户登录: {req.Username}");
            await ProtoHttp.WriteResp(ctx, new NetMsg.LoginResp
            {
                Token = token,
                PlayerId = player.Id,
                Username = player.Username ?? "",
            });
        });

        // 验证 token（需要登录，由认证中间件统一校验）
        app.MapGet(NetApi.GetMe, async (HttpContext context, PlayerRepository repo) =>
        {
            var idStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (idStr == null)
            {
                await ProtoHttp.WriteResp(context, new NetMsg.GetMeResp { ErrorCode = NetMsg.ErrorCode.ErrorUnauthorized }, 401);
                return;
            }

            var player = await repo.GetById(int.Parse(idStr));
            if (player == null)
            {
                await ProtoHttp.WriteResp(context, new NetMsg.GetMeResp { ErrorCode = NetMsg.ErrorCode.ErrorPlayerNotFound }, 404);
                return;
            }

            await ProtoHttp.WriteResp(context, new NetMsg.GetMeResp
            {
                PlayerId = player.Id,
                Username = player.Username ?? "",
                CreatedAt = player.CreatedAt ?? "",
            });
        }).RequireAuthorization();
    }
}
