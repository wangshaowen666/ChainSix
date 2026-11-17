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
/// Group = 三角组状态机；Settle = 物理沉降；Match = 消除与连锁；Presentation = 确定性轨迹。
/// </summary>
public partial class SixLineLogic
{
    public const int LogicFrameMs = 50;
    public const int PlayerCount = 2;
    public const int PreviewCount = 1;
    public const int EmptyColor = 0;
    public const int ColorMin = 1;
    public const int MaxSettleRounds = 128;

    public cfg.DSixLineRule Rules { get; }
    public int Rows => Rules.Rows;
    public int Columns => Rules.Columns;
    public int DangerRows => Rules.DangerRows;
    public int ColorMax => Rules.ColorCount;
    public int SlotCount => Columns - 1;
    public int ClearCount => Rules.ClearCount;
    public int GraceFrames => Rules.GraceFrames;
    public int TotalCells => RowStart(Rows);
    public Fix MoveSpeed { get; }
    public Fix SlowFallSpeed { get; }
    public Fix FastFallSpeed { get; }
    private static readonly Fix BallTouch = Fix.FromDouble(0.96);
    private static readonly Fix BallTouchSq = BallTouch * BallTouch;

    // ---- 对局状态 ----

    public readonly List<Player> Players = new List<Player>(PlayerCount);

    public bool GameOver { get; private set; }
    public int LastTickFrame { get; private set; }
    /// <summary>未结束或平局为 -1，由 GameOver 区分</summary>
    public int WinnerIndex { get; private set; } = -1;

    /// <summary>最近一次被拒原因（诊断用，不进 StateHash）</summary>
    public RejectReason LastReject { get; private set; }

    private readonly XRng _rng;
    private readonly byte[] _elim;
    private readonly byte[] _visited;
    private readonly int[] _stack;
    private readonly int[] _cluster;
    private readonly int[] _rowOf;
    private readonly int[] _colOf;

    public SixLineLogic(long seed, cfg.DSixLineRule rules)
    {
        ValidateRules(rules);
        Rules = rules;
        MoveSpeed = Fix.FromInt(rules.MoveSpeedMilli) / Fix.FromInt(1000);
        SlowFallSpeed = Fix.FromInt(rules.SlowFallSpeedMilli) / Fix.FromInt(1000);
        FastFallSpeed = Fix.FromInt(rules.FastFallSpeedMilli) / Fix.FromInt(1000);
        _rng = new XRng((ulong)seed ^ 0xD1B54A32D192ED03UL);
        _elim = new byte[TotalCells];
        _visited = new byte[TotalCells];
        _stack = new int[TotalCells];
        _cluster = new int[TotalCells];
        _rowOf = new int[TotalCells];
        _colOf = new int[TotalCells];

        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < RowCells(r); c++)
            {
                var idx = RowStart(r) + c;
                _rowOf[idx] = r;
                _colOf[idx] = c;
            }

