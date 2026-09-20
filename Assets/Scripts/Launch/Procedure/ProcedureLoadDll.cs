/*--------------------------------------------------------------
 * File: ProcedureLoadDll.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/05 14:10:17 
 *--------------------------------------------------------------
 */

using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ProcedureLoadDll : ProcedureBase
{
    public override void OnEnter()
    {
        base.OnEnter();
#if !UNITY_EDITOR
        AsyncRun().Forget();
#else
        // Editor下无需加载，直接查找获得HotUpdate程序集
        Assembly hotEntryAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == LaunchConfig.HotEntryDll);
        StartHotUpdate(hotEntryAss);
#endif
    }

#if !UNITY_EDITOR
    private async UniTaskVoid AsyncRun()
    {
        try
        {
            // 补充AOT元数据：必须在加载热更程序集之前完成（官方要求的时序）。
            // 放在AOT侧执行——这里的 await handle 泛型实例化在AOT编译时已生成，不存在鸡生蛋问题
            await ReplenishMeta();

            // 这里单独实现的脚本加载没走资源管理系统，因为资源管理本身属于热更脚本
            // 注意：微信小游戏(WebGL)下不能用 WaitForCompletion，JS桥回调无法在阻塞的主线程上执行，会永久卡死
            Assembly hotEntryAss = null;
            foreach (var dll in LaunchConfig.HotfixDll)
            {
                var ass = Assembly.Load(await LoadDllBytes(dll));
                if (dll.StartsWith(LaunchConfig.HotEntryDll))
                    hotEntryAss = ass;
            }
            StartHotUpdate(hotEntryAss);
        }
        catch (Exception e)
        {
            // 反射调用会包一层 TargetInvocationException，剥开打出真实异常
            var inner = e;
            while (inner is TargetInvocationException && inner.InnerException != null)
                inner = inner.InnerException;
            Log.Error("加载热更DLL出错:", inner.GetType().Name, inner.Message, "\n", inner.StackTrace);
        }
    }

    /// <summary>
    /// 补充AOT元数据（从热更代码挪到AOT侧：保证时序在热更程序集加载之前）
    /// 注意：数组 foreach 不需要泛型枚举器；await handle 模式在AOT侧实例化已存在
    /// </summary>
    private async UniTask ReplenishMeta()
    {
        string[] aotDllList =
        {
            "mscorlib.dll",
            "UniTask.dll",
        };

        foreach (var aotDllName in aotDllList)
        {
            var asset = await CoreMgr.Res.LoadAsync<TextAsset>(aotDllName);
            RuntimeApi.LoadMetadataForAOTAssembly(asset.bytes, HomologousImageMode.SuperSet);
        }
    }
#endif

    private void StartHotUpdate(Assembly hotEntryAss)
    {
        if (hotEntryAss == null)
        {
            Log.Error($"热更程序集{LaunchConfig.HotEntryDll} 找不到");
            return;
        }

        Type type = hotEntryAss.GetType(LaunchConfig.HotEntryClass);
        if (type == null)
        {
            Log.Error("未找到启动类 ", LaunchConfig.HotEntryClass);
            return;
        }
        type.GetMethod(LaunchConfig.HotEntryMethod)?.Invoke(null, null);
    }

    private async UniTask<byte[]> LoadDllBytes(string key)
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(key);
        await handle;
        byte[] bytes = handle.Result.bytes;
        Addressables.Release(handle);
        return bytes;
    }
}