/*--------------------------------------------------------------
 * File: ApiRegistry.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/17 12:00:00 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

/// <summary>
/// 接口注册表：请求/响应类型 → HTTP URL。
/// 业务层调用 HttpMsgHandler 时无需写 URL，由本表根据类型自动查找。
/// 新增接口：在 RegisterAll 中加一行即可
/// </summary>
public static class ApiRegistry
{
    // POST：请求类型 → URL
    private static readonly Dictionary<Type, string> s_postUrl = new();

    // GET：响应类型 → URL（GET 无请求体，用响应类型标识接口）
    private static readonly Dictionary<Type, string> s_getUrl = new();

    /// <summary>注册 POST 接口：请求类型 → 完整 URL（自动拼接 LoginServerUrl）</summary>
    private static void RegisterPost<TReq>(string apiPath) => s_postUrl[typeof(TReq)] = Combine(apiPath);

    /// <summary>注册 GET 接口：响应类型 → 完整 URL（自动拼接 LoginServerUrl）</summary>
    private static void RegisterGet<TResp>(string apiPath) => s_getUrl[typeof(TResp)] = Combine(apiPath);

    /// <summary>相对路径（如 /register）拼上登录服务器地址，导 proto 注册时统一处理</summary>
    private static string Combine(string apiPath) => GameConfig.LoginServerUrl + apiPath;

    /// <summary>注册所有接口（与 NetApi.cs 的 URL 约定一一对应）</summary>
    public static void RegisterAll()
    {
        // ===== 认证 =====
        RegisterPost<NetMsg.RegisterReq>(NetApi.Register);
        RegisterPost<NetMsg.LoginReq>(NetApi.Login);
        RegisterGet<NetMsg.GetMeResp>(NetApi.GetMe);
        // ===== 养成数据 =====
        RegisterGet<NetMsg.GetDataResp>(NetApi.GetData);
    }

    /// <summary>按请求类型查 POST URL</summary>
    public static string GetPostUrl(Type reqType)
    {
        if (s_postUrl.TryGetValue(reqType, out var url)) return url;
        throw new InvalidOperationException($"未注册 POST 接口: {reqType.Name}，请先在 ApiRegistry.RegisterAll 注册");
    }

    /// <summary>按响应类型查 GET URL</summary>
    public static string GetGetUrl(Type respType)
    {
        if (s_getUrl.TryGetValue(respType, out var url)) return url;
        throw new InvalidOperationException($"未注册 GET 接口: {respType.Name}，请先在 ApiRegistry.RegisterAll 注册");
    }
}
