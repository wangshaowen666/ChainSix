/*--------------------------------------------------------------
 * File: FrameAnimGpuUtil.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/27 11:15:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// GPU 动画剪辑数据（FrameAnimGpuUtil.CreateClip 的产出，调用方负责生命周期）：
/// 同一动画的全部实例共用同一个 Clip（共享 Material 保合批），释放时统一 Destroy 三个对象
/// </summary>
public sealed class FrameAnimGpuClip
{
    /// <summary>动画材质（同动画实例必须共享同一实例）</summary>
    public Material Material;

    /// <summary>联合包围盒 Sprite（赋给 SpriteRenderer.sprite，尺寸 = 全帧联合包围盒）</summary>
    public Sprite QuadSprite;

    /// <summary>帧矩形数据纹理（RGBAFloat，帧数×2：行0=四边形区域 / 行1=图集区域）</summary>
    public Texture2D FrameData;

    /// <summary>
    /// 联合包围盒 quad 网格（MeshRenderer 变体用，顶点/uv 与 QuadSprite 完全一致）：
    /// Unity 2022 的 SpriteRenderer 不走 SRP Batcher（2D 渲染器 2023.1 才支持），
    /// MeshRenderer + 本 shader（UnityPerMaterial）可走 SRP Batcher，也为 6-2 的实例化铺路
    /// </summary>
    public Mesh QuadMesh;
}

/// <summary>
/// GPU 序列帧动画工具（配套 shader：Game/FrameAnimGpu，见 Assets/Res/Common/Shader/FrameAnimGpu.shader）：
/// - CreateClip：从帧 Sprite 列表烘焙 GPU 播放数据，支持不规则尺寸/任意 pivot 的紧致排布大图
///   （与 CPU 方案同源 -- 直接传 FrameAnimAsset.Clip.sprites 即可做同条件 A/B 对比）
/// - 四边形 = 全部帧的联合包围盒（按各自 pivot 对齐），小于盒的帧以透明边距呈现，不拉伸变形
/// - 逐实例属性（相位偏移 / 手动帧）走 MaterialPropertyBlock，注意会破坏 SRP Batcher
/// </summary>
public static class FrameAnimGpuUtil
{
    /// <summary>shader 路径（Shader.Find 按名字查找，编辑器可直接找到）</summary>
    public const string ShaderName = "Game/FrameAnimGpu";

    /// <summary>
    /// shader 的 Addressable 地址（Remote_Common 组，地址不含扩展名）：
    /// 真机必须在进战斗前调用 <see cref="PreloadShadersAsync"/> 按此地址显式加载（加载后的实例缓存在本类，运行时不依赖 Shader.Find）
    /// </summary>
    public const string ShaderAddress = "FrameAnimGpu";

    /// <summary>GPU Instancing 版 shader 路径（逐实例属性变体，配套 Graphics.RenderMeshInstanced）</summary>
    public const string ShaderNameInstanced = "Game/FrameAnimGpuInst";

    /// <summary>GPU Instancing 版 shader 的 Addressable 地址（需在 Remote_Common 组配置同名条目）</summary>
    public const string ShaderAddressInstanced = "FrameAnimGpuInst";

    public static readonly int PropBaseMap = Shader.PropertyToID("_BaseMap");
    public static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    public static readonly int PropFrameData = Shader.PropertyToID("_FrameData");
    public static readonly int PropFrameCount = Shader.PropertyToID("_FrameCount");
    public static readonly int PropFps = Shader.PropertyToID("_Fps");
    public static readonly int PropLoop = Shader.PropertyToID("_Loop");
    public static readonly int PropPlaySpeed = Shader.PropertyToID("_PlaySpeed");
    public static readonly int PropStartFrame = Shader.PropertyToID("_StartFrame");
    public static readonly int PropFrameOffset = Shader.PropertyToID("_FrameOffset");
    public static readonly int PropFlashAmount = Shader.PropertyToID("_FlashAmount"); // 闪白强度：instancing 版逐实例 / SRP 版逐材质克隆
    public static readonly int PropTimeDriven = Shader.PropertyToID("_TimeDriven");
    public static readonly int PropFrameIndex = Shader.PropertyToID("_FrameIndex");

    /// <summary>
    /// 预载 GPU 动画 shader（必须在进战斗前的异步阶段调用，如 ProcedurePreload）：
    /// Shader.Find 只能命中"已加载"的 shader，Addressable 组里"包含"只代表"可被加载"，
    /// bundle 不加载时 shader 不在内存；而 new Material(字符串) 的字符串引用不构成 Addressable 依赖，
    /// 没有材质资产引用该 shader，故永远无人触发它加载 —— 真机必须显式加载一次，加载后常驻（勿 Unload）。
    /// 注意：微信小游戏 wasm 环境下 Shader.Find 对 bundle 加载的 shader 不可靠，
    /// 因此加载后的实例缓存在此处，运行时经 <see cref="FindShader"/> 直接取实例，不走按名查找
    /// </summary>
    public static async UniTask PreloadShadersAsync()
    {
        _loadedShader = await CoreMgr.Res.LoadAsync<Shader>(ShaderAddress);
        _loadedShaderInstanced = await CoreMgr.Res.LoadAsync<Shader>(ShaderAddressInstanced);

        if (_loadedShader == null)
            Log.Error($"FrameAnimGpuUtil 预载 shader 失败: {ShaderAddress}");
        if (_loadedShaderInstanced == null)
            Log.Error($"FrameAnimGpuUtil 预载 instanced shader 失败: {ShaderAddressInstanced}");
    }

    private static Shader _loadedShader;
    private static Shader _loadedShaderInstanced;

    /// <summary>
    /// 获取 shader：优先取 PreloadShadersAsync 缓存的实例；仅编辑器/兜底场景走 Shader.Find
    /// </summary>
    /// <param name="instanced">true = GPU Instancing 版 shader（Game/FrameAnimGpuInst，逐实例属性变体）</param>
    public static Shader FindShader(bool instanced = false)
    {
        var shader = instanced ? _loadedShaderInstanced : _loadedShader;
        if (shader != null)
            return shader;

        string name = instanced ? ShaderNameInstanced : ShaderName;
        shader = Shader.Find(name);
        if (shader == null)
        {
            Log.Error("FrameAnimGpuUtil 找不到 shader:", name,
                "（真机需先调用 PreloadShadersAsync 经 Addressable 显式加载；或进 Graphics 设置 Always Included Shaders）");
        }

        return shader;
    }

    /// <summary>
    /// 从帧 Sprite 列表烘焙 GPU 播放数据（不规则尺寸/任意 pivot 均支持；均匀网格是其特例）
    /// </summary>
    /// <param name="sprites">帧序列（须来自同一张大图；与 FrameAnimAsset.Clip.sprites 同源即可）</param>
    /// <param name="fps">帧率</param>
    /// <param name="loop">是否循环</param>
    /// <param name="pixelsPerUnit">与普通 Sprite 一致（100 = 1 像素 0.01 世界单位）</param>
    /// <param name="instanced">true = 用 GPU Instancing 版 shader 建材质（配套 Graphics.RenderMeshInstanced）</param>
    public static FrameAnimGpuClip CreateClip(IReadOnlyList<Sprite> sprites, float fps, bool loop = true, float pixelsPerUnit = 100f, bool instanced = false)
    {
        if (sprites == null || sprites.Count == 0)
        {
            Log.Error("FrameAnimGpuUtil.CreateClip 帧列表为空");
            return null;
        }

        var shader = FindShader(instanced);
        if (shader == null)
        {
            return null;
        }

        var atlas = sprites[0].texture;
        for (int i = 1; i < sprites.Count; i++)
        {
            if (sprites[i] == null || sprites[i].texture != atlas)
            {
                Log.Error("FrameAnimGpuUtil.CreateClip 帧不在同一张大图上，无法构建 GPU 剪辑（与换帧合批要求一致）");
                return null;
            }
        }

        float texW = atlas.width;
        float texH = atlas.height;
        int count = sprites.Count;

        // 1) 联合包围盒：帧 i 的局部四边形相对 pivot 原点横跨 [-pivot.x, rect.width - pivot.x] × [-pivot.y, rect.height - pivot.y]
        //    （Sprite.pivot 单位为像素，自 rect 左下角起算）
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            var r = sprites[i].textureRect;
            var p = sprites[i].pivot;
            minX = Mathf.Min(minX, -p.x);
            minY = Mathf.Min(minY, -p.y);
            maxX = Mathf.Max(maxX, r.width - p.x);
            maxY = Mathf.Max(maxY, r.height - p.y);
        }

        float boxW = maxX - minX;
        float boxH = maxY - minY;

        // 2) 帧矩形数据纹理：行0 = 该帧在四边形上的区域，行1 = 该帧在图集里的区域（均换算到图集 uv）
        //    必须是 RGBAFloat（uv 精度）+ Point 过滤 + Clamp 寻址（纹素中心定位）
        var data = new Texture2D(count, 2, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        data.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[count * 2];
        for (int i = 0; i < count; i++)
        {
            var r = sprites[i].textureRect;
            var p = sprites[i].pivot;

            // 该帧区域在联合包围盒内的位置（自盒左下角起算）：与各帧 pivot 对齐
            float offsetX = -p.x - minX;
            float offsetY = -p.y - minY;

            // 行0：四边形上的区域（四边形 Sprite 的 rect 在图集原点 [0, boxW]×[0, boxH]，四边形 uv 即该区域图集 uv）
            pixels[i] = new Color(offsetX / texW, offsetY / texH, r.width / texW, r.height / texH);
            // 行1：图集里的区域
            pixels[count + i] = new Color(r.x / texW, r.y / texH, r.width / texW, r.height / texH);
        }

        data.SetPixels(pixels);
        data.Apply();

        // 3) 四边形 Sprite：取图集原点 [0, boxW]×[0, boxH] 区域（只取其尺寸与 uv 跨度，内容由 shader 重映射），
        //    pivot 使 transform 原点落在联合包围盒的 (-minX, -minY) 处，与 CPU 方案各帧 pivot 语义对齐
        //    必须显式 FullRect：Sprite.Create 默认 Tight 网格，会按该区域内的不透明像素裁剪网格，
        //    而该区域内是其它帧的杂散内容 -> 四边形被裁成碎片，表现为每帧画面显示不完整；
        //    FullRect 同时免去 Tight 网格对贴图可读（Read/Write）的要求
        var quadSprite = Sprite.Create(atlas, new Rect(0f, 0f, boxW, boxH), new Vector2(-minX / boxW, -minY / boxH),
            pixelsPerUnit, 0, SpriteMeshType.FullRect);

        // 4) 动画材质（instancing 版须开 enableInstancing，否则 RenderMeshInstanced 不生效）
        var mat = new Material(shader);
        mat.enableInstancing = instanced;
        mat.SetTexture(PropBaseMap, atlas);
        mat.SetTexture(PropFrameData, data);
        mat.SetFloat(PropFrameCount, count);
        mat.SetFloat(PropFps, fps);
        mat.SetFloat(PropLoop, loop ? 1f : 0f);

        // 5) MeshRenderer 变体用 quad 网格：顶点 = 联合包围盒范围（pivot 原点局部空间），uv 与 QuadSprite 完全一致
        //    （shader Cull Off，绕序不影响显示）
        var mesh = new Mesh { name = "FrameAnimGpuQuad" };
        float inv = 1f / pixelsPerUnit;
        mesh.vertices = new[]
        {
            new Vector3(minX * inv, minY * inv, 0f),
            new Vector3(maxX * inv, minY * inv, 0f),
            new Vector3(minX * inv, maxY * inv, 0f),
            new Vector3(maxX * inv, maxY * inv, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(boxW / texW, 0f),
            new Vector2(0f, boxH / texH),
            new Vector2(boxW / texW, boxH / texH),
        };
        mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        mesh.RecalculateBounds();

        return new FrameAnimGpuClip { Material = mat, QuadSprite = quadSprite, FrameData = data, QuadMesh = mesh };
    }
}
