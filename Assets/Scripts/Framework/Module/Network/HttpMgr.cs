/*--------------------------------------------------------------
 * File: HttpMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/26 15:31:12 
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// HTTP 字节传输层（Framework，AOT）：
/// 只负责发起 HTTP 请求、收发原始字节，不依赖任何协议。
/// 具体协议（proto 消息等）在热更层封装（见 HttpMsgHelper）。
/// </summary>
public class HttpMgr : ManagerBase
{
    /// <summary>
    /// POST 请求，body 为原始字节，返回状态码 + 响应字节
    /// </summary>
    public async UniTask<HttpBytesResp> Post(string url, byte[] body, Dictionary<string, string> headers = null)
    {
        using var webRequest = new UnityWebRequest(url, "POST");

        if (body != null && body.Length > 0)
            webRequest.uploadHandler = new UploadHandlerRaw(body);
        webRequest.downloadHandler = new DownloadHandlerBuffer();

        ApplyHeaders(webRequest, headers);

        await SendAndIgnoreError(webRequest);
        return new HttpBytesResp
        {
            StatusCode = webRequest.responseCode,
            Data = webRequest.downloadHandler?.data,
        };
    }

    /// <summary>
    /// GET 请求，返回状态码 + 响应字节
    /// </summary>
    public async UniTask<HttpBytesResp> Get(string url, Dictionary<string, string> headers = null)
    {
        using var webRequest = UnityWebRequest.Get(url);
        webRequest.downloadHandler = new DownloadHandlerBuffer();

        ApplyHeaders(webRequest, headers);

        await SendAndIgnoreError(webRequest);
        return new HttpBytesResp
        {
            StatusCode = webRequest.responseCode,
            Data = webRequest.downloadHandler?.data,
        };
    }

    /// <summary>
    /// 发送请求但不因 HTTP 非 2xx 抛异常：
    /// Unity 在状态码 >= 400 时会把 await 转成 UnityWebRequestException，
    /// 但错误码在响应体里，所以必须继续读取响应体
    /// </summary>
    private static async UniTask SendAndIgnoreError(UnityWebRequest webRequest)
    {
        try
        {
            await webRequest.SendWebRequest();
        }
        catch (UnityWebRequestException)
        {
            // 非 2xx：不抛给上层，由调用方从响应体解析错误
        }
    }

    private static void ApplyHeaders(UnityWebRequest request, Dictionary<string, string> headers)
    {
        if (headers == null) return;
        foreach (var kv in headers)
            request.SetRequestHeader(kv.Key, kv.Value);
    }
}

/// <summary>
/// HTTP 字节响应（传输层结果）
/// </summary>
public class HttpBytesResp
{
    /// <summary>HTTP 状态码：200 成功 / 400 请求错误 / 401 未授权等</summary>
    public long StatusCode;
    /// <summary>响应原始字节</summary>
    public byte[] Data;
}
