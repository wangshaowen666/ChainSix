/*--------------------------------------------------------------
 * File: SixLineView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 六连珠（表现层）：
/// - 组装 SixLineLogic + SlLocalDriver（本地固定步长驱动）+ SlKeyboardInput（P0 移动/旋转/加速，触屏按键 1-6 接入）
/// - 蜂窝错行棋盘（偶行 10 格/奇行 9 格）+ 圆形棋子池 + 三角下落组（按旋转状态渲染 ▲/▽，
///   旋转时三球保色走位到新姿态 = 旋转过程），横屏双棋盘左右并排
/// - 动画层（VS 双缓冲范式）：逻辑帧对账做来源匹配——新占用格从正上方/斜上方刚空出的格认领视觉圈，
///   落定组三颗从下落组手中移交；渲染帧所有视觉圈向目标位匀速趋近，下落/滚落/沉降全程平滑无瞬移
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

    // 动画速度（世界单位/秒）
    private const float TriFollowSpeed = 30f; // 下落组跟随逻辑位（掩盖 20Hz 步进）
    private const float SettleSpeed = 16f;    // 沉降/滚落移动速度
    private const float RotAnimSec = 0.1f;    // 旋转过渡时长（秒）：三球按统一时长转位，保留颜色身份

    // 5 色灰盒调色板（颜色 id 1~5：红/黄/蓝/绿/紫）
    private static readonly Color[] s_palette =
    {
        new(0.90f, 0.25f, 0.25f, 1f),
        new(0.95f, 0.80f, 0.25f, 1f),
        new(0.25f, 0.55f, 0.95f, 1f),
        new(0.30f, 0.80f, 0.35f, 1f),
        new(0.70f, 0.40f, 0.90f, 1f),
    };

    private static Mesh _circleMesh; // 圆盘网格（扇形三角化，半径 0.5，全局共享）

    private SixLineLogic _logic;
    private SlLocalDriver _driver;
    private SlKeyboardInput _keyboardInput;
    private long _seed;
    private int _curFrame; // 当前逻辑帧号（视觉圈认领去重戳）

    private Transform _root;
    private BoardView _p0;
    private BoardView _p1;

    /// <summary>单颗视觉圈：逻辑帧对账确定目标位，渲染帧向目标匀速趋近</summary>
    private class CircleAnim
    {
        public Transform Tr;
        public MeshRenderer Mr;
        public int Color;      // 颜色 id（0 = 未着色）
        public Vector3 Pos;    // 视觉位置（棋盘局部）
        public Vector3 Target; // 目标位置（棋盘局部）
        public float Speed;    // 趋近速度（单位/秒）
        public int LastCell;   // 上一逻辑帧占用的格子下标（-1 = 无，如刚落定的下落组）
        public int Stamp;      // 最近被认领的逻辑帧号（同帧防重复认领）
    }

    /// <summary>单方棋盘表现（格子下标与 Board.Cells 对齐）</summary>
    private class BoardView
    {
        public Transform Root;
        public float Cell;
        public CircleAnim[] CellAnims; // 格槽 -> 绑定视觉圈（null = 空格）
        public int[] Prev;             // 上一逻辑帧棋盘（来源匹配）
        public bool TriActive;         // 下落组是否激活
        public int TriSeqPos = -1;     // 当前下落组对应的出子游标（变化 = 旧组落定 + 新组出生）
        public int TriRotation;        // 下落组已同步的旋转姿态（变化 = 触发旋转过渡动画）
        public CircleAnim[] TriAnims;  // 下落组三颗（按球序绑定：0=A 1=B 2=C，颜色身份不变）
        public List<CircleAnim> Free;  // 空闲视觉圈池（含刚释放待认领的）
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
        _driver.OnRenderFrame += OnRenderFrame;

        Log.Info("[六连珠] 进入战斗, 种子:", _seed);
    }

    public override void Dispose()
    {
        if (_driver != null)
        {
            _driver.OnFrame -= OnFrame;
            _driver.OnRenderFrame -= OnRenderFrame;
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

    /// <summary>逻辑帧推进：棋盘/三角组对账（确定各视觉圈目标位），周期哈希日志供同种子重放对比</summary>
    private void OnFrame(int frame)
    {
        _curFrame = frame;
        SyncPlayer(_logic.Players[0], _p0);
        SyncPlayer(_logic.Players[1], _p1);

        if (frame % 50 == 0)
            Log.Info("[六连珠][校验] 帧:", frame, "哈希:", _logic.StateHash());
    }

    /// <summary>渲染帧推进：所有视觉圈向目标位匀速趋近（下落/滚落/沉降平滑呈现）</summary>
    private void OnRenderFrame(float alpha, float dt)
    {
        UpdateAnims(_p0, dt);
        UpdateAnims(_p1, dt);
    }

    private static void UpdateAnims(BoardView bv, float dt)
    {
        // 格子圈 + 下落组圈统一按速度趋近
        for (int i = 0; i < bv.CellAnims.Length; i++)
        {
            var a = bv.CellAnims[i];
            if (a != null)
                StepAnim(a, dt);
        }

        if (bv.TriActive)
            for (int i = 0; i < 3; i++)
                StepAnim(bv.TriAnims[i], dt);
    }

    private static void StepAnim(CircleAnim a, float dt)
    {
        a.Pos = Vector3.MoveTowards(a.Pos, a.Target, a.Speed * dt);
        a.Tr.localPosition = a.Pos;
    }

    // ---- 逻辑帧对账 ----

    private void SyncPlayer(SixLineLogic.Player p, BoardView bv)
    {
        var cells = p.Board.Cells;
        var tri = p.Tri;

        // ---- 下落组状态机（SeqPos 变化 = 新组出生：同一 Tick 内旧组落定 + 新组出组，必须先移交再重生）----
        if (bv.TriSeqPos != p.SeqPos)
        {
            if (bv.TriActive)
            {
                // 旧组刚落定：三颗捐入空闲池（Pos 保持最后下落位，LastCell = -1，供落定格就近认领）
                for (int i = 0; i < 3; i++)
                {
                    bv.TriAnims[i].LastCell = -1;
                    bv.Free.Add(bv.TriAnims[i]);
                    bv.TriAnims[i] = null;
                }
                bv.TriActive = false;
            }

            // 新组出生：从池取三颗按球序绑定（0=A 1=B 2=C），Pos = 首帧理想渲染位（无位移感）
            var y0 = tri.Y.AsFloat;
            var s0 = tri.Slot;
            for (int b = 0; b < 3; b++)
            {
                BallIdealPos(tri.Rotation, s0, y0, b, out var bx, out var by);
                var a = TakeFromPool(bv);
                bv.TriAnims[b] = a;
                a.Speed = TriFollowSpeed;
                a.Pos = new Vector3(bx * bv.Cell, by * bv.Cell, 0.2f);
                a.Tr.localPosition = a.Pos;
                SetTriTarget(a, bx * bv.Cell, by * bv.Cell, tri.Colors[b]);
            }
            bv.TriActive = true;
            bv.TriSeqPos = p.SeqPos;
            bv.TriRotation = tri.Rotation;
        }

        if (bv.TriActive && tri.State == SixLineLogic.PieceState.Falling)
        {
            // 三颗按球序（0=A 1=B 2=C）走位到各自理想渲染位（▲ 格点位 / ▽ 理想 60° 位），颜色身份不变
            var r = tri.Rotation;
            var y = tri.Y.AsFloat;
            var s = tri.Slot;

            if (bv.TriRotation != r)
            {
                // 旋转过渡：三球按统一时长转向新姿态（走位过程即 60° 旋转表现）
                for (int b = 0; b < 3; b++)
                {
                    BallIdealPos(r, s, y, b, out var bx, out var by);
                    var a = bv.TriAnims[b];
                    var tx = bx * bv.Cell;
                    var ty = by * bv.Cell;
                    var d = Vector2.Distance(new Vector2(tx, ty), a.Pos);
                    a.Speed = d > 0.0001f ? d / RotAnimSec : TriFollowSpeed;
                    SetTriTarget(a, tx, ty, tri.Colors[b]);
                }
                bv.TriRotation = r;
            }
            else
            {
                for (int b = 0; b < 3; b++)
                {
                    BallIdealPos(r, s, y, b, out var bx, out var by);
                    bv.TriAnims[b].Speed = TriFollowSpeed; // 过渡已结束：恢复跟随速度，避免旋转低速残留导致移动掉队
                    SetTriTarget(bv.TriAnims[b], bx * bv.Cell, by * bv.Cell, tri.Colors[b]);
                }
            }
        }

        // ---- 格子对账 pass1：新变空的格子释放视觉圈进池（保留位置，供同帧新占用格认领）----
        for (int i = 0; i < SixLineLogic.TotalCells; i++)
        {
            if (cells[i] != SixLineLogic.EmptyColor || bv.Prev[i] == SixLineLogic.EmptyColor || bv.CellAnims[i] == null)
                continue;
            bv.CellAnims[i].LastCell = i;
            bv.Free.Add(bv.CellAnims[i]);
            bv.CellAnims[i] = null;
        }

        // ---- 格子对账 pass2：占用格子绑定视觉圈 ----
        for (int r = 0; r < SixLineLogic.Rows; r++)
        {
            for (int c = 0; c < SixLineLogic.RowCells(r); c++)
            {
                var idx = SixLineLogic.RowStart(r) + c;
                var color = cells[idx];
                if (color == SixLineLogic.EmptyColor)
                    continue;

                var anim = bv.CellAnims[idx];
                if (anim != null && anim.Color == color)
                {
                    anim.LastCell = idx; // 延续在位：刷新来源戳供后续沉降匹配
                    anim.Stamp = _curFrame;
                    continue;
                }

                if (anim != null)
                    bv.Free.Add(anim); // 换色异常（理论不发生）：原圈归还池

                var take = TakeForCell(bv, idx, r, c, color);
                bv.CellAnims[idx] = take;
            }
        }

        // ---- pass3：隐藏本帧未被认领的池圈 ----
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp != _curFrame && a.Tr.gameObject.activeSelf)
                a.Tr.gameObject.SetActive(false);
        }

        Array.Copy(cells, bv.Prev, SixLineLogic.TotalCells);
    }

    /// <summary>为新占用格认领视觉圈：优先来源格（正上 2 行 = 垂直下落 / 斜上 = 滚落）同色，其次同色就近，再次任意重着色，最后新建</summary>
    private CircleAnim TakeForCell(BoardView bv, int idx, int row, int col, int color)
    {
        var target = CellLocal(row, col, bv.Cell, 0.1f);
        CircleAnim best;
        var bestDist = float.MaxValue;

        // 1. 垂直来源（正上 2 行刚空出）
        best = TakeSource(bv, color, row + 2 < SixLineLogic.Rows ? SixLineLogic.RowStart(row + 2) + col : -1);
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 2. 斜上来源（滚落）
        best = TakeSource(bv, color, SixLineLogic.Neighbor(row, col, 2));
        if (best != null)
            return Bind(bv, best, idx, color, target);
        best = TakeSource(bv, color, SixLineLogic.Neighbor(row, col, 3));
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 3. 同色就近
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp == _curFrame || a.Color != color)
                continue;
            var d = (a.Pos - target).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 4. 任意就近（重着色兜底）
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp == _curFrame)
                continue;
            var d = (a.Pos - target).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 5. 池耗尽（理论不发生：圈数守恒）新建
        var na = new CircleAnim
        {
            Tr = MakeCircle(bv.Root, $"Anim_{idx}", bv.Cell * 0.96f, s_palette[color - 1]),
            Mr = null,
            Color = color,
            Pos = target,
            Target = target,
            Speed = SettleSpeed,
            LastCell = idx,
            Stamp = _curFrame,
        };
        na.Mr = na.Tr.GetComponent<MeshRenderer>();
        na.Tr.gameObject.SetActive(true);
        return na;
    }

    private CircleAnim Bind(BoardView bv, CircleAnim a, int idx, int color, Vector3 target)
    {
        bv.Free.Remove(a);
        if (a.Color != color)
        {
            a.Color = color;
            a.Mr.material.color = s_palette[color - 1];
        }
        a.Target = target;
        a.Speed = SettleSpeed;
        a.LastCell = idx;
        a.Stamp = _curFrame;
        a.Tr.gameObject.SetActive(true);
        return a;
    }

    /// <summary>从空闲池按来源格 + 同色认领（认领即出池）</summary>
    private CircleAnim TakeSource(BoardView bv, int color, int srcIdx)
    {
        if (srcIdx < 0)
            return null;
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp != _curFrame && a.Color == color && a.LastCell == srcIdx)
            {
                bv.Free.RemoveAt(i);
                return a;
            }
        }
        return null;
    }

    /// <summary>从空闲池取任意一个（未认领的）</summary>
    private CircleAnim TakeFromPool(BoardView bv)
    {
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp != _curFrame)
            {
                bv.Free.RemoveAt(i);
                return a;
            }
        }
        var na = new CircleAnim
        {
            Tr = MakeCircle(bv.Root, "Anim", bv.Cell * 0.96f, Color.white),
            LastCell = -1,
        };
        na.Mr = na.Tr.GetComponent<MeshRenderer>();
        return na;
    }

    /// <summary>球 ball（0=A,1=B,2=C）在姿态 r 的理想渲染位（格单位局部坐标；▲=格点位，▽=理想 60° 位）</summary>
    private static void BallIdealPos(int r, int s, float y, int ball, out float x, out float py)
    {
        if ((r & 1) == 0)
        {
            if (SixLineLogic.EvenLeftColor[r] == ball) { x = s + 0.5f; py = y + 0.5f; }
            else if (SixLineLogic.EvenRightColor[r] == ball) { x = s + 1.5f; py = y + 0.5f; }
            else { x = s + 1f; py = y + 1.5f; } // OddLeftColor[r]
        }
        else
        {
            if (SixLineLogic.OddLeftColor[r] == ball) { x = s + 0.5f; py = y + 1.5f; }
            else if (SixLineLogic.OddRightColor[r] == ball) { x = s + 1.5f; py = y + 1.5f; }
            else { x = s + 1f; py = y + 0.5f; } // EvenRightColor[r]
        }
    }

    private static void SetTriTarget(CircleAnim a, float x, float y, int color)
    {
        if (a.Color != color)
        {
            a.Color = color;
            a.Mr.material.color = s_palette[color - 1];
        }
        a.Target = new Vector3(x, y, 0.2f);
        a.Tr.gameObject.SetActive(true);
    }

    // ---- 灰盒图元构建 ----

    private BoardView BuildBoard(string name, Vector2 origin, float cell)
    {
        var bv = new BoardView
        {
            Root = new GameObject(name).transform,
            Cell = cell,
            CellAnims = new CircleAnim[SixLineLogic.TotalCells],
            Prev = new int[SixLineLogic.TotalCells],
            TriAnims = new CircleAnim[3],
            Free = new List<CircleAnim>(SixLineLogic.TotalCells + 3),
        };
        bv.Root.SetParent(_root, false);
        bv.Root.position = origin; // 棋盘左下角锚定

        var w = BoardWidth * cell;
        var h = SixLineLogic.Rows * cell;

        // z 越大越靠后：底板 -> 危险区罩 -> 棋子
        MakeQuad(bv.Root, "Bg", w, h, new Color(0.13f, 0.14f, 0.17f, 1f))
            .localPosition = new Vector3(w * 0.5f, h * 0.5f, 0.5f);

        var dh = SixLineLogic.DangerRows * cell;
        MakeQuad(bv.Root, "Danger", w, dh, new Color(0.9f, 0.2f, 0.2f, 0.15f))
            .localPosition = new Vector3(w * 0.5f, h - dh * 0.5f, 0.3f);

        // 视觉圈池（TotalCells 格 + 3 颗下落组，全部先入空闲池隐藏）
        for (int i = 0; i < SixLineLogic.TotalCells + 3; i++)
        {
            var a = new CircleAnim
            {
                Tr = MakeCircle(bv.Root, $"C_{i}", cell * 0.96f, Color.white),
                Color = 0,
                LastCell = -1,
            };
            a.Mr = a.Tr.GetComponent<MeshRenderer>();
            a.Tr.gameObject.SetActive(false);
            bv.Free.Add(a);
        }
        return bv;
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
