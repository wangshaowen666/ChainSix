/*--------------------------------------------------------------
 * File: FrameAnimGpuMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/31 14:00:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU 序列帧动画管理器（配合 FrameAnimGpuView 使用）：
/// - Clip 缓存：同一 (资产, 动画名) 全实例共享一个 FrameAnimGpuClip（共享材质保 SRP 合批），
///   由本管理器统一构建与销毁，避免各实例重复烘焙与派生资源泄漏
/// - 材质克隆池：逐实例差异（起播第一帧/相位/闪白/变速/染色）走"每实例每 clip 材质克隆"，
///   克隆随怪池化复用；同 shader 多材质不涨 SetPass（SRP Batcher 按 shader 合批，
///   实测见《序列帧渲染方案性能基线对比与定案》第六节）
/// 接入：GameMgr.FrameAnimGpu，由 GameMgr 统一 OnInit / OnSceneExit / OnGameRestart 生命周期
/// </summary>
public sealed class FrameAnimGpuMgr : ManagerBase
{
    /// <summary>clip 缓存：key = (资产引用, 动画名)，引用键避免 Addressable 卸载后实例 ID 复用串数据</summary>
    private readonly Dictionary<(FrameAnimAsset, string), FrameAnimGpuClip> _clips = new();

    /// <summary>材质克隆池：key = 基础材质（clip 的共享材质），value = 可复用克隆栈（纹理为共享引用，仅标量属性独立）</summary>
    private readonly Dictionary<Material, Stack<Material>> _materialPool = new();

    /// <summary>
    /// 获取（或首次构建并缓存）指定动画的 GPU clip。全实例共享，勿直接 Destroy；
    /// 可在加载期预热，避免战斗中首次 Play 才烘焙造成卡顿
    /// </summary>
    public FrameAnimGpuClip GetClip(FrameAnimAsset asset, string clipName)
    {
        if (asset == null || string.IsNullOrEmpty(clipName))
        {
            return null;
        }

        if (_clips.TryGetValue((asset, clipName), out var cached))
        {
            return cached;
        }

        var clip = asset.GetClip(clipName);
        if (clip == null || clip.sprites == null || clip.sprites.Count == 0)
        {
            Log.Warning("FrameAnimGpuMgr 找不到动画:", clipName, " 资源:", asset.name);
            return null;
        }

        float fps = clip.duration > 0f ? clip.sprites.Count / clip.duration : 10f;
        var gpu = FrameAnimGpuUtil.CreateClip(clip.sprites, fps, clip.loop);
        if (gpu == null)
        {
            return null;
        }

        _clips.Add((asset, clipName), gpu);
        return gpu;
    }

    /// <summary>取逐实例材质克隆（优先复用池中克隆，无则 new）。实例销毁/失活时须 RecycleInstanceMaterial 归还</summary>
    public Material GetInstanceMaterial(FrameAnimGpuClip clip)
    {
        if (clip == null || clip.Material == null)
        {
            return null;
        }

        if (_materialPool.TryGetValue(clip.Material, out var stack) && stack.Count > 0)
        {
            return stack.Pop();
        }

        var mat = new Material(clip.Material) { name = clip.Material.name + " (Clone)" };
        return mat;
    }

    /// <summary>归还逐实例材质克隆到池（幂等；以基础材质为桶，同 clip 克隆循环复用）</summary>
    public void RecycleInstanceMaterial(FrameAnimGpuClip clip, Material mat)
    {
        if (clip == null || clip.Material == null || mat == null)
        {
            return;
        }

        if (!_materialPool.TryGetValue(clip.Material, out var stack))
        {
            stack = new Stack<Material>();
            _materialPool.Add(clip.Material, stack);
        }

        stack.Push(mat);
    }

    public override void OnSceneExit()
    {
        ClearAll();
    }

    public override void OnGameRestart()
    {
        ClearAll();
    }

    /// <summary>统一销毁全部 clip 派生资源与克隆材质（切场景/重启时调用，全实例共享所以一次清）</summary>
    private void ClearAll()
    {
        foreach (var kv in _clips)
        {
            var gpu = kv.Value;
            if (gpu.Material != null) Object.Destroy(gpu.Material);
            if (gpu.QuadSprite != null) Object.Destroy(gpu.QuadSprite);
            if (gpu.FrameData != null) Object.Destroy(gpu.FrameData);
            if (gpu.QuadMesh != null) Object.Destroy(gpu.QuadMesh);
        }
        _clips.Clear();

        foreach (var stack in _materialPool.Values)
        {
            foreach (var mat in stack)
            {
                if (mat != null) Object.Destroy(mat);
            }
        }
        _materialPool.Clear();
    }
}
