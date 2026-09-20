/*--------------------------------------------------------------
 * File: IResLoader.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/09/01 10:56:06 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public interface IResLoader
{
    /// <summary>
    /// 异步加载资源：可 await 顺序编排；失败时 Log.Error + 返回 default，不抛异常
    /// </summary>
    UniTask<T> LoadAsync<T>(string key);

    /// <summary>
    /// 按标签预载一组资源（预载后资源常驻内存，供同步场景直接使用）；失败返回 default
    /// </summary>
    UniTask<IList<T>> PreloadWithLabel<T>(string label);

    /// <summary>
    /// 异步加载场景：await 完成时机；进度经 onProgress 回调（流式事件）
    /// </summary>
    /// <param name="sceneName">场景名称</param>
    /// <param name="onProgress">加载进度回调（0~1）</param>
    UniTask LoadSceneAsync(string sceneName, Action<float> onProgress = null);

    /// <summary>
    /// 卸载指定key的资源
    /// </summary>
    void Unload(string key);

    /// <summary>
    /// 卸载所有已加载的资源
    /// </summary>
    void UnloadAll();

#if STATS_ON && UNITY_EDITOR
    System.Collections.Generic.List<string> DealPoolStats();
#endif
}