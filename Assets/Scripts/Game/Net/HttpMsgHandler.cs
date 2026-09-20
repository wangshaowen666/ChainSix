/*--------------------------------------------------------------
 * File: HttpMsgHandler.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/17 11:00:00 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Google.Protobuf;

/// <summary>
/// HTTP 协议层（热更）：
/// 在 Framework 的 HttpMgr 字节传输基础上，封装 proto 消息的收发与错误码解析。
/// 底层用 UniTask 异步执行，对外提供回调 API（业务层无需 async/await）。
/// 依赖方向：Game(本类) → Framework(HttpMgr)，单向正确
/// </summary>
public class HttpMsgHandler : ManagerBase
{
    // headers 缓存：token 只在登录态变化时变，缓存复用避免每次请求分配
    private Dictionary<string, string> _headers;

    public override void OnInit()
    {
        ApiRegistry.RegisterAll();
        // 登录态变化时（登录/登出）使 headers 缓存失效，下次请求重建
        GameMgr.Account.OnLoginStateChanged += InvalidateHeaders;
    }

    public override void OnSceneExit()
    {
        //GameMgr.Account.OnLoginStateChanged -= InvalidateHeaders;
    }

    /// <summary>
    /// POST 请求，请求/响应体均为 proto 二进制，完成后回调响应。
    /// URL 由请求类型自动查表（见 ApiRegistry）；token 头由内部自动附加
    /// </summary>
    public void Post<TResp>(IMessage req, Action<TResp> onResp)
        where TResp : class, IMessage<TResp>, new()
    {
        var url = ApiRegistry.GetPostUrl(req.GetType());
        PostAsync(url, req.ToByteArray(), onResp).Forget();
    }

    /// <summary>
    /// GET 请求，响应体为 proto 二进制，完成后回调响应。
    /// URL 由响应类型自动查表（见 ApiRegistry）；token 头由内部自动附加
    /// </summary>
    public void Get<TResp>(Action<TResp> onResp)
        where TResp : class, IMessage<TResp>, new()
    {
        var url = ApiRegistry.GetGetUrl(typeof(TResp));
        GetAsync(url, onResp).Forget();
    }

    private async UniTaskVoid PostAsync<TResp>(string url, byte[] body, Action<TResp> onResp)
        where TResp : class, IMessage<TResp>, new()
    {
        var httpResp = await CoreMgr.Http.Post(url, body, GetHeaders());
        onResp?.Invoke(RespParser<TResp>.Parse(httpResp));
    }

    private async UniTaskVoid GetAsync<TResp>(string url, Action<TResp> onResp)
        where TResp : class, IMessage<TResp>, new()
    {
        var httpResp = await CoreMgr.Http.Get(url, GetHeaders());
        onResp?.Invoke(RespParser<TResp>.Parse(httpResp));
    }

    /// <summary>
    /// 请求头（懒加载缓存）：已登录时自动附加 token，POST/GET 共用。
    /// GET 携带 Content-Type 无害（服务器端不校验 GET 的 Content-Type）
    /// </summary>
    private Dictionary<string, string> GetHeaders()
    {
        if (_headers == null)
        {
            _headers = new Dictionary<string, string>();
            if (GameMgr.Account.IsLoggedIn)
                _headers["Authorization"] = "Bearer " + GameMgr.Account.Token;
            _headers["Content-Type"] = "application/x-protobuf";
        }
        return _headers;
    }

    /// <summary>登录态变化时使 headers 缓存失效（下次请求重建）</summary>
    private void InvalidateHeaders()
    {
        _headers = null;
    }
}

/// <summary>
/// proto 响应解析器（泛型静态缓存，每个 TResp 类型只初始化一次）
/// </summary>
internal static class RespParser<TResp> where TResp : class, IMessage<TResp>, new()
{
    /// <summary>解析器只建一次</summary>
    private static readonly MessageParser<TResp> Parser = new(() => new TResp());

    /// <summary>ErrorCode 属性缓存（避免失败路径每次 GetProperty 反射）</summary>
    private static readonly PropertyInfo ErrorCodeProp = typeof(TResp).GetProperty("ErrorCode");

    public static TResp Parse(HttpBytesResp httpResp)
    {
        var bytes = httpResp?.Data;
        if (bytes != null && bytes.Length > 0)
        {
            try
            {
                return Parser.ParseFrom(bytes);
            }
            catch (Exception e)
            {
                Log.Error("解析 proto 响应失败:", e.Message, "状态码:", httpResp?.StatusCode);
            }
        }

        // 空 body：proto3 默认值（0）不序列化，成功响应的 error_code=0 时 body 为空是正常现象
        // 2xx + 空 body = 成功；非 2xx + 空 body = 服务器异常
        if (httpResp is { StatusCode: >= 200 and < 300 })
            return new TResp();

        var resp = new TResp();
        ErrorCodeProp?.SetValue(resp, NetMsg.ErrorCode.ErrorInternal);
        return resp;
    }
}
