using Google.Protobuf;

namespace LoginServer.Services;

/// <summary>
/// proto 的 HTTP 传输辅助：请求体读 proto，响应体写 proto（Content-Type: application/x-protobuf）
/// </summary>
public static class ProtoHttp
{
    /// <summary>读取请求体并解析为 proto 消息（空 body 返回默认实例）</summary>
    public static async Task<T> ReadReq<T>(HttpContext ctx) where T : IMessage<T>, new()
    {
        var bytes = new byte[ctx.Request.ContentLength ?? 0];
        if (bytes.Length > 0)
        {
            await ctx.Request.Body.ReadExactlyAsync(bytes);
            return new MessageParser<T>(() => new T()).ParseFrom(bytes);
        }
        return new T();
    }

    /// <summary>把 proto 消息写入响应体，Content-Type: application/x-protobuf</summary>
    public static async Task WriteResp(HttpContext ctx, IMessage msg, int statusCode = 200)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/x-protobuf";
        await ctx.Response.Body.WriteAsync(msg.ToByteArray());
    }
}
