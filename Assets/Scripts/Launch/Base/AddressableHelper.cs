/*--------------------------------------------------------------
 * File: AddressableHelper.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/16 17:14:26 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class ResourceUpdateException : Exception
{
    public int ErrorCode { get; }
    public ResourceUpdateException(int code, string msg = null, Exception inner = null) : base(msg, inner)
        => ErrorCode = code;
}

public class AddressableHelper
{
    public event Action<float, long, long> OnDownloadProgress;
    
    private const string BundleExtension = ".bundle";
    // 远端AB包的前缀
    private const string RemoteBundlePrefix = "remote_";
    private static readonly string[] LoginDownloadLabels = new string[] { "common", "lua"};
    
    private List<IResourceLocation> _totalLocation = new List<IResourceLocation>();
    private long _fileSize;

    public async UniTask InitAsync()
    {
        try
        {
            await Addressables.InitializeAsync();
        }
        catch (Exception e)
        {
            throw new ResourceUpdateException(0, "InitializeAsync 失败", e);
        }
    }

    public async UniTask UpdateCatalog()
    {
        try
        {
            var checkHandle = Addressables.CheckForCatalogUpdates(false);
            try
            {
                await checkHandle;
                if (checkHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new ResourceUpdateException(1, "CheckForCatalogUpdates 失败");

                if (checkHandle.Result is { Count: > 0 })
                {
                    PlayerPrefsUtil.SetInt("UpdateFlag", 0);
                    var updateHandle = Addressables.UpdateCatalogs(checkHandle.Result, false);
                    try
                    {
                        await updateHandle;
                        if (updateHandle.Status != AsyncOperationStatus.Succeeded)
                            throw new ResourceUpdateException(2, "UpdateCatalogs 失败");

#if !UNITY_WEBGL
                        ClearCache();
#endif
                    }
                    finally { Addressables.Release(updateHandle); }
                }
            }
            finally { Addressables.Release(checkHandle); }
        }
        catch (ResourceUpdateException) { throw; }
        catch (Exception e)
        {
            // 包住裸 await 阶段的异常（如切网时的桥接层失败），统一为可跳过的热更失败
            throw new ResourceUpdateException(1, "catalog 检查/更新异常", e);
        }
    }

    public async UniTask CheckRes()
    {
        try
        {
            if (PlayerPrefsUtil.GetInt("UpdateFlag") == 1)
                return;

            // 下载交集，同时满足所有标签的bundle才会下载
            var locationHandle = Addressables.LoadResourceLocationsAsync(LoginDownloadLabels, Addressables.MergeMode.Intersection);
            try
            {
                await locationHandle;
                if (locationHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new ResourceUpdateException(3, "LoadResourceLocationsAsync 失败");

                // 预防CheckRes意外多次执行了
                _totalLocation.Clear();
                _totalLocation.AddRange(locationHandle.Result);

                var sizeHandle = Addressables.GetDownloadSizeAsync(_totalLocation);
                try
                {
                    await sizeHandle;
                    if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
                        throw new ResourceUpdateException(4, "GetDownloadSizeAsync 失败");

                    _fileSize = sizeHandle.Result;
                    Log.Info("热更资源总大小:", GetFileLength(_fileSize));
                }
                finally { Addressables.Release(sizeHandle); }
            }
            finally { Addressables.Release(locationHandle); }
        }
        catch (ResourceUpdateException) { throw; }
        catch (Exception e)
        {
            throw new ResourceUpdateException(4, "检查资源异常", e);
        }
    }

    public async UniTask Download()
    {
        try
        {
            if (_fileSize == 0 || _totalLocation.Count == 0)
                return;

            AsyncOperationHandle downHandle = Addressables.DownloadDependenciesAsync(_totalLocation, false);
            try
            {
                DownloadStatus state;
                while (!downHandle.IsDone)
                {
                    state = downHandle.GetDownloadStatus();
                    OnDownloadProgress?.Invoke(state.Percent, state.DownloadedBytes, state.TotalBytes);
                    await UniTask.Yield();
                }

                if (downHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new ResourceUpdateException(5, "DownloadDependenciesAsync 失败");

                PlayerPrefsUtil.SetInt("UpdateFlag", 1);
                Log.Info("下载成功");
            }
            finally { Addressables.Release(downHandle); }
        }
        catch (ResourceUpdateException) { throw; }
        catch (Exception e)
        {
            throw new ResourceUpdateException(5, "下载资源异常", e);
        }
    }

#if !UNITY_WEBGL
    /// <summary>
    /// 移除本地不在catalog中的旧Bundle
    /// WebGL(小游戏)平台没有 Caching API，缓存由适配层虚拟文件系统管理，无需清理
    /// </summary>
    private void ClearCache()
    {
        var activeBundleHashes = new HashSet<string>();
        foreach (var locator in Addressables.ResourceLocators)
        {
            foreach (var key in locator.Keys)
            {
                var keyString = key.ToString();
                if (keyString.StartsWith(RemoteBundlePrefix))
                    activeBundleHashes.Add(ExtractHashFromKey(keyString));
            }
        }
        
        var allCachePaths = new List<string>();
        Caching.GetAllCachePaths(allCachePaths);
        
        foreach (var cachePath in allCachePaths)
        {
            if (!Directory.Exists(cachePath)) continue;
            foreach (var path in Directory.EnumerateFileSystemEntries(cachePath))
            {
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName)) continue;
                
                var cachedVersions = new List<Hash128>();
                Caching.GetCachedVersions(fileName, cachedVersions);
                foreach (var version in cachedVersions)
                {
                    var versionHash = version.ToString();
                    if (!activeBundleHashes.Contains(versionHash))
                    {
                        Log.Info("移除缓存文件", versionHash);
                        Caching.ClearCachedVersion(fileName, version);
                    }
                }
            }
        }
    }
#endif

    /// <summary>
    /// 从资源键中提取哈希值。
    /// 例如：从 "RemoteABPrefix_1234567890abc.bundle" 中提取 "1234567890abc"。
    /// </summary>
    private string ExtractHashFromKey(string key)
    {
        var lastUnderscoreIndex = key.LastIndexOf('_');
        var hashStartIndex = lastUnderscoreIndex + 1;
        var hashLength = key.Length - hashStartIndex - BundleExtension.Length;
        return hashLength > 0 ? key.AsSpan(hashStartIndex, hashLength).ToString() : string.Empty;
    }
    
    private string GetFileLength(long byteLength)
    {
        if (byteLength < 1024L)
        {
            return $"{byteLength} B";
        }
        
        if (byteLength < 1048576L)
        {
            return $"{byteLength / 1024f:F2} KB";
        }
        
        if (byteLength < 1073741824L) // 2 ^ 30
        {
            return $"{byteLength / 1048576f:F2} MB";
        }
        
        return $"{byteLength / 1073741824f:F2} GB";
    }
}