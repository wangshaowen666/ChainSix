// 公共 HTTP API 路径常量（前后端共享）
// 服务器 LoginServer.csproj 链接引用本文件；客户端由 gen_proto.sh 复制到 Game/Net
// 两端必须使用同一份，改 API 路径只改这里
//
// 命名约定（供 ToolBox 自动补全 ApiRegistry.RegisterAll 使用）：
//   - 字段名 X + 注释 // POST  → 注册为 POST，请求类型 XReq
//   - 字段名 X + 注释 // GET   → 注册为 GET，响应类型 XResp
// 注释里的方法标识不区分大小写（post/get/POST/GET 均可）
// 字段名必须与 proto 类型名语义一致（如 GetMe 对应 GetMeResp）
public static class NetApi
{
    // ===== 认证 =====
    public const string Register = "/register";   // POST：注册
    public const string Login = "/login";         // POST：登录
    public const string GetMe = "/me";            // GET：验证 token 取玩家信息

    // ===== 养成数据 =====
    public const string GetData = "/data";        // GET：获取养成数据（写入由服务器业务接口主导，客户端无写权限）
}
