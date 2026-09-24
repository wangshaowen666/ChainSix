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
/// - 组状态机：Falling 刚性下落 -> 真实接触（整组到达最深合法量化位，必有球贴住地板/山谷）->
///   Resting 可沉降（GraceFrames 容错内可继续旋转/移动，脱离接触即回 Falling；计时到/按住加速 ->
///   一次性结算：入板 + 同帧完整沉降（坠落嵌V滚落一遍到底）+ 消除连锁，无二段式沉降）
/// - Tick(absFrame, inputs) 以绝对帧号锚定：同帧号 + 同操作序列 => 任意端状态完全一致
/// - 构造传入对局种子（阶段 1 本地生成；阶段 3 联机由 StartGamePush 下发，双端一致）
/// - 双驱动零改动：人机/本地双人 = SlLocalDriver（已接入），好友对战 = FrameSyncMgr（阶段 3）
/// - 确定性纪律：禁止 float/double、UnityEngine.Random/System.Random、Time.*、Dictionary 遍历、
///   Tick 内 new/装箱/LINQ/字符串拼接（数值全走 Fix + XRng，拒绝原因用枚举不用字符串）
/// </summary>
public class SixLineLogic
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
    public const int GraceFrames = 40;       // 可沉降容错窗口（帧）：真实接触后可继续旋转/移动，2s @ 50ms 慢放（1-8 迁表，正式 0.5s = 10）

    /// <summary>左右平移速度（列/逻辑帧）：按住期间连续 Fix 积分（0.25 列/帧 = 5 列/秒，1-8 迁表调参）</summary>
    public static readonly Fix MoveSpeed = Fix.FromDouble(0.25);
    private static readonly Fix Half = Fix.FromDouble(0.5);

    /// <summary>常速下落（格/帧）：0.08 格/帧 = 1.6 格/秒</summary>
    public static readonly Fix SlowFallSpeed = Fix.FromDouble(0.08);

    /// <summary>加速下落（格/帧）：按住加速抢节奏用，1 格/帧 = 20 格/秒</summary>
    public static readonly Fix FastFallSpeed = Fix.FromInt(1);

    // ---- 蜂窝错行几何（偶数行 10 格中心 x=col+0.5，奇数行 9 格中心 x=col+1，错开半格）----

    public static bool RowIsWide(int row) => (row & 1) == 0;
    public static int RowCells(int row) => RowIsWide(row) ? 10 : 9;

    /// <summary>行首扁平下标（偶行 10 格 + 奇行 9 格 = 每两行 19 格，共 114 格）</summary>
    public static int RowStart(int row) => (row >> 1) * 19 + (RowIsWide(row) ? 0 : 10);
    public const int TotalCells = 114;

    /// <summary>
    /// 六邻接扁平下标；-1 = 越界。dir：0=右 1=左 2=右上 3=左上 4=右下 5=左下。
    /// 蜂窝错行斜邻：偶行 (r,c) = 奇行 (r±1, c) [右] / (r±1, c-1) [左]；
    /// 奇行 (r,c) = 偶行 (r±1, c+1) [右] / (r±1, c) [左]
    /// </summary>
    public static int Neighbor(int row, int col, int dir)
    {
        switch (dir)
        {
            case 0: return col + 1 < RowCells(row) ? RowStart(row) + col + 1 : -1;
            case 1: return col - 1 >= 0 ? RowStart(row) + col - 1 : -1;
            case 2: // 右上
            case 3: // 左上
            case 4: // 右下
            case 5: // 左下
                var nr = dir <= 3 ? row + 1 : row - 1;
                if (nr < 0 || nr >= Rows) return -1;
                int nc;
                if (RowIsWide(row))
                    nc = dir == 2 || dir == 4 ? col : col - 1;
                else
                    nc = dir == 2 || dir == 4 ? col + 1 : col;
                return nc >= 0 && nc < RowCells(nr) ? RowStart(nr) + nc : -1;
            default: return -1;
        }
    }

    /// <summary>当前子状态</summary>
    public enum PieceState : byte
    {
        None,    // 无组（结算完成到下一组出生的瞬时态）
        Falling, // 下落中（出生即下落，玩家移动/旋转/加速干预）
        Resting, // 可沉降（已真实接触：容错期内可旋转/移动，脱离接触回 Falling，计时到一次性结算）
    }

    /// <summary>操作被拒原因（枚举替代字符串保证 Tick 零 GC；文案由表现层组装）</summary>
    public enum RejectReason : byte
    {
        None = 0,
        BadPlayerIndex,  // 玩家下标非法
        RotationBlocked, // 旋转目标格被占
        MoveBlocked,     // 移动目标量化位与棋子重叠
    }

    /// <summary>
    /// 下落三角组：Colors = 原始 ▲ 槽位色 [A=顶, B=左下, C=右下]；Rotation 0..5（偶=▲，奇=▽），
    /// 旋转 = 绕三球中心 60° 刚体旋转的格点量化（形状 ▲↔▽ 互转，颜色随旋转轮换），
    /// 颜色到足迹格的映射见 OddLeftColor/OddRightColor/EvenLeftColor/EvenRightColor。
    /// X = 连续列位锚点（量化槽位 Slot 派生）；Y = 下排（偶行）所在行坐标（连续）
    /// </summary>
    public class Triomino
    {
        public readonly int[] Colors = new int[3];
        public int Rotation;

        /// <summary>连续列位（按住左右平移时 Fix 积分）；量化槽位 = Slot 派生属性</summary>
        public Fix X;
        public Fix Y;
        public PieceState State;

        /// <summary>量化槽位（碰撞/落点/结算按列，half-up 取整）</summary>
        public int Slot => RoundSlot(X);
    }

    /// <summary>
    /// 旋转状态 -> 颜色槽映射（元素 = Colors 下标 0/1/2 = A/B/C，-1 = 该姿态不占用此格）。
    /// 足迹：▲（偶行对 @EvenLeft/EvenRight + 上方奇行单 @OddLeft）；▽（上方奇行对 @OddLeft/OddRight + 下方偶行单 @EvenRight）。
    /// 单球（顶点）列随所在行奇偶取 s/s+1（见 SingleCol），渲染恒居中于其配对之下
    /// </summary>
    public static readonly int[] OddLeftColor = { 0, 1, 1, 2, 2, 0 };
    public static readonly int[] OddRightColor = { -1, 0, -1, 1, -1, 2 };
    public static readonly int[] EvenLeftColor = { 1, -1, 2, -1, 0, -1 };
    public static readonly int[] EvenRightColor = { 2, 2, 0, 0, 1, 1 };

    /// <summary>
    /// 单球（顶点）列：随所在行奇偶取 slot（奇行=山谷位）或 slot+1（偶行=头顶位）。
    /// 蜂窝错行渲染下两种行奇偶的顶点都居中于其配对之下（真蜂窝竖直下落的锯齿列）
    /// </summary>
    public static int SingleCol(int row, int slot) => (row & 1) == 1 ? slot : slot + 1;

    /// <summary>连续列位 -> 量化槽位（half-up：x+0.5 取整；X 恒 &gt;= 0，截断即 floor）</summary>
    private static int RoundSlot(Fix x) => (x + Half).Int;

    /// <summary>棋盘：蜂窝错行扁平格子（行优先，下标经 RowStart/RowCells 换算）</summary>
    public class Board
    {
        public readonly int[] Cells = new int[TotalCells];
        public int Get(int row, int col) => Cells[RowStart(row) + col];
        public void Set(int row, int col, int color) => Cells[RowStart(row) + col] = color;
    }

    /// <summary>参战方（双方各一块棋盘）</summary>
    public class Player
    {
        public int PlayerIndex;
        public readonly Board Board = new Board();
        public int SeqPos; // 出子游标：下一个待取颜色在共享序列 SharedSeq 中的下标（双方同队列、各自推进）
        public readonly Triomino Tri = new Triomino();
        public bool MoveLeftHeld;  // 本帧长按标志（应用输入置位，下落步消费后复位）
        public bool MoveRightHeld;
        public bool FastDropHeld;

        // 消除结算结果（每 Tick 步骤 3 重置；1-4 垃圾公式与 1-6 HUD 消费）
        public int LastSettleChain; // 本 Tick 结算的连锁轮数（0 = 无消除；首轮消除即 1 连锁）
        public int LastSettleClusters; // 本 Tick 结算的普通消除连通块总数（≥6 同色相连）
        public readonly int[] RoundClusters = new int[MaxSettleRounds]; // 每轮普通消除连通块数
        public readonly int[] RoundCells = new int[MaxSettleRounds];    // 每轮消除格数（含特殊全消）
        public readonly int[] RoundSpecial = new int[MaxSettleRounds];  // 每轮特殊消除颜色 bitmask（bit = 颜色 id-1；1-4 消费）
        public int RoundCount;

        public int RestEndFrame; // 可沉降容错截止帧（真实接触帧 + GraceFrames；按住加速可提前结算）
    }

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
    /// 旋转 60°（Rotation + step mod 6）：目标足迹在棋盘内的格须全为空才生效，否则拒绝（不做踢墙）。
    /// ▽ 的奇行对占 col s/s+1，最右只能到 slot 7（奇行 9 格）；
    /// 行 ≥ Rows（棋盘顶之上）视为空；基准行取 Y 截断（Fix.Int，纯整数确定性）。
    /// 表现层按理想 60° 位渲染（与本足迹横移半格，见 SixLineView），落定时半格吸附归位
    /// </summary>
    private bool TryRotate(Player p, int step)
    {
        var tri = p.Tri;
        var nr = (tri.Rotation + step) % 6;
        var s = tri.Slot;

        if ((nr & 1) == 1 && s > SlotCount - 2)
        {
            LastReject = RejectReason.RotationBlocked; // ▽ 奇行对越右墙
            return false;
        }

        var y = tri.Y.Int;

        var ok = SlotUsable(p.Board, y + 1, s);                  // 奇行左格（▲ 顶点 / ▽ 左上，都占）
        if (ok && (nr & 1) == 0)
            ok = SlotUsable(p.Board, y, s) && SlotUsable(p.Board, y, s + 1)
                && SlotUsable(p.Board, y + 1, SingleCol(y + 1, s));        // ▲ 偶行对 + 顶点
        else if (ok)
            ok = SlotUsable(p.Board, y + 1, s + 1) && SlotUsable(p.Board, y, SingleCol(y, s)); // ▽ 奇行右格 + 顶点

        if (!ok)
        {
            LastReject = RejectReason.RotationBlocked;
            return false;
        }

        tri.Rotation = nr;
        return true;
    }

    /// <summary>
    /// 三角组步进（下落/可沉降状态机）：
    /// - 长按左右移动（间隔步进）：目标量化位与棋子重叠则拒绝本次移动
    /// - Falling：持续下落，到达最深合法量化位（必有球真实贴住地板/山谷）转 Resting 并启动容错计时
    /// - Resting：容错到期或按住加速 => 一次性结算；移动/旋转后脱离接触（ReevaluateContact）则回 Falling
    /// </summary>
    private void StepGroup(int absFrame, Player p)
    {
        var tri = p.Tri;
        if (tri.State == PieceState.None)
            return;

        // 左右平移：按住按 MoveSpeed 连续积分（丝滑无格跳）；量化列位变化时校验目标列，
        // 被挡则钳到当前列边界（半格贴靠，量化列位不变），墙界钳到 [0, maxSlot]
        var dir = 0;
        if (p.MoveLeftHeld)
            dir = -1;
        else if (p.MoveRightHeld)
            dir = 1;

        if (dir != 0)
        {
            var step = dir > 0 ? MoveSpeed : -MoveSpeed;
            var qOld = tri.Slot;
            var newX = tri.X + step;
            var qNew = RoundSlot(newX);

            if (qNew != qOld && !PlacementFree(p.Board, tri.Rotation, qNew, tri.Y.Int))
            {
                // 目标列被挡：钳回当前列边界（半格贴靠，量化列位不变）
                newX = dir > 0
                    ? Fix.FromInt(qOld) + Half - Fix.FromRaw(1)
                    : Fix.FromInt(qOld) - Half;
                LastReject = RejectReason.MoveBlocked;
            }

            var maxSlot = SlotCount - 1 - (tri.Rotation & 1);
            if (newX < Fix.FromInt(0))
                newX = Fix.FromInt(0);
            if (newX > Fix.FromInt(maxSlot))
                newX = Fix.FromInt(maxSlot);

            tri.X = newX;

            if (tri.Slot != qOld)
                ReevaluateContact(p); // 量化列位变化：重判接触（可沉降态可能脱离回下落态）
        }

        if (tri.State == PieceState.Falling)
        {
            tri.Y -= p.FastDropHeld ? FastFallSpeed : SlowFallSpeed;
            var stop = DeepestStop(p.Board, tri.Rotation, tri.Slot, tri.Y.Int);
            if (tri.Y <= Fix.FromInt(stop))
            {
                tri.Y = Fix.FromInt(stop);
                tri.State = PieceState.Resting;
                p.RestEndFrame = absFrame + GraceFrames; // 真实接触：进入容错期
            }
            return;
        }

        // Resting：容错到期或按住加速 => 一次性结算（入板 + 完整沉降 + 连锁 + 下一组）
        if (absFrame >= p.RestEndFrame || p.FastDropHeld)
            Collapse(p);
    }

    /// <summary>移动/旋转后重判接触：当前位仍为最深合法位 = 仍接触保持 Resting；出现更深合法位 = 脱离回 Falling（容错作废，再接触重新计时）</summary>
    private void ReevaluateContact(Player p)
    {
        var tri = p.Tri;
        if (tri.State != PieceState.Resting)
            return;
        if (DeepestStop(p.Board, tri.Rotation, tri.Slot, tri.Y.Int) < tri.Y.Int)
            tri.State = PieceState.Falling;
    }

    /// <summary>格位可用：行 ≥ Rows 视为空（棋盘顶之上）；列越界或已占视为不可用</summary>
    private static bool SlotUsable(Board b, int row, int col)
    {
        if (row >= Rows)
            return true;
        return col >= 0 && col < RowCells(row) && b.Get(row, col) == EmptyColor;
    }

    /// <summary>
    /// 组在基准行 k（下排行）的量化格位是否全部合法空置。▲：偶行对 (k,s)(k,s+1) + 顶点 (k+1,SingleCol)；
    /// ▽：奇行对 (k+1,s)(k+1,s+1) + 顶点 (k,SingleCol)（列随行奇偶，渲染恒居中）
    /// </summary>
    private static bool PlacementFree(Board b, int rotation, int slot, int k)
    {
        if (k < 0)
            return false;
        if ((rotation & 1) == 0)
            return SlotUsable(b, k, slot) && SlotUsable(b, k, slot + 1) && SlotUsable(b, k + 1, SingleCol(k + 1, slot));
        return SlotUsable(b, k + 1, slot) && SlotUsable(b, k + 1, slot + 1) && SlotUsable(b, k, SingleCol(k, slot));
    }

    /// <summary>
    /// 最深合法量化基准行：从 fromK 起逐行向下，首个被挡行之上即停（挡格必为组内某球的山谷贴位或地板，
    /// 即真实接触）；fromK 自身被占（防御，如出生位被堆顶侵入）则向上找最近合法位
    /// </summary>
    private static int DeepestStop(Board b, int rotation, int slot, int fromK)
    {
        var k = fromK < 0 ? 0 : fromK;
        while (k < Rows && !PlacementFree(b, rotation, slot, k))
            k++;
        while (k > 0 && PlacementFree(b, rotation, slot, k - 1))
            k--;
        return k;
    }

    /// <summary>
    /// 一次性结算（容错到期/按住加速触发）：整组在当前量化位写入棋盘（行 ≥ Rows 不写入，
    /// 顶部溢出由 1-5 判负收尾）-> 同帧完整沉降（坠落+嵌V+滚落一遍到底，无二段式）->
    /// 消除连锁递归到稳定 -> 下一组出生
    /// </summary>
    private void Collapse(Player p)
    {
        var tri = p.Tri;
        var r = tri.Rotation;
        var s = tri.Slot;
        var k = tri.Y.Int;
        if ((r & 1) == 0) // ▲：偶行对 + 顶点（列随奇偶，渲染恒居中）
        {
            Place(p, k, s, tri.Colors[EvenLeftColor[r]]);
            Place(p, k, s + 1, tri.Colors[EvenRightColor[r]]);
            Place(p, k + 1, SingleCol(k + 1, s), tri.Colors[OddLeftColor[r]]);
        }
        else // ▽：奇行对 + 顶点（列随奇偶，渲染恒居中）
        {
            Place(p, k + 1, s, tri.Colors[OddLeftColor[r]]);
            Place(p, k + 1, s + 1, tri.Colors[OddRightColor[r]]);
            Place(p, k, SingleCol(k, s), tri.Colors[EvenRightColor[r]]);
        }

        tri.State = PieceState.None;
        SettlePass(p); // 入板沉降：坠落 + 嵌 V + 滚落一遍到底
        SettleChains(p);
        DrawNext(p);
    }

    private static void Place(Player p, int row, int col, int color)
    {
        if (row < Rows && color >= 0)
            p.Board.Set(row, col, color);
    }

    // ---- 消除与连锁（1-3）----

    /// <summary>
    /// 消除与连锁：特殊图形检测 + 普通连通块扫描（≥ClearCount 同色相连标记）-> 消除 ->
    /// 沉降（坠落+嵌V+滚落一遍到底）-> 复扫递归到稳定。每轮消除连锁 +1（首轮即 1 连锁），
    /// 结果写入 Player（1-4 垃圾公式消费）。仅在入板/消除触发时运行
    /// </summary>
    private void SettleChains(Player p)
    {
        p.LastSettleChain = 0;
        p.LastSettleClusters = 0;
        p.RoundCount = 0;

        while (p.RoundCount < MaxSettleRounds)
        {
            var special = ScanSpecial(p);   // 特殊图形：命中色全板标记进 _elim，返回颜色 bitmask
            var clusters = ScanClusters(p); // 普通 ≥6 同色连通块标记进 _elim
            var cells = ApplyElim(p);
            if (cells == 0)
                break;

            SettlePass(p); // 消除后沉降：坠落 + 嵌 V + 滚落一遍到底

            p.LastSettleChain++;
            p.LastSettleClusters += clusters;
            p.RoundClusters[p.RoundCount] = clusters;
            p.RoundCells[p.RoundCount] = cells;
            p.RoundSpecial[p.RoundCount] = special;
            p.RoundCount++;
        }
    }

    /// <summary>
    /// 特殊消除检测（6 种图形），命中色的全板格子标记进 _elim，返回命中颜色 bitmask（bit = 颜色 id-1）：
    /// 1-3 直线：横/右斜/左斜 ≥ClearCount 同色连直线；
    /// 4-5 金字塔：上1中2下3 / 上3中2下1（六球同色，紧凑对齐，宽窄行两相位）；
    /// 6 六边形环：上2/下2/中左/中右 同色 X（六球环抱），最中间格可空可任意色
    /// </summary>
    private int ScanSpecial(Player p)
    {
        var mask = 0;
        var cells = p.Board.Cells;

        // ---- 直线三轴（横/右斜/左斜）----
        for (int axis = 0; axis < 3; axis++)
        {
            var fwd = axis == 0 ? 0 : axis == 1 ? 4 : 5;
            var back = axis == 0 ? 1 : axis == 1 ? 3 : 2;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < RowCells(r); c++)
                {
                    var idx = RowStart(r) + c;
                    var color = cells[idx];
                    if (color == EmptyColor)
                        continue;

                    var b = Neighbor(r, c, back);
                    if (b >= 0 && cells[b] == color)
                        continue; // 非线首

                    var count = 1;
                    var cr = r;
                    var cc = c;
                    while (count < TotalCells) // 防御上限
                    {
                        var n = Neighbor(cr, cc, fwd);
                        if (n < 0 || cells[n] != color)
                            break;
                        count++;
                        cr = _rowOf[n];
                        cc = _colOf[n];
                    }

                    if (count >= ClearCount)
                        mask |= 1 << (color - 1);
                }
            }
        }

        // ---- 塔形（上1中2下3 / 上3中2下1）：3 连行 rr、中 2 行、单球行，六球同色 ----
        // 宽行 3 连 cols c..c+2 => 中 2 = 邻行 cols c..c+1、单 = 隔行 col c+1；
        // 窄行 3 连 cols c..c+2 => 中 2 = 邻行 cols c+1..c+2、单 = 隔行 col c+2
        for (int rr = 0; rr < Rows; rr++)
        {
            var wide = RowIsWide(rr);
            for (int c = 0; c + 2 < RowCells(rr); c++)
            {
                var x = cells[RowStart(rr) + c];
                if (x == EmptyColor || cells[RowStart(rr) + c + 1] != x || cells[RowStart(rr) + c + 2] != x)
                    continue;

                var m0 = wide ? c : c + 1;
                var s = wide ? c + 1 : c + 2;

                // 上1中2下3：中 2 在 rr+1、单在 rr+2（单球在上 = 行号更大）
                if (rr + 2 < Rows && Match2(cells, rr + 1, m0, x) && CellAt(cells, rr + 2, s) == x)
                    mask |= 1 << (x - 1);

                // 上3中2下1：中 2 在 rr-1、单在 rr-2
                if (rr - 2 >= 0 && Match2(cells, rr - 1, m0, x) && CellAt(cells, rr - 2, s) == x)
                    mask |= 1 << (x - 1);
            }
        }

        // ---- 六边形环（上2 同色 X + 中左/中右 同色 X + 下2 同色 X，共 6 球；最中间格可空可任意色）----
        for (int r = 2; r < Rows; r++)
        {
            if (RowIsWide(r))
            {
                // 上 2 = 宽行 r cols c..c+1，中排 = 窄行 r-1 cols c-1..c+1，下 2 = 宽行 r-2 cols c..c+1
                // 中左 (r-1,c-1) / 中右 (r-1,c+1) 须同色 X；最中间 (r-1,c) 可空可任意色，不判定
                for (int c = 0; c + 1 < RowCells(r); c++)
                {
                    var x = cells[RowStart(r) + c];
                    if (x == EmptyColor || cells[RowStart(r) + c + 1] != x)
                        continue;
                    if (c - 1 < 0 || c + 1 >= RowCells(r - 1) || c + 1 >= RowCells(r - 2))
                        continue;
                    if (CellAt(cells, r - 1, c - 1) != x || CellAt(cells, r - 1, c + 1) != x)
                        continue;
                    if (CellAt(cells, r - 2, c) != x || CellAt(cells, r - 2, c + 1) != x)
                        continue;
                    mask |= 1 << (x - 1);
                }
            }
            else
            {
                // 上 2 = 窄行 r cols c..c+1，中排 = 宽行 r-1 cols c..c+2，下 2 = 窄行 r-2 cols c..c+1
                // 中左 (r-1,c) / 中右 (r-1,c+2) 须同色 X；最中间 (r-1,c+1) 可空可任意色，不判定
                for (int c = 0; c + 1 < RowCells(r); c++)
                {
                    var x = cells[RowStart(r) + c];
                    if (x == EmptyColor || cells[RowStart(r) + c + 1] != x)
                        continue;
                    if (r - 2 < 0 || c + 2 >= RowCells(r - 1) || c + 1 >= RowCells(r - 2))
                        continue;
                    if (CellAt(cells, r - 1, c) != x || CellAt(cells, r - 1, c + 2) != x)
                        continue;
                    if (CellAt(cells, r - 2, c) != x || CellAt(cells, r - 2, c + 1) != x)
                        continue;
                    mask |= 1 << (x - 1);
                }
            }
        }

        // 命中色全板标记
        if (mask != 0)
        {
            for (int i = 0; i < TotalCells; i++)
            {
                var color = cells[i];
                if (color > 0 && (mask & (1 << (color - 1))) != 0)
                    _elim[i] = 1;
            }
        }

        return mask;
    }

    private static bool Match2(int[] cells, int row, int col, int color)
    {
        return col + 1 < RowCells(row)
            && cells[RowStart(row) + col] == color
            && cells[RowStart(row) + col + 1] == color;
    }

    /// <summary>行内取色；越界返回 -1</summary>
    private static int CellAt(int[] cells, int row, int col)
    {
        return col < 0 || col >= RowCells(row) ? -1 : cells[RowStart(row) + col];
    }

    /// <summary>
    /// 普通消除扫描：同色连通块（六邻接相连，任意形状）≥ClearCount 即整块标记进 _elim，
    /// 返回命中连通块数（flood fill，迭代栈实现）
    /// </summary>
    private int ScanClusters(Player p)
    {
        var clusters = 0;
        var cells = p.Board.Cells;

        for (int i = 0; i < TotalCells; i++)
            _visited[i] = 0;

        for (int start = 0; start < TotalCells; start++)
        {
            var color = cells[start];
            if (color == EmptyColor || _visited[start] != 0)
                continue;

            // flood fill 收集同色连通块
            var size = 0;
            var top = 0;
            _stack[top++] = start;
            _visited[start] = 1;
            while (top > 0)
            {
                var idx = _stack[--top];
                _cluster[size++] = idx;
                var r = _rowOf[idx];
                var c = _colOf[idx];
                for (int d = 0; d < 6; d++)
                {
                    var n = Neighbor(r, c, d);
                    if (n >= 0 && _visited[n] == 0 && cells[n] == color)
                    {
                        _visited[n] = 1;
                        _stack[top++] = n;
                    }
                }
            }

            if (size < ClearCount)
                continue;

            clusters++;
            for (int i = 0; i < size; i++)
                _elim[_cluster[i]] = 1;
        }

        return clusters;
    }

    /// <summary>消除标记格（置空）并清除标记，返回消除格数</summary>
    private int ApplyElim(Player p)
    {
        var cleared = 0;
        var cells = p.Board.Cells;
        for (int i = 0; i < TotalCells; i++)
        {
            if (_elim[i] == 0)
                continue;
            cells[i] = EmptyColor;
            _elim[i] = 0;
            cleared++;
        }
        return cleared;
    }

    /// <summary>
    /// 沉降一遍（自底向上逐个圆子沉降到稳定位）。稳定 = V 支撑（双下斜位均占用）/ 地板
    /// （偶相位 row0、奇相位最低行 row1）/ 单侧支撑+墙楔。单侧支撑滚向空侧、平衡左滚
    /// （确定性破平局），一遍到底无悬停。入板与消除后共用，任何浮空都会被沉降消除
    /// </summary>
    private void SettlePass(Player p)
    {
        var cells = p.Board.Cells;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < RowCells(r); c++)
            {
                var idx = RowStart(r) + c;
                var color = cells[idx];
                if (color == EmptyColor)
                    continue;

                cells[idx] = EmptyColor; // 取出再沉降，目标判定不受自身影响
                SettleOne(cells, r, c, color);
            }
        }
    }

    /// <summary>
    /// 单圆沉降：垂直下落 + 就近嵌 V + 滚落，一遍到稳定。稳定 = V 支撑 / 地板（偶 row0、奇 row1）/ 单侧+墙楔。
    /// 单侧支撑滚向空侧（另一侧为墙则楔住）；双下斜位全空：垂直下落，正下方被占 = 平衡 ->
    /// 就近嵌 V（左/右候选格自身构成 V 即滚向该侧，平局左滚；均不构成 V：左滚）
    /// </summary>
    private void SettleOne(int[] cells, int row, int col, int color)
    {
        while (true)
        {
            if (row == 0)
                break; // 地板（偶相位最低行）

            var bl = Neighbor(row, col, 5);
            var br = Neighbor(row, col, 4);
            var blOcc = bl >= 0 && cells[bl] != EmptyColor; // 墙不算支撑
            var brOcc = br >= 0 && cells[br] != EmptyColor;

            if (blOcc && brOcc)
                break; // V 支撑

            if (blOcc != brOcc)
            {
                // 单侧支撑：另一侧为墙则楔住；否则滚向空侧
                var empty = blOcc ? br : bl;
                if (empty < 0)
                    break; // 墙楔
                row = _rowOf[empty];
                col = _colOf[empty];
                continue;
            }

            // 双下斜位全空（或墙）：垂直下落一格（路径内 -2 行）
            var nr = row - 2;
            if (nr < 0)
                break; // 奇相位最低行（row1）
            if (cells[RowStart(nr) + col] != EmptyColor)
            {
                // 平衡在正下方圆子上：就近嵌 V（左/右候选格自身构成 V 即滚向该侧，平局左滚）
                var blV = bl >= 0 && VAt(cells, _rowOf[bl], _colOf[bl]);
                var brV = br >= 0 && VAt(cells, _rowOf[br], _colOf[br]);
                if (blV)
                {
                    row = _rowOf[bl];
                    col = _colOf[bl];
                    continue;
                }
                if (brV)
                {
                    row = _rowOf[br];
                    col = _colOf[br];
                    continue;
                }
                if (bl >= 0 && cells[bl] == EmptyColor)
                {
                    row = _rowOf[bl];
                    col = _colOf[bl];
                    continue; // 无 V 可达：左滚
                }
                row = _rowOf[br];
                col = _colOf[br];
                continue; // 左为墙：右滚
            }
            row = nr;
        }

        cells[RowStart(row) + col] = color;
    }

    /// <summary>(row,col) 是否构成 V 支撑位（双下斜位均占用）</summary>
    private bool VAt(int[] cells, int row, int col)
    {
        var a = Neighbor(row, col, 5);
        var b = Neighbor(row, col, 4);
        return a >= 0 && b >= 0 && cells[a] != EmptyColor && cells[b] != EmptyColor;
    }

    /// <summary>
    /// 从共享序列按本方游标取一组（3 色：A/B/C）为当前三角组（出生即下落），游标前进；
    /// 双方第 N 组一致（同队列），仅消费进度不同。预览 = 各自游标后的序列段（1-6 渲染）
    /// </summary>
    private void DrawNext(Player p)
    {
        var tri = p.Tri;
        EnsureSeq(p.SeqPos + 3);
        tri.Colors[0] = SharedSeq[p.SeqPos];
        tri.Colors[1] = SharedSeq[p.SeqPos + 1];
        tri.Colors[2] = SharedSeq[p.SeqPos + 2];
        p.SeqPos += 3;
        tri.Rotation = 0;
        tri.X = Fix.FromInt(SlotCount / 2); // 默认中间列出生
        tri.Y = Fix.FromInt(Rows);  // 从棋盘顶部之上起落
        tri.State = PieceState.Falling;
    }

    /// <summary>懒生成共享序列至指定长度（XRng 独立出色，追加顺序随消费顺序确定）</summary>
    private void EnsureSeq(int need)
    {
        while (SharedSeq.Count < need)
            SharedSeq.Add(_rng.NextInt(ColorMin, ColorMax + 1));
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
