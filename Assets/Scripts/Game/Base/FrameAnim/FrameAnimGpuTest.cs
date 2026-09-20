/*--------------------------------------------------------------
 * File: FrameAnimGpuTest.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/27 15:30:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// GPU 序列帧性能基线测试（阶段 1-1，与 CPU 换帧方案 FrameAnimTest 对照）：
/// 素材来源二选一（_assets 配置真实资产优先）：
/// - 真实资产：每个 FrameAnimAsset 的全部 clip 各建一套 GPU 剪辑，实例轮转分配（多套序列帧混场压测）
/// - 程序化生成（_assets 留空）：同一张大图生成"不规则尺寸"帧（紧致裁切、38~60px），CPU/GPU 两方案完全同源
/// 池化 GameObject 每帧位移（绕基础点公转），_mode 在 Inspector 里选定后启动即生效（一次只测一种条件，互不切换）：
/// - GpuTimeDriven（GPU 时间驱动）：材质 _Time 自动换帧，CPU 每帧零动画成本，本方案核心指标
///   （实测：Forward Renderer 下 SpriteRenderer 也能进 SRP Batch，SetPass 个位数；
///    多套材质共用同一 shader 仍在同一 SRP batch 内，SetPass 不随动画套数上涨）
/// - GpuMpbOffset（GPU + MPB 相位偏移）：MaterialPropertyBlock 给每个实例不同相位（测 MPB 对合批的影响）
/// - CpuSwap（CPU 换帧）：FrameAnimMgr 换 Sprite 引用（现有方案同条件压测；需 SpriteRenderer 形态）
/// - CpuManualFrame（CPU 手动帧）：CPU 算帧号逐帧 MPB 喂给 shader（测"shader uv + CPU 控制"混合方案成本）
/// - StaticFrame（静帧）：不播动画，隔离"N 个物体每帧位移"本身的纯基线
/// - GpuInstancing（GPU 实例化）：不走 GameObject 池，Graphics.DrawMeshInstanced 直接提交（_useMesh 对本模式无效），
///   逐实例相位错开/闪白走 MPB 的 SetFloatArray 数组进 instanced buffer（一个 draw 提交一整批），
///   与 GpuMpbOffset（MPB 逐实例提交、踢出 SRP 合批）对照；shader 用 Game/FrameAnimGpuInst（与 SRP Batcher 互斥）
/// 渲染器类型由 _useMesh 决定（与 _mode 同为启动配置；Unity 限制 SpriteRenderer / MeshFilter 不能同 GameObject 共存）：
/// - SpriteRenderer（默认）：实测与 MeshRenderer 合批表现一致，无需为合批改 Mesh
/// - MeshRenderer：同一 shader/网格数据，仅在将来走 RenderMeshInstanced 实例化（任务 6-2）时有意义；不可配 CpuSwap
/// 用法：Inspector 配好模式/数量/渲染器后直接 Play；布局按实例数与屏幕宽高比动态算行列，
/// 以屏幕中心为原点向四周扩展（间距 _spacing 调密集度），改配置需重新进 Play。
/// 注意：CpuSwap 需从正常游戏流程启动（GameMgr.Init 注册 FrameAnimMgr 后动画才会推进），裸场景会停在帧 0。
/// </summary>
public class FrameAnimGpuTest : MonoBehaviour
{
    /// <summary>测试模式（启动即生效，改配置需重新进 Play，保证每次测试条件单一）</summary>
    public enum AnimMode
    {
        GpuTimeDriven = 0,  // GPU 时间驱动：材质 _Time 自动换帧，零 CPU 动画成本
        GpuMpbOffset = 1,   // GPU 时间驱动 + MPB 相位偏移：测 MPB 对 SRP 合批的影响
        CpuSwap = 2,        // CPU 换帧：FrameAnimMgr 换 Sprite 引用（现有方案，需 SpriteRenderer）
        CpuManualFrame = 3, // CPU 手动帧：CPU 算帧号逐帧 MPB 喂 shader
        StaticFrame = 4,    // 静帧：不播动画，隔离纯位移基线
        GpuInstancing = 5,  // GPU 实例化：DrawMeshInstanced 批量提交 + 逐实例相位/闪白（instanced buffer）
    }

    private const int FrameCount = 8;     // 程序化模式帧数（尺寸 38~60px 不等，模拟紧致裁切）
    private const float Fps = 10f;        // 程序化模式帧率（CPU 测试 walk：8 帧 / 0.8s = 10fps）
    private const int SheetW = 512;       // 程序化大图宽
    private const int SheetH = 128;       // 程序化大图高
    private const int FrameGap = 6;       // 帧间距（模拟排布留白，同时抑制双线性串色）
    private const int FrameMargin = 4;    // 帧矩形内透明边距
    private const float OrbitRadius = 0.25f;
    private const float OrbitSpeed = 2f;
    private const float PlaySpeed = 1f;   // 播放速度（固定 1，测试不引入运行时变量）
    private const int InstBatchSize = 1000; // GPU Instancing 单次 draw 实例上限（引擎 instanced buffer 上限 1023，留余量取整）

    /// <summary>IMGUI 字体大小（高分屏 1440*3200 下需放大）</summary>
    private const int GuiFontSize = 72;

    [SerializeField]
    [Tooltip("测试模式：启动即生效（改后需重新进 Play），一次只测一种条件")]
    private AnimMode _mode = AnimMode.GpuTimeDriven;

    [SerializeField]
    [Tooltip("实例数量：启动即生效；布局按数量与屏幕宽高比从中心向四周扩展，密集堆叠测试需保证相机视野覆盖网格（Ortho Size 按需调大）")]
    private int _count = 1000;

    [SerializeField]
    [Tooltip("实例网格间距：越小越密集、互相堆叠（测 overdraw/深度/合批用）；帧世界尺寸约 0.4~0.6，小于该值即重叠；改后需重新进 Play")]
    private float _spacing = 0.35f;

    [SerializeField]
    [Tooltip("真实动画资产（FrameAnimAsset）：每个资产的全部 clip 各建一套 GPU 剪辑并轮转分配给实例；留空则程序化生成测试帧")]
    private List<FrameAnimAsset> _assets;

    [SerializeField]
    [Tooltip("渲染器类型：勾选=MeshFilter+MeshRenderer，不勾=SpriteRenderer；Awake 时生效，修改后需重新进 Play")]
    private bool _useMesh;

    [SerializeField]
    [Tooltip("显示 IMGUI 统计面板（测性能时保持关闭，避免 IMGUI 开销污染数据）")]
    private bool _showGui;

    /// <summary>可播放动画单元：资产 + clip + GPU 剪辑（多套序列帧轮转分配给实例）</summary>
    private struct AnimEntry
    {
        public FrameAnimAsset Asset;     // CPU 换帧用（CreatePlayer + Play 按名）
        public FrameAnimAsset.Clip Clip; // 帧数据（帧数 / 时长 / 循环）
        public FrameAnimGpuClip Gpu;     // GPU 剪辑（材质 / 包围盒 Sprite / 帧矩形纹理 / 网格）
        public float Fps;                // clip 帧率 = 帧数 / 时长
    }

    private readonly List<AnimEntry> _anims = new();
    private readonly List<int> _animIndices = new(); // 每实例使用的动画下标（与 _renderers 对齐）
    private readonly List<Renderer> _renderers = new(); // 每实例渲染器（SpriteRenderer 或 MeshRenderer，由 _useMesh 决定）
    private readonly List<Vector2> _basePos = new();
    private readonly List<float> _phase = new();
    private MaterialPropertyBlock _mpb;

    private Texture2D _sheet;                          // 程序化模式大图（真实资产模式为 null）
    private readonly List<Sprite> _frames = new();     // 程序化模式帧列表（真实资产模式为空）
    private FrameAnimAsset _cpuAsset;                  // 程序化模式 CPU 动画配置（真实资产模式为 null）
    private Material _defaultMat;                      // SpriteRenderer 默认材质（CPU 换帧模式用）
    private readonly List<FrameAnimPlayer> _cpuPlayers = new(); // 与 _renderers 下标对齐，null = 未创建

    private int _activeCount;
    private float _cpuTime; // CPU 手动帧模式动画时间（累计制）

    /// <summary>GPU Instancing 分组：每套动画一组（同材质同网格才能合成一个 instanced draw）</summary>
    private sealed class InstGroup
    {
        public Material Material;                                // 该组材质（DrawMeshInstanced 直接传）
        public Mesh Mesh;                                         // 该组四边形网格
        public int FrameCount;                                    // 该组动画帧数（相位取模用）
        public readonly List<int> Indices = new();                // 组内实例的全局下标（对应 _basePos/_phase）
        public readonly List<Matrix4x4[]> MatBatches = new();     // 按批切分的实例矩阵（每帧更新）
        public readonly List<MaterialPropertyBlock> MpbBatches = new(); // 按批的逐实例属性块（SetFloatArray，仅启动设置）
    }

    private readonly List<InstGroup> _instGroups = new(); // GpuInstancing 模式的分组渲染数据（替代 GameObject 池）

    // 渲染统计（Unity.Profiling.ProfilerRecorder 在真机 Release 包也收集，
    // 替代仅编辑器可用的 UnityStats；配 _showGui 显示）
    private ProfilerRecorder _recDrawCalls;
    private ProfilerRecorder _recBatches;
    private ProfilerRecorder _recSetPass;

    // 性能统计（1 秒窗口）
    private int _statFrames;
    private float _statTime;
    private float _statWorst;
    private float _fps = 60f;
    private float _avgMs = 16.7f;
    private float _worstMs = 16.7f;

    private void Awake()
    {
        if (_mode == AnimMode.CpuSwap && _useMesh)
        {
            Log.Error("FrameAnimGpuTest 配置冲突：CPU 换帧模式需要 SpriteRenderer（_useMesh 请关闭）");
            enabled = false;
            return;
        }

        // GpuInstancing 用 instancing 版 shader 建材质（逐实例属性变体）；该模式下 _useMesh 无意义（不走 GameObject 渲染器）
        bool instanced = _mode == AnimMode.GpuInstancing;

        if (_assets != null && _assets.Count > 0)
        {
            BuildRealAnims(instanced);
        }
        else
        {
            BuildProceduralAnim(instanced);
        }

        if (_anims.Count == 0)
        {
            // shader 未进包 / 资产无有效 clip 等：停用组件避免后续空引用刷屏（CreateClip 内已打错误日志）
            enabled = false;
            return;
        }

        _mpb = new MaterialPropertyBlock();
        int count = Mathf.Clamp(_count, 0, 5000);

        if (instanced)
        {
            // instancing 路径：不建 GameObject，布局数据 + 分批缓冲直接交 DrawMeshInstanced
            BuildInstancedGroups(count);
            _activeCount = count;
        }
        else
        {
            SetActiveCount(count);
        }

        // 渲染统计采样（每帧渲染后由引擎更新，OnGUI 读取 LastValue）
        _recDrawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _recBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _recSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
    }

    private void Update()
    {
        float t = Time.time;

        // GPU Instancing：矩阵更新 + DrawMeshInstanced 提交（不走 GameObject，位移语义与其余模式一致）
        if (_mode == AnimMode.GpuInstancing)
        {
            UpdateInstanced(t);
            UpdateStats();
            return;
        }

        // 池化实例每帧位移（阶段 1-1 基线：N 个 GameObject 每帧动的纯成本见 StaticFrame 模式）
        for (int i = 0; i < _activeCount; i++)
        {
            float p = t * OrbitSpeed + _phase[i];
            var b = _basePos[i];
            _renderers[i].transform.position = new Vector3(b.x + Mathf.Cos(p) * OrbitRadius, b.y + Mathf.Sin(p) * OrbitRadius, 0f);
        }

        if (_mode == AnimMode.CpuManualFrame)
        {
            // CPU 算帧号逐帧喂 shader（_TimeDriven=0）：各动画帧率/帧数不同，MPB 每帧重设以测真实成本
            _cpuTime += Time.deltaTime * PlaySpeed;
            for (int i = 0; i < _activeCount; i++)
            {
                var anim = _anims[_animIndices[i]];
                float frame = Mathf.Repeat(Mathf.Floor(_cpuTime * anim.Fps), anim.Clip.sprites.Count);
                _mpb.SetFloat(FrameAnimGpuUtil.PropFrameIndex, frame);
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }

        UpdateStats();
    }

    private void OnDestroy()
    {
        // 释放渲染统计采样（未启动（IsRunning=false）的默认实例 Dispose 为无操作）
        if (_recDrawCalls.IsRunning) _recDrawCalls.Dispose();
        if (_recBatches.IsRunning) _recBatches.Dispose();
        if (_recSetPass.IsRunning) _recSetPass.Dispose();

        if (_cpuPlayers != null)
        {
            for (int i = 0; i < _cpuPlayers.Count; i++)
            {
                if (_cpuPlayers[i] != null)
                {
                    GameMgr.FrameAnim.ReleasePlayer(_cpuPlayers[i]);
                }
            }
        }

        // 各套动画的 GPU 资源（真实资产模式不持有贴图本体，只销毁运行时创建的派生资源）
        foreach (var anim in _anims)
        {
            if (anim.Gpu == null)
            {
                continue;
            }

            if (anim.Gpu.QuadSprite != null)
            {
                Destroy(anim.Gpu.QuadSprite);
            }

            if (anim.Gpu.Material != null)
            {
                Destroy(anim.Gpu.Material);
            }

            if (anim.Gpu.FrameData != null)
            {
                Destroy(anim.Gpu.FrameData);
            }

            if (anim.Gpu.QuadMesh != null)
            {
                Destroy(anim.Gpu.QuadMesh);
            }
        }

        // 程序化模式的资源（真实资产模式均为 null）
        if (_cpuAsset != null)
        {
            Destroy(_cpuAsset);
        }

        foreach (var s in _frames)
        {
            if (s != null)
            {
                Destroy(s);
            }
        }

        if (_sheet != null)
        {
            Destroy(_sheet);
        }
    }

    private void OnGUI()
    {
        if (!_showGui)
        {
            return;
        }

        // 放大字体与控件尺寸，适配 1440*3200 高分屏
        var skin = GUI.skin;
        skin.box.fontSize = GuiFontSize;
        skin.label.fontSize = GuiFontSize;

        var rect = new Rect(40, 40, 920, 600);
        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);

        // 渲染路径名：instancing 模式无 GameObject 渲染器，其余按配置显示
        string rendererName = _mode == AnimMode.GpuInstancing
            ? "DrawMeshInstanced"
            : (_useMesh ? "MeshRenderer" : "SpriteRenderer");
        GUILayout.Label($"[{_mode}] {rendererName}  实例: {_activeCount}  动画: {_anims.Count}套");
        GUILayout.Label($"FPS: {_fps:F0}  平均: {_avgMs:F2}ms  最差帧: {_worstMs:F2}ms");
        // 真机同样有效（ProfilerRecorder）；LastValue = 最近一帧渲染统计
        GUILayout.Label($"DrawCalls: {_recDrawCalls.LastValue}  Batches: {_recBatches.LastValue}  SetPass: {_recSetPass.LastValue}");

        GUILayout.EndArea();
    }

    /// <summary>
    /// 真实资产模式：每个资产的每个 clip 各建一套 GPU 剪辑（fps = 帧数 / 时长）
    /// </summary>
    /// <param name="instanced">true = 用 GPU Instancing 版 shader（GpuInstancing 模式）</param>
    private void BuildRealAnims(bool instanced)
    {
        foreach (var asset in _assets)
        {
            if (asset == null || asset.clips == null)
            {
                continue;
            }

            foreach (var clip in asset.clips)
            {
                if (clip?.sprites == null || clip.sprites.Count == 0)
                {
                    continue;
                }

                float fps = clip.duration > 0f ? clip.sprites.Count / clip.duration : Fps;
                var gpu = FrameAnimGpuUtil.CreateClip(clip.sprites, fps, clip.loop, 100f, instanced);
                if (gpu == null)
                {
                    continue;
                }

                _anims.Add(new AnimEntry { Asset = asset, Clip = clip, Gpu = gpu, Fps = fps });
            }
        }
    }

    /// <summary>
    /// 程序化模式：同一张大图生成"不规则尺寸"帧，CPU/GPU 两方案完全同源
    /// </summary>
    /// <param name="instanced">true = 用 GPU Instancing 版 shader（GpuInstancing 模式）</param>
    private void BuildProceduralAnim(bool instanced)
    {
        BuildFrames();
        var gpu = FrameAnimGpuUtil.CreateClip(_frames, Fps, true, 100f, instanced);
        if (gpu == null)
        {
            return;
        }

        _cpuAsset = BuildCpuAsset();
        _anims.Add(new AnimEntry { Asset = _cpuAsset, Clip = _cpuAsset.clips[0], Gpu = gpu, Fps = Fps });
    }

    /// <summary>
    /// 构建 GPU Instancing 分组与批缓冲（GpuInstancing 模式启动时一次构建，替代 GameObject 池）：
    /// - 每套动画一组（同材质同网格才可合成一个 instanced draw），实例按轮转归属（与其余模式分配一致）
    /// - 每组按 InstBatchSize 切批（引擎单次 draw 实例上限 1023），批数 = draw call 数
    /// - 逐实例属性（相位错开 / 闪白演示）走 MPB 的 SetFloatArray 数组（数组索引 = 实例索引，
    ///   DrawMeshInstanced 标准机制，引擎把数组元素展开进 instanced buffer，各实例取值不同且不破合批）
    /// </summary>
    private void BuildInstancedGroups(int count)
    {
        _instGroups.Clear();

        // 布局行列与 GameObject 模式同一套算法（保证各模式画面一致，性能对比公平）
        float aspect = Screen.width / (float)Screen.height;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
        float cx = (cols - 1) * 0.5f;
        float cy = (rows - 1) * 0.5f;

        for (int a = 0; a < _anims.Count; a++)
        {
            var anim = _anims[a];
            _instGroups.Add(new InstGroup
            {
                Material = anim.Gpu.Material,
                Mesh = anim.Gpu.QuadMesh,
                FrameCount = anim.Clip.sprites.Count,
            });
        }

        // 实例归属 + 布局数据（公转相位黄金比散布，与其余模式同源）
        for (int i = 0; i < count; i++)
        {
            _instGroups[i % _anims.Count].Indices.Add(i);

            int gx = i % cols;
            int gy = i / cols;
            _basePos.Add(new Vector2((gx - cx) * _spacing, (gy - cy) * _spacing));
            _phase.Add(i * 0.618f);
        }

        // 每组切批：矩阵缓冲（每帧更新）+ MPB（逐实例相位/闪白数组，仅此处设置一次）
        for (int g = 0; g < _instGroups.Count; g++)
        {
            var group = _instGroups[g];
            int total = group.Indices.Count;
            int batches = (total + InstBatchSize - 1) / InstBatchSize;
            for (int b = 0; b < batches; b++)
            {
                int size = Mathf.Min(InstBatchSize, total - b * InstBatchSize);
                var mats = new Matrix4x4[size];
                var offsets = new float[size];
                var flashes = new float[size];
                for (int j = 0; j < size; j++)
                {
                    int gi = group.Indices[b * InstBatchSize + j];
                    // 相位错开（帧号偏移，黄金比散布取模帧数）：不同步播放的关键，走 instanced buffer 不破合批
                    offsets[j] = (gi * 0.618f) % group.FrameCount;
                    // 闪白演示：每 7 个实例常驻强闪白，肉眼验证逐实例属性生效（实际闪白按事件动态改数组元素）
                    flashes[j] = gi % 7 == 0 ? 0.85f : 0f;
                }

                var mpb = new MaterialPropertyBlock();
                mpb.SetFloatArray(FrameAnimGpuUtil.PropFrameOffset, offsets);
                mpb.SetFloatArray(FrameAnimGpuUtil.PropFlashAmount, flashes);

                group.MatBatches.Add(mats);
                group.MpbBatches.Add(mpb);
            }
        }
    }

    /// <summary>
    /// GPU Instancing 逐帧提交：更新各批实例矩阵（公转位移，与其余模式同参数）后
    /// DrawMeshInstanced 一个 draw 提交整批（draw 命令不持久，每帧必须重新提交）。
    /// 用 DrawMeshInstanced 而非 RenderMeshInstanced：后者（Unity/团结 2022.3 系）是泛型 instanceData
    /// 签名、无 MPB 参数，传不了逐实例 shader 属性；前者的 MPB SetFloatArray 数组是逐实例属性标准机制
    /// </summary>
    private void UpdateInstanced(float t)
    {
        for (int g = 0; g < _instGroups.Count; g++)
        {
            var group = _instGroups[g];
            for (int b = 0; b < group.MatBatches.Count; b++)
            {
                var mats = group.MatBatches[b];
                int baseIdx = b * InstBatchSize;
                for (int j = 0; j < mats.Length; j++)
                {
                    int gi = group.Indices[baseIdx + j];
                    float p = t * OrbitSpeed + _phase[gi];
                    var bp = _basePos[gi];
                    var pos = new Vector3(bp.x + Mathf.Cos(p) * OrbitRadius, bp.y + Mathf.Sin(p) * OrbitRadius, 0f);
                    mats[j] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
                }

                // 与管线无关的底层实例化绘制（URP 可用）；引擎自动算整组合并包围盒做剔除排序
                Graphics.DrawMeshInstanced(group.Mesh, 0, group.Material, mats, mats.Length,
                    group.MpbBatches[b], ShadowCastingMode.Off, false);
            }
        }
    }

    /// <summary>
    /// 调整激活数量（池复用：超出部分失活保留，回弹即复用）
    /// </summary>
    private void SetActiveCount(int count)
    {
        // 布局行列：按实例数与屏幕宽高比动态算（网格比例接近屏幕，避免数量大时纵向出框），
        // 从屏幕中心向四周扩展（1920*1080 等横屏下 3000 实例约为 74×41 网格）
        float aspect = Screen.width / (float)Screen.height;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
        float cx = (cols - 1) * 0.5f;
        float cy = (rows - 1) * 0.5f;

        while (_renderers.Count < count)
        {
            int i = _renderers.Count;
            _animIndices.Add(i % _anims.Count); // 多套动画轮转分配
            _renderers.Add(CreateInstance());

            // 布局：以屏幕中心为原点向四周扩展（间距 _spacing 调密集度）
            int gx = i % cols;
            int gy = i / cols;
            _basePos.Add(new Vector2((gx - cx) * _spacing, (gy - cy) * _spacing));
            _phase.Add(i * 0.618f); // 黄金比散布公转相位
            _cpuPlayers.Add(null);
        }

        _activeCount = count;
        ApplyMode();
    }

    /// <summary>
    /// 创建实例（渲染器类型由 _useMesh 决定：MeshRenderer / SpriteRenderer 两者 Unity 不允许共存）
    /// </summary>
    private Renderer CreateInstance()
    {
        var gpu = _anims[_animIndices[_renderers.Count]].Gpu;
        var go = new GameObject($"FrameAnimGpu_{_renderers.Count:000}");
        go.transform.SetParent(transform, false);

        Renderer renderer;
        if (_useMesh)
        {
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = gpu.QuadMesh;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            renderer = mr;
        }
        else
        {
            var sr = go.AddComponent<SpriteRenderer>();
            if (_defaultMat == null)
            {
                _defaultMat = sr.sharedMaterial; // 记录默认 Sprite 材质（CPU 换帧模式用）
            }

            sr.sprite = gpu.QuadSprite;
            renderer = sr;
        }

        renderer.sharedMaterial = gpu.Material;
        return renderer;
    }

    /// <summary>
    /// 把当前配置的模式作用到全部实例（启动 / 扩池后调用；模式为启动配置，运行期不切换）
    /// </summary>
    private void ApplyMode()
    {
        // GPU 手动帧模式关时间驱动；静帧用 _PlaySpeed=0 冻在起始帧（多套动画逐材质设置）
        bool timeDriven = _mode != AnimMode.CpuManualFrame;
        for (int a = 0; a < _anims.Count; a++)
        {
            var mat = _anims[a].Gpu.Material;
            mat.SetFloat(FrameAnimGpuUtil.PropTimeDriven, timeDriven ? 1f : 0f);
            mat.SetFloat(FrameAnimGpuUtil.PropPlaySpeed, _mode == AnimMode.StaticFrame ? 0f : PlaySpeed);
        }

        for (int i = 0; i < _renderers.Count; i++)
        {
            var renderer = _renderers[i];
            bool active = i < _activeCount;
            renderer.gameObject.SetActive(active);
            var anim = _anims[_animIndices[i]];

            if (_mode == AnimMode.CpuSwap)
            {
                if (active)
                {
                    EnsureCpuPlayer(i);
                }
                else
                {
                    ReleaseCpuPlayer(i);
                }
            }
            else
            {
                ReleaseCpuPlayer(i);
                if (active)
                {
                    renderer.sharedMaterial = anim.Gpu.Material;
                    if (!_useMesh)
                    {
                        ((SpriteRenderer)renderer).sprite = anim.Gpu.QuadSprite;
                    }

                    if (_mode == AnimMode.GpuMpbOffset)
                    {
                        _mpb.SetFloat(FrameAnimGpuUtil.PropFrameOffset, i % anim.Clip.sprites.Count);
                        renderer.SetPropertyBlock(_mpb);
                    }
                    else
                    {
                        renderer.SetPropertyBlock(null); // 清 MPB
                    }
                }
            }
        }
    }

    private void EnsureCpuPlayer(int i)
    {
        if (_cpuPlayers[i] != null)
        {
            return;
        }

        var anim = _anims[_animIndices[i]];
        var sr = (SpriteRenderer)_renderers[i];
        sr.SetPropertyBlock(null);
        sr.sharedMaterial = _defaultMat;
        var player = GameMgr.FrameAnim.CreatePlayer(sr, anim.Asset);
        player.SetSpeed(PlaySpeed);
        player.Play(anim.Clip.clipName);
        _cpuPlayers[i] = player;
    }

    private void ReleaseCpuPlayer(int i)
    {
        var player = _cpuPlayers[i];
        if (player == null)
        {
            return;
        }

        GameMgr.FrameAnim.ReleasePlayer(player);
        _cpuPlayers[i] = null;
    }

    private void UpdateStats()
    {
        float dt = Time.unscaledDeltaTime;
        _statFrames++;
        _statTime += dt;
        if (dt > _statWorst)
        {
            _statWorst = dt;
        }

        if (_statTime >= 1f)
        {
            _fps = _statFrames / _statTime;
            _avgMs = _statTime * 1000f / _statFrames;
            _worstMs = _statWorst * 1000f;
            _statFrames = 0;
            _statTime = 0f;
            _statWorst = 0f;
        }
    }

    /// <summary>
    /// 构造程序化模式 CPU 动画配置：复用同一份帧列表（与 GPU 方案完全同条件）
    /// </summary>
    private FrameAnimAsset BuildCpuAsset()
    {
        var asset = ScriptableObject.CreateInstance<FrameAnimAsset>();
        asset.clips = new List<FrameAnimAsset.Clip>
        {
            new()
            {
                clipName = "walk",
                sprites = _frames,
                duration = 0.8f,
                loop = true,
            },
        };
        return asset;
    }

    /// <summary>
    /// 在同一张大图上生成"不规则尺寸"帧：逐帧紧致裁切（尺寸随帧变化），
    /// 画面与 CPU 测试（FrameAnimTest）一致 -- 色相随帧渐变 + 方向白点每帧转 45°
    /// </summary>
    private void BuildFrames()
    {
        _sheet = new Texture2D(SheetW, SheetH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        _sheet.hideFlags = HideFlags.HideAndDontSave;

        var pixels = new Color[SheetW * SheetH];
        int x = FrameGap;
        for (int f = 0; f < FrameCount; f++)
        {
            int radius = 15 + (f * 5) % 12; // 15~26px，刻意造成帧尺寸不一
            int size = (radius + FrameMargin) * 2;
            int y = (SheetH - size) / 2;
            FillFrame(pixels, x, y, size, f, radius);

            // 紧致裁切：帧 rect 即内容包围盒，pivot 在圆心（与真实美术切片语义一致）
            // FullRect 与真实美术导入设置一致（img_*.png 的 meshType: 0），CPU/GPU 两方案同为 4 顶点四边形，对比公平
            float pivot = (radius + FrameMargin) / (float)size;
            _frames.Add(Sprite.Create(_sheet, new Rect(x, y, size, size), new Vector2(pivot, pivot), 100f, 0, SpriteMeshType.FullRect));
            x += size + FrameGap;
        }

        _sheet.SetPixels(pixels);
        _sheet.Apply();
    }

    /// <summary>
    /// 填充单帧：色相圆 + 随帧旋转的方向白点（圆心在帧 rect 中心）
    /// </summary>
    private void FillFrame(Color[] pixels, int x0, int y0, int size, int frame, int radius)
    {
        var color = Color.HSVToRGB(frame * 0.125f, 0.8f, 1f);
        float center = (size - 1) * 0.5f;
        float angle = frame * 45f * Mathf.Deg2Rad;
        float dirX = center + Mathf.Cos(angle) * radius * 0.55f;
        float dirY = center + Mathf.Sin(angle) * radius * 0.55f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                var c = dx * dx + dy * dy > radius * radius
                    ? new Color(0f, 0f, 0f, 0f)
                    : color;

                // 方向白点（半径 4）
                float pdx = x - dirX;
                float pdy = y - dirY;
                if (pdx * pdx + pdy * pdy <= 16f)
                {
                    c = Color.white;
                }

                pixels[(y0 + y) * SheetW + x0 + x] = c;
            }
        }
    }
}
