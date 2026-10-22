/*--------------------------------------------------------------
 * File: SixLineLogic.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;

/// <summary>
/// 六连珠确定性逻辑层（纯 C#，无 UnityEngine）。帧同步纪律：禁 float/double、System.Random、
/// Time.*、Dictionary 遍历、Tick 内分配；同帧号 + 同操作序列 => 任意端状态一致。
/// partial 拆分：本文件 = 常量/状态/Tick/输入/StateHash；Board = 几何与数据；
/// Group = 三角组状态机；Settle = 物理沉降；Match = 消除与连锁。
/// </summary>
public partial class SixLineLogic
{
    // ---- 规则常量（1-8 迁入 Luban 调参表）----
    public const int LogicFrameMs = 50;      // 与 FrameSyncMgr.LogicFrameMs 一致

    public const int Rows = 12;              // row 0 = 底部
    public const int DangerRows = 2;         // 顶部危险区行数
    public const int PlayerCount = 2;
    public const int PreviewCount = 3;
    public const int EmptyColor = 0;
    public const int ColorMin = 1;
    public const int ColorMax = 5;           // 颜色数 = 难度总开关
    public const int SlotCount = 9;          // 横向相位数（奇行锚点列 0..8）
    public const int ClearCount = 6;
    public const int MaxSettleRounds = 20;   // 结算轮数防御上限
    public const int GraceFrames = 10;       // 可沉降容错窗口（0.5s @ 50ms）

    public static readonly Fix MoveSpeed = Fix.FromDouble(0.25);      // 横移 0.25 列/帧
    public static readonly Fix SlowFallSpeed = Fix.FromDouble(0.08);  // 0.08 格/帧
    public static readonly Fix FastFallSpeed = Fix.FromInt(1);        // 1 格/帧
    private static readonly Fix BallTouch = Fix.FromDouble(0.96);     // 球直径：中心距 < 此值即重叠
    private static readonly Fix BallTouchSq = BallTouch * BallTouch;

    // ---- 对局状态 ----

    public readonly List<Player> Players = new List<Player>(PlayerCount);

    /// <summary>双方共享出子序列：第 N 组一致，懒生成，追加顺序随消费顺序确定</summary>
    public readonly List<int> SharedSeq = new List<int>(64);

    public bool GameOver { get; private set; }
    public int LastTickFrame { get; private set; }

    /// <summary>最近一次被拒原因（诊断用，不进 StateHash）</summary>
    public RejectReason LastReject { get; private set; }

    private readonly XRng _rng;
    private readonly byte[] _elim = new byte[TotalCells];      // 消除标记（扫描复用）
    private readonly byte[] _visited = new byte[TotalCells];   // flood fill 访问标记
    private readonly int[] _stack = new int[TotalCells];
    private readonly int[] _cluster = new int[TotalCells];
    private readonly int[] _rowOf = new int[TotalCells];       // 扁平下标 -> 行
    private readonly int[] _colOf = new int[TotalCells];       // 扁平下标 -> 列

    public SixLineLogic(long seed)
    {
        _rng = new XRng((ulong)seed);

        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < RowCells(r); c++)
            {
                var idx = RowStart(r) + c;
                _rowOf[idx] = r;
                _colOf[idx] = c;
            }

        for (int i = 0; i < PlayerCount; i++)
        {
            var p = new Player { PlayerIndex = i };
            DrawNext(p);
            Players.Add(p);
        }
    }

    /// <summary>
    /// 推进一逻辑帧。步骤顺序即确定性本身，禁止调整：
    /// 1 应用输入 -> 2 三角组步进（含结算）-> 3 垃圾子投放(1-4) -> 4 胜负判定(1-5)
    /// </summary>
    public void Tick(int absFrame, IList<SlInput> inputs)
    {
        if (GameOver) return;
        LastTickFrame = absFrame;

        if (inputs != null)
            for (int i = 0; i < inputs.Count; i++)
                ApplyInput(inputs[i]);

        for (int i = 0; i < Players.Count; i++)
        {
            StepGroup(absFrame, Players[i]);
            Players[i].MoveLeftHeld = false;  // 长按均为帧内瞬时标志
            Players[i].MoveRightHeld = false;
            Players[i].FastDropHeld = false;
        }

        // TODO(1-4) 3. 垃圾子投放
        // TODO(1-5) 4. 胜负判定
    }

    private void ApplyInput(SlInput input)
    {
        if (input.PlayerIndex < 0 || input.PlayerIndex >= Players.Count)
        {
            LastReject = RejectReason.BadPlayerIndex;
            return;
        }

        var p = Players[input.PlayerIndex];
        switch (input.OpType)
        {
            case SlOp.MoveLeft:
                p.MoveLeftHeld = true;
                break;
            case SlOp.MoveRight:
                p.MoveRightHeld = true;
                break;
            case SlOp.RotateCW:
                if (TryRotate(p, 1))
                    ReevaluateContact(p);
                break;
            case SlOp.RotateCCW:
                if (TryRotate(p, 5))
                    ReevaluateContact(p);
                break;
            case SlOp.FastDrop:
                p.FastDropHeld = true;
                break;
        }
    }

    /// <summary>FNV-1a 64。新字段只允许追加到方法尾部，禁止重排已有混合顺序</summary>
    public ulong StateHash()
    {
        var h = 14695981039346656037UL;
        Mix(ref h, LastTickFrame);
        Mix(ref h, GameOver ? 1L : 0L);

        Mix(ref h, SharedSeq.Count);
        for (int i = 0; i < SharedSeq.Count; i++)
            Mix(ref h, SharedSeq[i]);

        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            Mix(ref h, p.PlayerIndex);
            for (int c = 0; c < TotalCells; c++)
                Mix(ref h, p.Board.Cells[c]);
            Mix(ref h, p.SeqPos);
            Mix(ref h, p.Tri.Colors[0]);
            Mix(ref h, p.Tri.Colors[1]);
            Mix(ref h, p.Tri.Colors[2]);
            Mix(ref h, p.Tri.X.Raw);
            Mix(ref h, p.Tri.Y.Raw);
            Mix(ref h, (int)p.Tri.State);
            Mix(ref h, p.Tri.Rotation);
            Mix(ref h, p.RestEndFrame);
        }

        // TODO(1-3 起) 消除/连锁/垃圾子状态按固定序追加
        return h;
    }

    private static void Mix(ref ulong h, long v)
    {
        h = (h ^ (ulong)v) * 1099511628211UL;
    }
}