        for (int i = 0; i < PlayerCount; i++)
        {
            // 两个同种子流代表同一条出子序列，各自只保存预览，避免长局缓存扩容。
            var p = new Player(TotalCells, Columns, (ulong)seed) { PlayerIndex = i };
            for (int c = 0; c < p.PreviewColors.Length; c++)
                p.PreviewColors[c] = p.PieceRng.NextInt(ColorMin, ColorMax + 1);
            DrawNext(p);
            Players.Add(p);
        }
    }

    /// <summary>
    /// 推进一逻辑帧。步骤顺序即确定性本身，禁止调整：
    /// 1 应用输入 -> 2 双方落子结算 -> 3 汇总攻击并投放 -> 4 同帧胜负 -> 5 记录固定结算窗口，窗口结束后出下一组
    /// </summary>
    public void Tick(int absFrame, IList<SlInput> inputs)
    {
        if (GameOver) return;
        LastTickFrame = absFrame;
        LastReject = RejectReason.None;
        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            p.LockedThisFrame = false;
            p.LastSettleChain = p.LastSettleClusters = p.RoundCount = 0;
            p.SentThisFrame = p.ReceivedThisFrame = 0;
            if (p.Tri.State == PieceState.None && absFrame >= p.ResolveUntilFrame)
                DrawNext(p);
        }

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

        ResolveGarbage();
        CheckGameOver();
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].LockedThisFrame)
                Players[i].ResolveUntilFrame = absFrame + Players[i].VisualEndFrame + 2;
    }

    private void ApplyInput(SlInput input)
    {
        if (input.PlayerIndex < 0 || input.PlayerIndex >= Players.Count)
        {
            LastReject = RejectReason.BadPlayerIndex;
            return;
        }

        var p = Players[input.PlayerIndex];
        if (p.Tri.State == PieceState.None)
            return;
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

    /// <summary>FNV-1a 64，覆盖规则、随机流、待投垃圾及预览；新增字段在尾部追加</summary>
    public ulong StateHash()
    {
        var h = 14695981039346656037UL;
        Mix(ref h, LastTickFrame);
        Mix(ref h, GameOver ? 1L : 0L);

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

        Mix(ref h, WinnerIndex);
        Mix(ref h, unchecked((long)_rng.State));
        Mix(ref h, Rules.Id);
        Mix(ref h, Rows);
        Mix(ref h, Columns);
        Mix(ref h, DangerRows);
        Mix(ref h, ColorMax);
        Mix(ref h, ClearCount);
        Mix(ref h, GraceFrames);
        Mix(ref h, MoveSpeed.Raw);
        Mix(ref h, SlowFallSpeed.Raw);
        Mix(ref h, FastFallSpeed.Raw);
        Mix(ref h, Rules.SingleGarbage);
        Mix(ref h, Rules.DoubleGarbage);
        Mix(ref h, Rules.ChainGarbage);
        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            Mix(ref h, unchecked((long)p.PieceRng.State));
            for (int c = 0; c < p.PreviewColors.Length; c++)
                Mix(ref h, p.PreviewColors[c]);
            Mix(ref h, p.PendingGarbage);
            Mix(ref h, p.Overflow ? 1 : 0);
            Mix(ref h, p.Lost ? 1 : 0);
            Mix(ref h, p.LockedThisFrame ? 1 : 0);
            Mix(ref h, p.LastSettleChain);
            Mix(ref h, p.LastSettleClusters);
            Mix(ref h, p.SentThisFrame);
            Mix(ref h, p.ReceivedThisFrame);
            Mix(ref h, p.TotalSent);
            Mix(ref h, p.TotalReceived);
            Mix(ref h, p.RoundCount);
            for (int r = 0; r < p.RoundCount; r++)
            {
                Mix(ref h, p.RoundClusters[r]);
                Mix(ref h, p.RoundCells[r]);
                Mix(ref h, p.RoundSpecial[r]);
                Mix(ref h, p.RoundPatterns[r]);
            }
        }
        Mix(ref h, RowPitch.Raw);
        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            Mix(ref h, p.ResolveUntilFrame);
            Mix(ref h, p.NextPieceId);
            Mix(ref h, p.Tri.RotationTurns);
            for (int c = 0; c < TotalCells; c++)
                Mix(ref h, p.Board.PieceIds[c]);
            for (int c = 0; c < 3; c++)
                Mix(ref h, p.Tri.PieceIds[c]);
        }
        return h;
    }

    private static void ValidateRules(cfg.DSixLineRule rules)
    {
        if (rules == null || rules.Rows < 6 || rules.Rows > 32 || rules.Columns < 6 || rules.Columns > 20
            || rules.DangerRows < 1 || rules.DangerRows >= rules.Rows - 2
            || rules.ColorCount < 2 || rules.ColorCount > 5 || rules.ClearCount < 5 || rules.ClearCount > 6
            || rules.GraceFrames < 0 || rules.MoveSpeedMilli < 1 || rules.MoveSpeedMilli > 1000
            || rules.SlowFallSpeedMilli < 1 || rules.SlowFallSpeedMilli > 1000
            || rules.FastFallSpeedMilli < rules.SlowFallSpeedMilli || rules.FastFallSpeedMilli > 1000
            || rules.SingleGarbage < 0 || rules.DoubleGarbage < rules.SingleGarbage || rules.ChainGarbage < 0
            || rules.DoubleGarbage > 1000 || rules.ChainGarbage > 1000)
            throw new System.ArgumentException("六连珠规则配置超出灰盒支持范围", nameof(rules));
    }

    private static void Mix(ref ulong h, long v)
    {
        h = (h ^ (ulong)v) * 1099511628211UL;
    }
}
