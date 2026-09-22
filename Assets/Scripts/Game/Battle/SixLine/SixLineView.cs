/*--------------------------------------------------------------
 * File: SixLineView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 六连珠（表现层）：
/// - 组装 SixLineLogic + SlLocalDriver（本地固定步长驱动）+ SlKeyboardInput（P0 移动/旋转/加速，触屏按键 1-6 接入）
/// - 灰盒最小表现：蜂窝错行棋盘（偶行 10 格/奇行 9 格）+ 圆形棋子 + 三角下落组（按旋转状态渲染 ▲/▽）
///   （横屏双棋盘左右并排等大）；1-6 正式化布局适配/下落插值/合批/出组预览/触屏按键
/// - 表现层读逻辑层数据只允许 Fix.AsFloat
/// </summary>
public class SixLineView : BattleView
{
    // ---- 灰盒布局（横屏 1920×1080：正交 size 10 => 视野约 35.6×20 格；双方等大棋盘左右并排）----
    private const float CellSize = 1.4f; // 格尺寸（= 圆直径）：12 行棋盘高 16.8 格，上下留 HUD 空间
    private const float BoardWidth = 10f;
    // 棋盘左下角（世界坐标）：P0 左、P1 右，中间留 2 格间隔，两侧对称余量
    private static readonly Vector2 P0Origin = new(-15f, -8.4f);
    private static readonly Vector2 P1Origin = new(1f, -8.4f);

    // 5 色灰盒调色板（颜色 id 1~5：红/蓝/绿/黄/紫）
    private static readonly Color[] s_palette =
    {
        new(0.90f, 0.25f, 0.25f, 1f),
        new(0.25f, 0.55f, 0.95f, 1f),
        new(0.30f, 0.80f, 0.35f, 1f),
        new(0.95f, 0.80f, 0.25f, 1f),
        new(0.70f, 0.40f, 0.90f, 1f),
    };

    private static Mesh _circleMesh; // 圆盘网格（扇形三角化，半径 0.5，全局共享）

    private SixLineLogic _logic;
    private SlLocalDriver _driver;
    private SlKeyboardInput _keyboardInput;
    private long _seed;

    private Transform _root;
    private BoardView _p0;
    private BoardView _p1;

    /// <summary>单方棋盘表现（格子渲染器下标与 Board.Cells 对齐；Tri = 底左/底右/顶）</summary>
    private class BoardView
    {
        public Transform Root;
        public MeshRenderer[] Cells;
        public Transform[] Tri;
    }

    public override void Init()
    {
        // 阶段 1 本地对局：本地生成种子（同种子可重放验证 StateHash）；阶段 3 改读 Room.Seed（StartGamePush 下发）
        _seed = GenerateSeed();
        _logic = new SixLineLogic(_seed);
        _driver = new SlLocalDriver(_logic);
        _keyboardInput = new SlKeyboardInput(0); // TODO(1-7) P1 键盘源 / 触屏按键

        _root = new GameObject("SixLineRoot").transform;
        _p0 = BuildBoard("P0Board", P0Origin, CellSize);
        _p1 = BuildBoard("P1Board", P1Origin, CellSize);

        _driver.RegisterInput(_keyboardInput);
        _driver.OnFrame += OnFrame;
        // TODO(1-6) 订阅 OnRenderFrame 做下落插值

        Log.Info("[六连珠] 进入战斗, 种子:", _seed);
    }

    public override void Dispose()
    {
        if (_driver != null)
        {
            _driver.OnFrame -= OnFrame;
            _driver.Dispose();
            _driver = null;
        }
        _keyboardInput = null;

        if (_root != null)
        {
            _root.SetParent(null);
            Object.Destroy(_root.gameObject);
            _root = null;
            _p0 = null;
            _p1 = null;
        }
        _logic = null;
    }

    /// <summary>逻辑帧推进：棋盘/三角组刷新，周期哈希日志供同种子重放对比</summary>
    private void OnFrame(int frame)
    {
        RefreshPlayer(_logic.Players[0], _p0, CellSize);
        RefreshPlayer(_logic.Players[1], _p1, CellSize);

        if (frame % 50 == 0)
            Log.Info("[六连珠][校验] 帧:", frame, "哈希:", _logic.StateHash());
    }

    // ---- 灰盒图元构建与刷新 ----

    private BoardView BuildBoard(string name, Vector2 origin, float cell)
    {
        var bv = new BoardView();
        bv.Root = new GameObject(name).transform;
        bv.Root.SetParent(_root, false);
        bv.Root.position = origin; // 棋盘左下角锚定

        var w = BoardWidth * cell;
        var h = SixLineLogic.Rows * cell;

        // z 越大越靠后：底板 -> 危险区罩 -> 棋子 -> 下落组
        MakeQuad(bv.Root, "Bg", w, h, new Color(0.13f, 0.14f, 0.17f, 1f))
            .localPosition = new Vector3(w * 0.5f, h * 0.5f, 0.5f);

        var dh = SixLineLogic.DangerRows * cell;
        MakeQuad(bv.Root, "Danger", w, dh, new Color(0.9f, 0.2f, 0.2f, 0.15f))
            .localPosition = new Vector3(w * 0.5f, h - dh * 0.5f, 0.3f);

        // 格子圆池（空位隐藏，入格点亮；1-6 正式化走池化/合批）
        bv.Cells = new MeshRenderer[SixLineLogic.TotalCells];
        for (int r = 0; r < SixLineLogic.Rows; r++)
            for (int c = 0; c < SixLineLogic.RowCells(r); c++)
            {
                var circle = MakeCircle(bv.Root, $"B_{r}_{c}", cell * 0.96f, Color.white);
                circle.localPosition = CellLocal(r, c, cell, 0.1f);
                var mr = circle.GetComponent<MeshRenderer>();
                mr.enabled = false;
                bv.Cells[SixLineLogic.RowStart(r) + c] = mr;
            }

        // 下落三角组（底左/底右/顶）
        bv.Tri = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            bv.Tri[i] = MakeCircle(bv.Root, $"Tri_{i}", cell * 0.96f, Color.white);
            bv.Tri[i].gameObject.SetActive(false);
        }
        return bv;
    }

    private void RefreshPlayer(SixLineLogic.Player p, BoardView bv, float cell)
    {
        for (int r = 0; r < SixLineLogic.Rows; r++)
            for (int c = 0; c < SixLineLogic.RowCells(r); c++)
            {
                var color = p.Board.Get(r, c);
                var mr = bv.Cells[SixLineLogic.RowStart(r) + c];
                if (color == SixLineLogic.EmptyColor)
                {
                    mr.enabled = false;
                    continue;
                }
                mr.enabled = true;
                mr.material.color = s_palette[color - 1];
            }

        var tri = p.Tri;
        if (tri.State == SixLineLogic.PieceState.Falling)
        {
            // 按旋转状态渲染 ▲（单颗在上）/ ▽（单颗在下）；颜色取自逻辑层旋转映射
            var r = tri.Rotation;
            var yPair = tri.Y.AsFloat;
            SetCircle(bv.Tri[0], (tri.Slot + 0.5f) * cell, (yPair + 0.5f) * cell, tri.Colors[SixLineLogic.EvenLeftColor[r]]);
            SetCircle(bv.Tri[1], (tri.Slot + 1.5f) * cell, (yPair + 0.5f) * cell, tri.Colors[SixLineLogic.EvenRightColor[r]]);
            var ySingle = yPair + ((r & 1) == 0 ? 1f : -1f);
            SetCircle(bv.Tri[2], (tri.Slot + 1f) * cell, (ySingle + 0.5f) * cell, tri.Colors[SixLineLogic.OddPathColor[r]]);
        }
        else
        {
            for (int i = 0; i < 3; i++)
                bv.Tri[i].gameObject.SetActive(false);
        }
    }

    private void SetCircle(Transform t, float x, float y, int color)
    {
        t.gameObject.SetActive(true);
        t.localPosition = new Vector3(x, y, 0f);
        t.GetComponent<MeshRenderer>().material.color = s_palette[color - 1];
    }

    /// <summary>格中心局部坐标（蜂窝错行：偶行 x=col+0.5，奇行 x=col+1）</summary>
    private static Vector3 CellLocal(int row, int col, float cell, float z)
    {
        var x = col + 0.5f + (SixLineLogic.RowIsWide(row) ? 0f : 0.5f);
        return new Vector3(x * cell, (row + 0.5f) * cell, z);
    }

    /// <summary>图元方块（底板/危险区罩；Sprites/Default，Editor 灰盒够用，真机兜底随 1-6 重建 ViewMats）</summary>
    private static Transform MakeQuad(Transform parent, string name, float w, float h, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>()); // 图元自带碰撞体，纯表现去掉

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };

        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.localScale = new Vector3(w, h, 1f);
        return tr;
    }

    /// <summary>圆形棋子（共享圆盘网格 + 实例材质上色）</summary>
    private static Transform MakeCircle(Transform parent, string name, float diameter, Color color)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = CircleMesh();
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };

        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.localScale = new Vector3(diameter, diameter, 1f);
        return tr;
    }

    /// <summary>圆盘网格（24 段扇形，半径 0.5，随缩放适配格尺寸）</summary>
    private static Mesh CircleMesh()
    {
        if (_circleMesh != null)
            return _circleMesh;

        const int seg = 24;
        var verts = new Vector3[seg + 1];
        var tris = new int[seg * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < seg; i++)
        {
            var a = (float)(i * Math.PI * 2.0 / seg);
            verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + i;
            tris[i * 3 + 2] = 1 + (i + 1) % seg;
        }
        _circleMesh = new Mesh { vertices = verts, triangles = tris };
        _circleMesh.RecalculateNormals();
        return _circleMesh;
    }

    /// <summary>本地对局种子（联机阶段改为服务器下发，本方法删除）</summary>
    private static long GenerateSeed()
    {
        return DateTime.UtcNow.Ticks;
    }
}
