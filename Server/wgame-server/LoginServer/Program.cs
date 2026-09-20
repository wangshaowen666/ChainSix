using System.Text;
using LoginServer.Data;
using LoginServer.Endpoints;
using LoginServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 监听所有网卡（真机/局域网客户端可访问；本地调试可用 ASPNETCORE_URLS 覆盖）
builder.WebHost.UseUrls("http://0.0.0.0:5049");

// 从配置读取 JWT 密钥
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("缺少配置 Jwt:Secret");

// 注册服务（依赖注入）
builder.Services.AddSingleton<PlayerRepository>();
builder.Services.AddSingleton<PlayerProfileRepository>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<JwtService>();

// 配置 JWT 认证中间件（统一校验 token，替代手动 ValidateToken）
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// 授权服务（配合 RequireAuthorization() 使用）
builder.Services.AddAuthorization();

var app = builder.Build();

// 初始化数据库（建表）
app.Services.GetRequiredService<PlayerRepository>().InitDatabase();
app.Services.GetRequiredService<PlayerProfileRepository>().InitDatabase();
Console.WriteLine("数据库初始化完成");

// 启用认证中间件（必须在路由之前）
app.UseAuthentication();
app.UseAuthorization();

// 注册路由
app.MapAuthEndpoints();
app.MapPlayerEndpoints();

app.Run();
