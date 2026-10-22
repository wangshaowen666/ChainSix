/*--------------------------------------------------------------
 * File: SixLineLogic.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;

/// <summary>
/// 六连珠确定性逻辑层（纯 C#，无 UnityEngine）：
/// - 双棋盘对战：圆形棋子蜂窝错行摆放（相邻行错开半格，结构上禁止上下对齐），
///   每组正三角三连出生即持续下落（▲上1下2 / ▽上2下1，旋转 = 绕三球中心 60° 刚体旋转，
///   颜色随旋转轮换：▲(顶A/左下B/右下C) 顺时针一次 → ▽(上B/A，下C)），
///   同色连通块 ≥6（六邻接相连，任意形状）消除；6 种特殊图形（横/右斜/左斜直线 6 连、
///   上1中2下3、上3中2下1 金字塔、上2/下2/中左/中右 同色六球环）触发该色全消 + 向对方投垃圾子（1-4），
///   堆过高度线即负
/// - 横移：长按连续平移，按三球圆形外形与已占格做球面贴面（贴住即停，不关注落点格位/奇偶、不收敛半格）；
///   左右墙只限横移范围，不影响下落状态、不触发吸附窗口
/// - 组状态机：Falling 刚性下落 -> 真实接触（整组到达最深可停位，几何非法的相位行直接路过）->
///   Resting 可沉降（GraceFrames 容错内可继续旋转/移动，脱离接触即回 Falling；计时到/按住加速 ->
///   一次性结算：入板 + 同帧完整沉降（坠落嵌V滚落一遍到底）+ 消除连锁，无二段式沉降）
/// - Tick(absFrame, inputs) 以绝对帧号锚定：同帧号 + 同操作序列 => 任意端状态完全一致
/// - 构造传入对局种子（阶段 1 本地生成；阶段 3 联机由 StartGamePush 下发，双端一致）
/// - 双驱动零改动：人机/本地双人 = SlLocalDriver（已接入），好友对战 = FrameSyncMgr（阶段 3）
/// - 确定性纪律：禁止 float/double、UnityEngine.Random/System.Random、Time.*、Dictionary 遍历、
///   Tick 内 new/装箱/LINQ/字符串拼接（数值全走 Fix + XRng，拒绝原因用枚举不用字符串）
///
/// 文件拆分（partial，按职责；不改任何成员可见性与调用顺序，StateHash 逐帧结果不变）：
/// - SixLineLogic.cs        本文件：规则常量、对局状态、构造、Tick 编排、输入应用、StateHash
/// - SixLineLogic.Board.cs  棋盘几何（蜂窝下标/六邻接/占位与落点查询）+ 数据结构（Board/Player/Triomino）
/// - SixLineLogic.Group.cs  三角组状态机（旋转/横移贴面/下落/接触/结算入板）+ 出子序列
/// - SixLineLogic.Settle.cs 物理沉降（坠落 + 嵌 V + 滚落）
/// - SixLineLogic.Match.cs  消除与连锁（特殊图形 + 连通块 + 递归沉降）
/// </summary>
public partial class SixLineLogic
{
    // ---- 规则常量（1-8 迁入 Luban 调参表，先以常量跑通灰盒）----
    /// <summary>逻辑帧间隔（毫秒），与 FrameSyncMgr.LogicFrameMs 一致（50ms = 20 帧/秒）</summary>
    public const int LogicFrameMs = 50;

    public const int Rows = 12;         // 行数（row 0 = 底部）
    public const int DangerRows = 2;    // 顶部危险区行数（堆入即负，1-5 判定）
    public const int PlayerCount = 2;   // 对战人数（本地双人/人机/联机同构）
    public const int PreviewCount = 3;  // 出子队列预览组数
    public const int EmptyColor = 0;    // 空格
    public const int ColorMin = 1;      // 最小颜色 id
    public const int ColorMax = 5;      // 最大颜色 id（含），共 5 色（颜色数是难度总开关）
    public const int SlotCount = 9;     // 横向相位数（奇行锚点列 0..8）
    public const int ClearCount = 6;         // 消除所需同色连线数（横/右斜/左斜三轴，≥ClearCount 即消）
    public const int MaxSettleRounds = 20;   // 单次结算轮数上限（每轮至少消 6 格，114 格板不可能触顶，防御用）
    public const int GraceFrames = 10;       // 可沉降容错窗口（帧）：真实接触后可继续旋转/移动，0.5s @ 50ms（1-8 迁表）

    /// <summary>左右平移速度（列/逻辑帧）：按住期间连续 Fix 积分（0.25 列/帧 = 5 列/秒，1-8 迁表调参）</summary>
    public static readonly Fix MoveSpeed = Fix.FromDouble(0.25);

    /// <summary>常速下落（格/帧）：0.08 格/帧 = 1.6 格/秒</summary>
    public static readonly Fix SlowFallSpeed = Fix.FromDouble(0.08);

    /// <summary>加速下落（格/帧）：按住加速抢节奏用，1 格/帧 = 20 格/秒</summary>
    public static readonly Fix FastFallSpeed = Fix.FromInt(1);

    /// <summary>球直径（格单位，= 渲染圆 cell*0.96）：两球中心距 &lt; 此值即重叠，横移按此球面贴住即停</summary>
    private static readonly Fix BallTouch = Fix.FromDouble(0.96);
    private static readonly Fix BallTouchSq = BallTouch * BallTouch;

    // ---- 对局状态 ----

    public readonly List<Player> Players = new List<Player>(PlayerCount);

    /// <summary>
    /// 共享出子序列（双方同一条队列）：第 N 组（3 色）双方一致，仅因下落/消除进度不同而画面不同步；
    /// 懒生成（EnsureSeq 按消费推进追加），追加顺序 = DrawNext 调用顺序（同输入下确定）
    /// </summary>
    public readonly List<int> SharedSeq = new List<int>(64);

    public bool GameOver { get; private set; }
    public int LastTickFrame { get; private set; }

    /// <summary>最近一次被拒操作原因（诊断用，不影响确定性）</summary>
    public RejectReason LastReject { get; private set; }

    private readonly XRng _rng;
    private readonly byte[] _elim = new byte[TotalCells];      // 消除标记（扫描复用，ApplyElim 时清零）
    private readonly byte[] _visited = new byte[TotalCells];   // 连通块 flood fill 访问标记（每轮清零）
    private readonly int[] _stack = new int[TotalCells];       // flood fill 栈
    private readonly int[] _cluster = new int[TotalCells];     // 当前连通块成员
    private readonly int[] _rowOf = new int[TotalCells];       // 扁平下标 -> 行（扫描/沉降游走用）
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
            DrawNext(p); // 首组（出生即下落；双方从共享序列同一下标起取，首组一致）
            Players.Add(p);
        }
    }

    /// <summary>
    /// 推进一逻辑帧（帧号从 1 连续递增，空帧也要 tick）。步骤固定序即确定性本身，
    /// 后续任务按下列编排填实现，禁止调整步骤顺序：
    /// 1. 应用输入（长按移动/加速、点按旋转）
    /// 2. 三角组步进：下落/可沉降状态机（真实接触进容错 -> 容错到期或按住加速一次性结算：
    ///    入板 + 同帧完整沉降 + 消除连锁 -> 下一组出生）
    /// 3. 垃圾子投放（向对方棋盘投随机色子，单线 3/双线十字 5/连锁每级 +2）(1-4)
    /// 4. 胜负判定（任一棋盘堆过高度线 GameOver，区分胜负方）(1-5)
    /// </summary>
    public void Tick(int absFrame, IList<SlInput> inputs)
    {
        if (GameOver) return;
        LastTickFrame = absFrame;

        // 1. 应用输入（按帧内顺序，双端一致）
        if (inputs != null)
            for (int i = 0; i < inputs.Count; i++)
                ApplyInput(inputs[i]);

        // 2. 三角组步进（结算在组内触发时同帧完成，双端处理序固定即确定）
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
            case SlOp.MoveLeft: // 长按：按住期间每帧发送
                p.MoveLeftHeld = true;
                break;
            case SlOp.MoveRight:
                p.MoveRightHeld = true;
                break;
            case SlOp.RotateCW: // 点按：60° 顺时针（▲/▽ 互转 + 颜色轮换）
                if (TryRotate(p, 1))
                    ReevaluateContact(p); // 可沉降态旋转后重判接触（可能脱离回下落）
                break;
            case SlOp.RotateCCW: // 点按：60° 逆时针
                if (TryRotate(p, 5))
                    ReevaluateContact(p);
                break;
            case SlOp.FastDrop: // 长按：按住期间每帧发送
                p.FastDropHeld = true;
                break;
        }
    }

    /// <summary>
    /// 全量状态哈希（FNV-1a 64）：双端/同种子重放逐帧对比此值验证确定性。
    /// 新增哈希字段只允许追加到方法尾部，禁止重排已有混合顺序
    /// </summary>
    public ulong StateHash()
    {
        var h = 14695981039346656037UL;
        Mix(ref h, LastTickFrame);
        Mix(ref h, GameOver ? 1L : 0L);

        // 共享出子序列（一次性混合：长度 + 内容序）
        Mix(ref h, SharedSeq.Count);
        for (int i = 0; i < SharedSeq.Count; i++)
            Mix(ref h, SharedSeq[i]);

        // 1-2 玩家状态（玩家序 -> 行优先格子序 -> 出子游标 -> 三角组字段序）
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
            Mix(ref h, p.Tri.X.Raw);    // 连续列位（量化槽位 Slot 派生自 X）
            Mix(ref h, p.Tri.Y.Raw);
            Mix(ref h, (int)p.Tri.State);
            Mix(ref h, p.Tri.Rotation);  // 1-2 追加：旋转状态
            Mix(ref h, p.RestEndFrame);  // 状态机改造：可沉降容错截止帧
        }

        // TODO(1-3 起) 消除/连锁/垃圾子等状态按固定序追加
        return h;
    }

    private static void Mix(ref ulong h, long v)
    {
        h = (h ^ (ulong)v) * 1099511628211UL;
    }
}
