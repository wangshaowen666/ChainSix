/*--------------------------------------------------------------
 * File: SixLineLogic.Board.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:30:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠逻辑层 · 棋盘几何与数据结构（partial）：
/// - 蜂窝错行 12 行（偶行 10 格 / 奇行 9 格，共 114 格）：扁平下标换算 + 六邻接
/// - 占位判定与落点查询（最深可停位 = 真实接触，几何非法的相位行直接路过）
/// - 三角组足迹：旋转姿态 -> 颜色槽映射、球相对锚点偏移、格中心坐标
/// - 数据结构：Board / Player / Triomino / 状态枚举
/// 本文件只放纯几何与数据，不含状态机与结算（见 Group / Settle / Match）
/// </summary>
public partial class SixLineLogic
{
    /// <summary>格单位 0.5</summary>
    private static readonly Fix Half = Fix.FromDouble(0.5);

    /// <summary>格单位 1.5（三角组足迹偏移常量）</summary>
    private static readonly Fix One5 = Half + Fix.One;

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

    /// <summary>
    /// 单球（顶点）列：随所在行奇偶取 slot（奇行=山谷位）或 slot+1（偶行=头顶位）。
    /// 蜂窝错行渲染下两种行奇偶的顶点都居中于其配对之下（真蜂窝竖直下落的锯齿列）
    /// </summary>
    public static int SingleCol(int row, int slot) => (row & 1) == 1 ? slot : slot + 1;

    /// <summary>连续列位 -> 量化槽位（half-up：x+0.5 取整；X 恒 &gt;= 0，截断即 floor）</summary>
    private static int RoundSlot(Fix x) => (x + Half).Int;

    /// <summary>
    /// 指定旋转姿态下连续列位 X 的量化槽位（足迹左列）：▲ round(X) / ▽ floor(X)。
    /// 取整方式随行相位不同，目的都是让渲染三球恒落在足迹格内（见 Triomino.Slot）
    /// </summary>
    private static int SlotFor(int rotation, Fix x) => (rotation & 1) == 0 ? RoundSlot(x) : x.Int;

    /// <summary>格中心 X（格单位）：偶行 col+0.5 / 奇行 col+1（蜂窝错行）</summary>
    private static Fix CellCenterX(int row, int col)
    {
        return Fix.FromInt(col) + Half + (RowIsWide(row) ? Fix.Zero : Half);
    }

    // ---- 状态枚举 ----

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

    // ---- 三角组足迹与颜色映射 ----

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

        /// <summary>
        /// 量化槽位（足迹左列，碰撞/落点/结算按列）：▲ 的偶行对与渲染位 x+0.5 对齐于偶行格心 → round(x)；
        /// ▽ 的奇行对对齐于奇行格心 col+1 → floor(x)。两种取整都保证连续 X 下渲染三球恒落在足迹格内
        /// （否则 ▽ 足迹相对渲染右偏半格，贴棋子横移时会出现逻辑足迹先被占的假接触）
        /// </summary>
        public int Slot => SlotFor(Rotation, X);
    }

    // ---- 数据结构 ----

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

    /// <summary>
    /// 球 ball 相对锚点 (X, Y) 的格单位偏移（与表现层 BallIdealPos 一致，居中相位；碰撞只用位置集合，与颜色无关）：
    /// ▲：0=偶行左(0.5,0.5) 1=偶行右(1.5,0.5) 2=奇行顶点(1.0,1.5)；
    /// ▽：0=奇行左(0.5,1.5) 1=奇行右(1.5,1.5) 2=偶行顶点(1.0,0.5)
    /// </summary>
    private static void BallOffset(int rotation, int ball, out Fix ox, out Fix oy)
    {
        if ((rotation & 1) == 0)
        {
            if (ball == 0) { ox = Half; oy = Half; }
            else if (ball == 1) { ox = One5; oy = Half; }
            else { ox = Fix.One; oy = One5; }
        }
        else
        {
            if (ball == 0) { ox = Half; oy = One5; }
            else if (ball == 1) { ox = One5; oy = One5; }
            else { ox = Fix.One; oy = Half; }
        }
    }

    // ---- 占位判定 ----

    /// <summary>格位可用：行 ≥ Rows 视为空（棋盘顶之上）；列越界或已占视为不可用</summary>
    private static bool SlotUsable(Board b, int row, int col)
    {
        return CellInRow(row, col) && CellEmpty(b, row, col);
    }

    /// <summary>列是否在行内（行 ≥ Rows 视为在行内 = 棋盘顶之上）</summary>
    private static bool CellInRow(int row, int col)
    {
        return row >= Rows || (col >= 0 && col < RowCells(row));
    }

    /// <summary>格是否空（行 ≥ Rows 视为空）</summary>
    private static bool CellEmpty(Board b, int row, int col)
    {
        return row >= Rows || b.Get(row, col) == EmptyColor;
    }

    /// <summary>
    /// 组在基准行 k 的足迹列是否都在行内（行 ≥ Rows 视为在行内）。
    /// 用于区分「几何非法的相位行」（如 slot 8 的奇行对需要 col 9 不存在，下落时直接路过）与「被占」
    /// </summary>
    private static bool PlacementInRow(int rotation, int slot, int k)
    {
        if (k < 0)
            return false;
        if ((rotation & 1) == 0) // ▲：偶行对 (k,s)(k,s+1) + 奇行顶点 (k+1,SingleCol)
            return CellInRow(k, slot) && CellInRow(k, slot + 1) && CellInRow(k + 1, SingleCol(k + 1, slot));
        return CellInRow(k + 1, slot) && CellInRow(k + 1, slot + 1) && CellInRow(k, SingleCol(k, slot)); // ▽
    }

    /// <summary>组在基准行 k 的足迹格是否全空（行 ≥ Rows 视为空）</summary>
    private static bool PlacementEmpty(Board b, int rotation, int slot, int k)
    {
        if ((rotation & 1) == 0)
            return CellEmpty(b, k, slot) && CellEmpty(b, k, slot + 1) && CellEmpty(b, k + 1, SingleCol(k + 1, slot));
        return CellEmpty(b, k + 1, slot) && CellEmpty(b, k + 1, slot + 1) && CellEmpty(b, k, SingleCol(k, slot));
    }

    /// <summary>
    /// 最深可停量化基准行：向下滚动，被占行阻挡（真实接触），几何非法的相位行（列越界）直接路过，
    /// 最终停行必为几何合法行；fromK 自身被占（防御，如出生位被堆顶侵入）则先向上找最近合法空位。
    /// 关键：slot 8 的 ▲ 只有偶行、▽ 只有奇行才是合法落点行，不能按「单行是否空闲」直推上下
    /// （旧实现会在贴右墙时把奇行全算作不可用 → 下落中误判接触冻结）
    /// </summary>
    private static int DeepestStop(Board b, int rotation, int slot, int fromK)
    {
        var k = fromK < 0 ? 0 : fromK;
        while (k < Rows && PlacementInRow(rotation, slot, k) && !PlacementEmpty(b, rotation, slot, k))
            k++;
        while (k > 0)
        {
            var nk = k - 1;
            if (PlacementInRow(rotation, slot, nk) && !PlacementEmpty(b, rotation, slot, nk))
                break; // 被占，停在 k
            k = nk;    // 空行或几何非法行（路过）
        }
        while (k < Rows && !PlacementInRow(rotation, slot, k))
            k++;       // 停行必须几何合法
        return k;
    }
}
