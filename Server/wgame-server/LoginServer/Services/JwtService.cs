using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace LoginServer.Services;

/// <summary>JWT 令牌生成（校验由认证中间件 JwtBearer 统一处理）</summary>
public class JwtService
{
    private readonly SymmetricSecurityKey _key;
    private readonly int _expireMinutes;

    public JwtService(IConfiguration config)
    {
        // 密钥来自配置 Jwt:Secret（appsettings.json），生产环境应放到环境变量
        var secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("缺少配置 Jwt:Secret");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _expireMinutes = int.TryParse(config["Jwt:ExpireMinutes"], out var m) ? m : 60 * 24;
    }

    /// <summary>生成 token</summary>
    public string GenerateToken(int playerId, string username)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, playerId.ToString()),
            new Claim(ClaimTypes.Name, username),
        };

        var token = new JwtSecurityToken(
            issuer: null,
            audience: null,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expireMinutes),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
