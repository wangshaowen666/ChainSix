/*--------------------------------------------------------------
 * File: FrameAnimGpuView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/31 14:00:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GPU 序列帧播放组件（挂到角色/怪 prefab 上，配 MeshRenderer 或 SpriteRenderer）：
/// - 持有多动画 clip（同一 FrameAnimAsset 内按名切换），切换 = 换 sharedMesh + sharedMaterial
///   （不同动画联合包围盒可能不同，mesh 随材质一起换）
/// - 逐实例差异（起播第一帧/相位/变速/闪白/染色）默认走"每实例每 clip 材质克隆"（_perInstance=true），
///   克隆随怪池化复用（OnDisable 自动归还 FrameAnimGpuMgr），同 shader 多材质不破 SRP 合批
/// - _perInstance=false 时直接用共享 clip 材质（合批最优但无逐实例属性，仅适合无差异的纯表现）
/// 用法：Inspector 配好 _asset / _renderer 后 Play(clipName)；
/// 受击闪白/染色等瞬态属性在 Play 后由持有者调用 SetFlash / SetTint 设置
/// </summary>
public sealed class FrameAnimGpuView : MonoBehaviour
{
    [SerializeField] private FrameAnimAsset _asset;
    [SerializeField] private Renderer _renderer;

    [Tooltip("true=逐实例材质克隆（支持起播帧/相位/闪白/变速/染色，怪物走此路）；false=共享 clip 材质（无逐实例属性）")]
    [SerializeField] private bool _perInstance = true;

    [SerializeField] private string _defaultClipNm;

    /// <summary>本实例各动画的材质克隆（随怪池化，Release 时统一归还）</summary>
    private readonly Dictionary<string, (FrameAnimGpuClip Clip, Material Mat)> _clones = new();

    private FrameAnimGpuClip _curClip;
    private string _curClipName;
    private float _fps = 1f;
    private float _speed = 1f;

    /// <summary>每次激活重播默认动画（Start 只跑一次，池化复用后不会触发；OnDisable 的 Release 已清材质/状态，激活即回到默认动画）</summary>
    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(_defaultClipNm))
        {
            Play(_defaultClipNm);
        }
    }

    /// <summary>运行时挂载用：指定资产与渲染器（也可直接 Inspector 配置）</summary>
    public void Init(FrameAnimAsset asset, Renderer renderer)
    {
        _asset = asset;
        _renderer = renderer;
    }

    /// <summary>播放动画（从头播，重置进度）。找不到动画返回 false</summary>
    public bool Play(string clipName)
    {
        if (_renderer == null)
        {
            _renderer = GetComponent<Renderer>();
        }

        if (_asset == null || _renderer == null)
        {
            Log.Error("FrameAnimGpuView 未配置资产或渲染器，无法播放:", clipName);
            return false;
        }

        var clipData = _asset.GetClip(clipName);
        if (clipData == null || clipData.sprites == null || clipData.sprites.Count == 0)
        {
            Log.Warning("FrameAnimGpuView 播放失败, 找不到动画:", clipName, " 资源:", _asset.name);
            return false;
        }

        var gpu = GameMgr.FrameAnimGpu.GetClip(_asset, clipName);
        if (gpu == null)
        {
            return false;
        }

        // 换网格 + 材质（不同动画包围盒尺寸可能不同，mesh 随材质一起换）
        if (_renderer is MeshRenderer mr)
        {
            var mf = _renderer.GetComponent<MeshFilter>();
            if (mf == null)
            {
                Log.Error("FrameAnimGpuView 的 MeshRenderer 缺少 MeshFilter，无法播放:", clipName);
                return false;
            }

            mf.sharedMesh = gpu.QuadMesh;
            mr.sharedMaterial = ResolveMaterial(clipName, gpu);
        }
        else if (_renderer is SpriteRenderer sr)
        {
            sr.sprite = gpu.QuadSprite;
            sr.sharedMaterial = ResolveMaterial(clipName, gpu);
        }
        else
        {
            Log.Error("FrameAnimGpuView 不支持的渲染器:", _renderer.GetType(), "（需 MeshRenderer 或 SpriteRenderer）");
            return false;
        }

        _curClip = gpu;
        _curClipName = clipName;
        _fps = clipData.duration > 0f ? clipData.sprites.Count / clipData.duration : 10f;

        if (_perInstance)
        {
            // 从帧 0 起播：FrameOffset = -T × fps × speed（GPU 帧号恰为 0）
            var mat = _clones[clipName].Mat;
            mat.SetFloat(FrameAnimGpuUtil.PropFrameOffset, -Time.time * _fps * _speed);
            mat.SetFloat(FrameAnimGpuUtil.PropPlaySpeed, _speed);
        }

        return true;
    }

    /// <summary>变速（保持当前进度不跳帧；只作用于当前动画的克隆，符合"减速只降移动动画"）</summary>
    public void SetPlaySpeed(float speed)
    {
        if (speed <= 0f)
        {
            return;
        }

        var mat = CurrentMaterial();
        if (mat == null)
        {
            return;
        }

        // 变速防相位跳变：新offset = 旧offset + T × fps × (旧速度 − 新速度)
        float now = Time.time;
        mat.SetFloat(FrameAnimGpuUtil.PropFrameOffset,
            mat.GetFloat(FrameAnimGpuUtil.PropFrameOffset) + now * _fps * (_speed - speed));
        mat.SetFloat(FrameAnimGpuUtil.PropPlaySpeed, speed);
        _speed = speed;
    }

    /// <summary>受击闪白（0~1，命中期间由持有者逐帧衰减）</summary>
    public void SetFlash(float amount)
    {
        var mat = CurrentMaterial();
        if (mat != null)
        {
            mat.SetFloat(FrameAnimGpuUtil.PropFlashAmount, amount);
        }
    }

    /// <summary>染色（整怪染色，如队伍色）</summary>
    public void SetTint(Color color)
    {
        var mat = CurrentMaterial();
        if (mat != null)
        {
            mat.SetColor(FrameAnimGpuUtil.PropBaseColor, color);
        }
    }

    /// <summary>当前动画名（未播放为 null）</summary>
    public string CurrentClipName => _curClipName;

    /// <summary>
    /// 归还材质克隆到池（随怪池化）。实例销毁/失活时自动触发（OnDisable 兜底），
    /// 若在 OnDisable 之前主动释放也可显式调用（幂等）
    /// </summary>
    public void Release()
    {
        foreach (var kv in _clones)
        {
            if (kv.Value.Mat != null)
            {
                GameMgr.FrameAnimGpu.RecycleInstanceMaterial(kv.Value.Clip, kv.Value.Mat);
            }
        }
        _clones.Clear();
        _curClip = null;
        _curClipName = null;
        _speed = 1f;

        // 防止池化复用后未 Play 就显示他人克隆
        if (_renderer != null)
        {
            _renderer.sharedMaterial = null;
        }
    }

    private void OnDisable()
    {
        Release();
    }

    /// <summary>取材质：逐实例模式 = 该 clip 的克隆（惰性创建并缓存），共享模式 = clip 共享材质</summary>
    private Material ResolveMaterial(string clipName, FrameAnimGpuClip clip)
    {
        if (!_perInstance)
        {
            return clip.Material;
        }

        if (!_clones.TryGetValue(clipName, out var entry))
        {
            var mat = GameMgr.FrameAnimGpu.GetInstanceMaterial(clip);
            _clones[clipName] = (clip, mat);
            return mat;
        }

        return entry.Mat;
    }

    /// <summary>当前生效材质（克隆或共享）；未播放 / 共享模式不支持逐实例属性时返回 null</summary>
    private Material CurrentMaterial()
    {
        if (_curClip == null || _curClipName == null)
        {
            return null;
        }

        if (!_perInstance)
        {
            Log.Warning("FrameAnimGpuView 共享材质模式不支持逐实例属性（SetPlaySpeed/SetFlash/SetTint），请开启 _perInstance");
            return null;
        }

        return _clones.TryGetValue(_curClipName, out var entry) ? entry.Mat : null;
    }
}
