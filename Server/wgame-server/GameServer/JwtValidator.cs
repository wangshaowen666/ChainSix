using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GameServer;

/// <summary>
/// JWT 校验（口径与 LoginServer JwtBearer 中间件一致）
/// </summary>
public sealed class JwtValidator
{
    private readonly string _secret;

    public JwtValidator(string secret) => _secret = secret;

    /// <summary>校验 token（签名 + 过期）并解析 playerId，失败返回 false</summary>
    public bool Validate(string token, out int playerId)
    {
        playerId = 0;
        if (string.IsNullOrEmpty(token)) return false;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            var idStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idStr, out playerId);
        }
        catch (Exception)
        {
            return false; // 签名不符/格式错误/过期等，一律视为无效
        }
    }
}
