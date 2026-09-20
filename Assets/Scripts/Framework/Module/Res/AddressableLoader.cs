/*--------------------------------------------------------------
 * File: AddressableLoader.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/09/01 11:00:17 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class AddressableLoader : IResLoader
{
    private readonly Dictionary<string, AsyncOperationHandle> _handleMap = new();
    // Addressable内部也会引用计数，但是没有忽略计数直接卸载的方法，我们也拿不到它的内部计数，所以自己维护了一个计数
    private readonly Dictionary<string, int> _refMap = new();

    public async UniTask<T> LoadAsync<T>(string key)
    {
#if STATS_ON && UNITY_EDITOR
        UpdateStats(key,1);
#endif
        try
        {
            if (_handleMap.TryGetValue(key, out AsyncOperationHandle tempHandle))
            {
                _refMap.TryGetValue(key, out int c);
                _refMap[key] = c + 1;
                if (!tempHandle.IsDone)
                    await tempHandle.Task;
                if (tempHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Log.Error($"资源异步加载失败: {key}", tempHandle.OperationException);
                    return default;
                }
                return (T)tempHandle.Result;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            // 不等待完成，直接添加到map中。如果等完成后再添加，需要处理加载中又触发了相同key的加载。
            // 后者TryGetValue失败，引用未被管理，但Addressable 的 ResourceManager 内部列表持有，gc不会将其回收，且该bundle计数始终+1了，导致bundle常驻内存，无法卸载
            _handleMap[key] = handle;
            _refMap.TryGetValue(key, out int count);
            _refMap[key] = count + 1;

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                _handleMap.Remove(key);
                _refMap.Remove(key);
                Addressables.Release(handle);
                Log.Error($"资源异步加载失败: {key}", handle.OperationException);
                return default;
            }

            return handle.Result;
        }
        catch (Exception e)
        {
            Log.Error($"资源异步加载异常: {key}", e);
            return default;
        }
    }

    public async UniTask LoadSceneAsync(string sceneName, Action<float> onProgress = null)
    {
        try
        {
            var handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            while (!handle.IsDone)
            {
                onProgress?.Invoke(handle.PercentComplete);
                await UniTask.Yield();
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(handle);
                Log.Error("场景加载失败:", sceneName);
                return;
            }

            onProgress?.Invoke(1f);
        }
        catch (Exception e)
        {
            Log.Error("场景加载异常:", sceneName, e.Message, e.StackTrace);
        }
    }

    public async UniTask<IList<T>> PreloadWithLabel<T>(string label)
    {
        try
        {
            var handle = Addressables.LoadAssetsAsync<T>(label, _ => { });
            _handleMap[label] = handle;
            _refMap[label] = 1;

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                _handleMap.Remove(label);
                _refMap.Remove(label);
                Addressables.Release(handle);
                Log.Error($"预加载失败:{label}  {handle.OperationException}");
                return default;
            }

            return handle.Result;
        }
        catch (Exception e)
        {
            Log.Error($"预加载异常: {label}", e);
            return default;
        }
    }
    
    public void Unload(string key)
    {
        if (!_handleMap.TryGetValue(key, out var handle))
        {
            Log.Error("要卸载的资源不存在", key);
            return;
        }

        if (!_refMap.TryGetValue(key, out int refCount))
        {
            Log.Error("要卸载的资源引用计数不存在", key);
            return;
        }

#if STATS_ON && UNITY_EDITOR
        UpdateStats(key,2, 1);
#endif

        int count = refCount - 1;
        if (count <= 0)
        {
            _handleMap.Remove(key);
            _refMap.Remove(key);
            Addressables.Release(handle);
        }
        else
        {
            _refMap[key] = count;
        }
    }
    
    public void UnloadAll()
    {
        foreach (var kv in _handleMap)
        {
            if (kv.Value.IsValid())
            {
                _refMap.TryGetValue(kv.Key, out int count);
#if STATS_ON && UNITY_EDITOR
                if (count > 0)
                    UpdateStats(kv.Key,2, count);
#endif
                Addressables.Release(kv.Value);
            }
        }

        _handleMap.Clear();
        _refMap.Clear();
    }
    
    public int GetLoadedCount(string key)
    {
        _refMap.TryGetValue(key, out int count);
        return count;
    }
    
#if STATS_ON && UNITY_EDITOR
    private readonly Dictionary<string, AssetBundleStats> _statsMap = new();

    public AddressableLoader()
    {
        Application.quitting += CheckLeaks;
    }

    /// <summary>
    /// 程序退出时检测资源泄漏
    /// </summary>
    private void CheckLeaks()
    {
        bool hasLeak = false;
        foreach (var statsKV in _statsMap)
        {
            if (statsKV.Value.currentNum != 0)
            {
                hasLeak = true;
                Log.Warning($"资源泄漏: {statsKV.Key}, 当前引用数={statsKV.Value.currentNum}");
            }
        }
        if (!hasLeak)
            Log.Info("资源泄漏检测通过，无泄漏资源");
    }

    /// <summary>
    /// 更新统计信息
    /// </summary>
    /// <param name="key">bundle名</param>
    /// <param name="flag">操作标识。1获取 2释放</param>
    /// <param name="count">数量</param>
    private void UpdateStats(string key, int flag, int count = 1)
    {
        if (!_statsMap.TryGetValue(key, out var stats))
        {
            stats = new AssetBundleStats();
            _statsMap[key] = stats;
        }
        switch (flag)
        {
            case 1:
                stats.currentNum += count;
                stats.totalGets += count;

                if (stats.peakNUm < stats.currentNum)
                    stats.peakNUm = stats.currentNum;
                break;

            case 2:
                stats.currentNum -= count;
                stats.totalPuts += count;
                break;
        }
    }

    public List<string> DealPoolStats()
    {
        List<string> result = new List<string>();
        foreach (var statsKV in _statsMap)
        {
            var stats = statsKV.Value;
            result.Add($"{statsKV.Key},{stats.currentNum},{stats.peakNUm}," +
                       $"{stats.totalGets},{stats.totalPuts}");
        }

        return result;
    }
#endif
}

#if STATS_ON && UNITY_EDITOR
/// <summary>
/// 对象池统计信息
/// </summary>
public class AssetBundleStats
{
    public int currentNum;      // 当前引用数量
    public int peakNUm;         // 峰值引用数量
    public long totalGets;      // 总获取次数
    public long totalPuts;      // 总释放次数
}
#endif