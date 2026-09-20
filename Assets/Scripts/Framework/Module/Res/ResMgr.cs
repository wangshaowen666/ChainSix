/*--------------------------------------------------------------
 * File: ResMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/09/01 10:55:17 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

public class ResMgr : ManagerBase
{
    private IResLoader _resLoader;

    public override void OnInit()
    {
        base.OnInit();
        _resLoader = new AddressableLoader();
    }

    public UniTask<T> LoadAsync<T>(string key)
        => _resLoader.LoadAsync<T>(key);

    public UniTask<IList<T>> PreloadWithLabel<T>(string label)
        => _resLoader.PreloadWithLabel<T>(label);

    public UniTask LoadSceneAsync(string sceneName, Action<float> onProgress = null)
        => _resLoader.LoadSceneAsync(sceneName, onProgress);

    public void Unload(string key) => _resLoader.Unload(key);

    public void UnloadAll() => _resLoader.UnloadAll();

#if STATS_ON && UNITY_EDITOR
    public List<string> DealPoolStats() => _resLoader.DealPoolStats();
#endif
}