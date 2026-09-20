/*--------------------------------------------------------------
 * File: PerfBaselineTest.cs
 * Author: Wsw
 * Time: 2026/08/31
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 【临时测试】渲染性能基线验证（任务清单 阶段 1-1）：
/// 300 个池化 GameObject（SpriteRenderer / 图元）每帧位移，桌面 + 真机测帧率，
/// 并附 Graphics.DrawMeshInstanced 对照，结论作为阶段 6 渲染方案依据（GameObject 池 vs Instanced）。
///
/// 用法：
///  - 测试场景里给任意物体挂本组件即自动开始（或 TTTT 点"性能基线测试"动态创建）
///  - 真机打包前在 Inspector 调 BaselineMode / ObjectCount；测试过程 IMGUI 实时显示 FPS
///  - 每 5 秒 Log.Info 打印一次 FPS 统计（真机从日志查看）；点"导出报告"再汇总打一份
///  - 桌面/真机各测一轮，把三种模式对照结果记入任务清单 1-1 结论
/// 注意：Editor 里测出的帧率含编辑器开销且受 VSync 影响，真机结论为准。
/// 测试完成后删除本文件。
/// </summary>
public class PerfBaselineTest : MonoBehaviour, IUpdateable
{
    public enum BaselineMode
    {
        SpriteRenderer = 0, // 300 个 SpriteRenderer GameObject（与 VS 实体表现路径一致，推荐基线）
        PrimitiveCube   = 1, // 300 个 Cube 图元（MeshRenderer）
        Instanced       = 2, // Graphics.DrawMeshInstanced 批渲染（对照）
    }

    public BaselineMode Mode = BaselineMode.SpriteRenderer;
    public int ObjectCount = 300;
    public bool AutoStart = true;
    public float BoundsHalf = 18f; // 位移包围盒半宽（相机视野内，越界反弹保持在场内）

    private Transform _root;
    private GameObject[] _objs;
    private Vector3[] _pos;
    private Vector3[] _vel;
    private Texture2D _sharedTex;

    // Instanced 对照
    private Mesh _instMesh;
    private Material _instMat;
    private Matrix4x4[] _matrices;

    // FPS 统计（1 秒窗口采样）
    private float _fpsAccum;
    private int _fpsFrame;
    private float _curFps;
    private float _minFps = float.MaxValue;
    private float _fpsSum;
    private int _samples;
    private float _nextLogAt;

    private bool _running;
    private bool _built;

    private void Awake()
    {
        if (AutoStart)
            Build();
    }

    private void OnDestroy()
    {
        Clear();
    }

    // ---------------- 构建与清理 ----------------

    public void Build()
    {
        Clear();
        _built = true;
        _running = true;
        _minFps = float.MaxValue;
        _fpsSum = 0;
        _samples = 0;
        _fpsAccum = 0;
        _fpsFrame = 0;
        _nextLogAt = Time.realtimeSinceStartup + 5f;

        _root = new GameObject("PerfBaseline").transform;
        _pos = new Vector3[ObjectCount];
        _vel = new Vector3[ObjectCount];

        var rng = new System.Random(12345);
        for (int i = 0; i < ObjectCount; i++)
        {
            _pos[i] = new Vector3(
                (float)(rng.NextDouble() * 2 - 1) * BoundsHalf,
                (float)(rng.NextDouble() * 2 - 1) * BoundsHalf, 0f);
            _vel[i] = new Vector3(
                (float)(rng.NextDouble() * 2 - 1) * 3f,
                (float)(rng.NextDouble() * 2 - 1) * 3f, 0f);
        }

        switch (Mode)
        {
            case BaselineMode.SpriteRenderer: BuildSprites(); break;
            case BaselineMode.PrimitiveCube: BuildPrimitives(); break;
            case BaselineMode.Instanced: BuildInstanced(); break;
        }

        CoreMgr.Update.RegisterUpdate(this);
        Log.Info("[PerfBaseline] 开始: mode=", Mode, " count=", ObjectCount);
    }

    public void Clear()
    {
        if (_built)
            CoreMgr.Update.UnRegisterUpdate(this);
        _built = false;
        _running = false;

        if (_root != null)
        {
            Destroy(_root.gameObject);
            _root = null;
        }
        _objs = null;
        _pos = null;
        _vel = null;
        _matrices = null;

        if (_sharedTex != null)
        {
            Destroy(_sharedTex);
            _sharedTex = null;
        }
        if (_instMesh != null)
        {
            Destroy(_instMesh);
            _instMesh = null;
        }
        if (_instMat != null)
        {
            Destroy(_instMat);
            _instMat = null;
        }
    }

    private void SwitchMode(BaselineMode m)
    {
        Mode = m;
        Log.Info("[PerfBaseline] 切换模式: ", m);
        Build();
    }

    // ---------------- 三种渲染路径 ----------------

    private void BuildSprites()
    {
        // 共享白色 2x2 纹理 + Sprite（同 Sprite/同材质，SpriteRenderer 可动态合批，贴近真实实体表现）
        _sharedTex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        _sharedTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        _sharedTex.Apply();
        var sprite = Sprite.Create(_sharedTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);

        _objs = new GameObject[ObjectCount];
        var rng = new System.Random(7);
        for (int i = 0; i < ObjectCount; i++)
        {
            var go = new GameObject($"Entity_{i}");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = _pos[i];
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.HSVToRGB((i * 0.13f) % 1f, 0.6f, 1f);
            _objs[i] = go;
        }
    }

    private void BuildPrimitives()
    {
        _objs = new GameObject[ObjectCount];
        for (int i = 0; i < ObjectCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Entity_{i}";
            go.transform.SetParent(_root, false);
            go.transform.localScale = Vector3.one * 0.5f;
            go.transform.localPosition = _pos[i];
            // 共享 URP 材质（真机兜底链见 GetMat）
            go.GetComponent<Renderer>().sharedMaterial = GetMat(Color.HSVToRGB((i * 0.13f) % 1f, 0.6f, 1f));
            _objs[i] = go;
        }
    }

    private void BuildInstanced()
    {
        _instMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        _instMat = GetMat(Color.white);
        _matrices = new Matrix4x4[ObjectCount];
        for (int i = 0; i < ObjectCount; i++)
            _matrices[i] = Matrix4x4.TRS(_pos[i], Quaternion.identity, Vector3.one * 0.5f);
    }

    private void OnRenderObject()
    {
        if (Mode != BaselineMode.Instanced || !_running || _matrices == null) return;
        Graphics.DrawMeshInstanced(_instMesh, 0, _instMat, _matrices, _matrices.Length);
    }

    // ---------------- 驱动（CoreMgr.Update，表现层可用 float） ----------------

    public int Priority => 0;

    public void MyUpdate(float deltaTime, float realDeltaTime)
    {
        if (!_running) return;

        // 每帧位移（越界反弹保持在场内）
        for (int i = 0; i < ObjectCount; i++)
        {
            var p = _pos[i] + _vel[i] * realDeltaTime;
            if (p.x > BoundsHalf) { p.x = BoundsHalf; _vel[i].x = -Mathf.Abs(_vel[i].x); }
            else if (p.x < -BoundsHalf) { p.x = -BoundsHalf; _vel[i].x = Mathf.Abs(_vel[i].x); }
            if (p.y > BoundsHalf) { p.y = BoundsHalf; _vel[i].y = -Mathf.Abs(_vel[i].y); }
            else if (p.y < -BoundsHalf) { p.y = -BoundsHalf; _vel[i].y = Mathf.Abs(_vel[i].y); }
            _pos[i] = p;
        }

        switch (Mode)
        {
            case BaselineMode.SpriteRenderer:
            case BaselineMode.PrimitiveCube:
                for (int i = 0; i < ObjectCount; i++)
                    _objs[i].transform.localPosition = _pos[i];
                break;
            case BaselineMode.Instanced:
                for (int i = 0; i < ObjectCount; i++)
                    _matrices[i] = Matrix4x4.TRS(_pos[i], Quaternion.identity, Vector3.one * 0.5f);
                break;
        }

        // FPS 统计（1 秒窗口）
        _fpsAccum += realDeltaTime;
        _fpsFrame++;
        if (_fpsAccum >= 1f)
        {
            var fps = _fpsFrame / _fpsAccum;
            _curFps = fps;
            if (fps < _minFps) _minFps = fps;
            _fpsSum += fps;
            _samples++;
            _fpsAccum = 0;
            _fpsFrame = 0;
        }

        if (Time.realtimeSinceStartup >= _nextLogAt)
        {
            _nextLogAt = Time.realtimeSinceStartup + 5f;
            Log.Info("[PerfBaseline] ", Mode, " count=", ObjectCount,
                " 当前FPS=", _curFps.ToString("F1"),
                " 最低FPS=", (_minFps == float.MaxValue ? 0f : _minFps).ToString("F1"),
                " 平均FPS=", (_samples > 0 ? _fpsSum / _samples : 0f).ToString("F1"),
                " 采样", _samples, "秒");
        }
    }

    // ---------------- IMGUI 面板 ----------------

    private void OnGUI()
    {
        var skin = GUI.skin;
        skin.button.fontSize = 22;
        skin.label.fontSize = 24;
        skin.button.fixedHeight = 52;

        var rect = new Rect(20, 20, 480, 520);
        GUI.Box(rect, "");
        GUILayout.BeginArea(rect);
        GUILayout.Label($"[性能基线] mode={Mode} count={ObjectCount}");
        GUILayout.Label($"当前FPS: {_curFps:F1}");
        GUILayout.Label($"最低FPS: {(_minFps == float.MaxValue ? 0f : _minFps):F1}");
        GUILayout.Label($"平均FPS: {(_samples > 0 ? _fpsSum / _samples : 0f):F1}");

        GUILayout.Space(8);
        if (GUILayout.Button("SpriteRenderer 模式")) SwitchMode(BaselineMode.SpriteRenderer);
        if (GUILayout.Button("PrimitiveCube 模式")) SwitchMode(BaselineMode.PrimitiveCube);
        if (GUILayout.Button("Instanced 模式")) SwitchMode(BaselineMode.Instanced);
        if (GUILayout.Button("重建")) Build();
        if (GUILayout.Button("停止/清理")) Clear();
        if (GUILayout.Button("导出报告")) Report();
        GUILayout.EndArea();
    }

    private void Report()
    {
        Log.Info("[PerfBaseline] === 报告 === mode=", Mode, " count=", ObjectCount,
            " 最低FPS=", (_minFps == float.MaxValue ? 0f : _minFps).ToString("F1"),
            " 平均FPS=", (_samples > 0 ? _fpsSum / _samples : 0f).ToString("F1"),
            " 采样", _samples, "秒  (记入任务清单 1-1 结论)");
    }

    // ---------------- URP 材质工厂（真机兜底链，同 TdView.GetMat） ----------------

    private static readonly Dictionary<Color, Material> s_matCache = new();

    private static Material GetMat(Color c)
    {
        if (s_matCache.TryGetValue(c, out var m)) return m;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
            {
                m = new Material(rp.defaultMaterial);
            }
            else
            {
                m = new Material(Shader.Find("Sprites/Default"));
            }
        }
        else
        {
            m = new Material(shader);
        }

        m.SetColor("_BaseColor", c);
        m.SetColor("_Color", c);
        s_matCache[c] = m;
        return m;
    }
}
