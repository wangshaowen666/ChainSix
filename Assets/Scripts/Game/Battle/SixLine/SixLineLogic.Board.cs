/*--------------------------------------------------------------
 * File: SixLineLogic.Board.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:30:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠逻辑层 · 棋盘几何与数据结构（partial）。只放纯几何与数据，
/// 状态机与结算见 Group / Settle / Match。
/// </summary>
public partial class SixLineLogic
{
    private static readonly Fix Half = Fix.FromDouble(0.5);
    private static readonly Fix One5 = Half + Fix.One;

    // ---- 蜂窝错行几何（偶行 10 格中心 x=col+0.5，奇行 9 格中心 x=col+1）----

    public static bool RowIsWide(int row) => (row & 1) == 0;
    public static int RowCells(int row) => RowIsWide(row) ? 10 : 9;

    /// <summary>行首扁平下标（每两行 19 格）</summary>
    public static int RowStart(int row) => (row >> 1) * 19 + (RowIsWide(row) ? 0 : 10);
    public const int TotalCells = 114;

    /// <summary>六邻接扁平下标，-1 = 越界。dir：0=右 1=左 2=右上 3=左上 4=右下 5=左下</summary>
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

    /// <summary>单球（顶点）列：奇行取 slot、偶行取 slot+1（真蜂窝的锯齿列）</summary>
    public static int SingleCol(int row, int slot) => (row & 1) == 1 ? slot : slot + 1;

    private static int RoundSlot(Fix x) => (x + Half).Int;

    /// <summary>足迹左列槽位：▲ round(X) / ▽ floor(X)，保证渲染三球恒落在足迹格内</summary>
    private static int SlotFor(int rotation, Fix x) => (rotation & 1) == 0 ? RoundSlot(x) : x.Int;

    private static Fix CellCenterX(int row, int col)
    {
        return Fix.FromInt(col) + Half + (RowIsWide(row) ? Fix.Zero : Half);
    }

    // ---- 状态枚举 ----

    public enum PieceState : byte
    {
        None,    // 结算完成到下一组出生的瞬时态
        Falling, // 下落中
        Resting, // 已真实接触，容错期内可调整
    }

    /// <summary>拒绝原因用枚举，保证 Tick 零 GC</summary>
    public enum RejectReason : byte
    {
        None = 0,
        BadPlayerIndex,
        RotationBlocked,
        MoveBlocked,
    }

    // ---- 三角组足迹与颜色映射 ----

    /// <summary>旋转状态(0..5) -> 各足迹格的颜色槽（0/1/2 = A/B/C，-1 = 不占此格）</summary>
    public static readonly int[] OddLeftColor = { 0, 1, 1, 2, 2, 0 };
    public static readonly int[] OddRightColor = { -1, 0, -1, 1, -1, 2 };
    public static readonly int[] EvenLeftColor = { 1, -1, 2, -1, 0, -1 };
    public static readonly int[] EvenRightColor = { 2, 2, 0, 0, 1, 1 };

    /// <summary>
    /// 下落三角组。Colors = 原始 ▲ 槽位色 [A=顶, B=左下, C=右下]；Rotation 偶=▲ 奇=▽；
    /// X = 连续列位锚点，Y = 偶行（下排）行坐标。
    /// </summary>
    public class Triomino
    {
        public readonly int[] Colors = new int[3];
        public int Rotation;
        public Fix X;
        public Fix Y;
        public PieceState State;

        /// <summary>量化槽位（足迹左列）。取整方式随姿态变化，否则 ▽ 会右偏半格导致假接触</summary>
        public int Slot => SlotFor(Rotation, X);
    }

    // ---- 数据结构 ----

    public class Board
    {
        public readonly int[] Cells = new int[TotalCells];
        public int Get(int row, int col) => Cells[RowStart(row) + col];
        public void Set(int row, int col, int color) => Cells[RowStart(row) + col] = color;
    }

    public class Player
    {
        public int PlayerIndex;
        public readonly Board Board = new Board();
        public int SeqPos; // 出子游标（SharedSeq 下标）
        public readonly Triomino Tri = new Triomino();
        public bool MoveLeftHeld;  // 帧内瞬时标志，步进后复位
        public bool MoveRightHeld;
        public bool FastDropHeld;

        // 结算结果（每 Tick 重置；1-4 垃圾公式与 1-6 HUD 消费）
        public int LastSettleChain;
        public int LastSettleClusters;
        public readonly int[] RoundClusters = new int[MaxSettleRounds];
        public readonly int[] RoundCells = new int[MaxSettleRounds];
        public readonly int[] RoundSpecial = new int[MaxSettleRounds]; // 每轮特殊消除颜色 bitmask（bit = 颜色 id-1）
        public int RoundCount;

        public int RestEndFrame; // 容错截止帧
    }

    /// <summary>
    /// 球相对锚点的格单位偏移（居中相位）。▲：左(0.5,0.5) 右(1.5,0.5) 顶(1,1.5)；
    /// ▽：左(0.5,1.5) 右(1.5,1.5) 顶(1,0.5)
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

    // ---- 占位判定（行 >= Rows 一律视为棋盘顶之上：在行内且为空）----

    private static bool SlotUsable(Board b, int row, int col)
    {
        return CellInRow(row, col) && CellEmpty(b, row, col);
    }

    private static bool CellInRow(int row, int col)
    {
        return row >= Rows || (col >= 0 && col < RowCells(row));
    }

    private static bool CellEmpty(Board b, int row, int col)
    {
        return row >= Rows || b.Get(row, col) == EmptyColor;
    }

    private static bool PlacementInRow(int rotation, int slot, int k)
    {
        if (k < 0)
            return false;
        if ((rotation & 1) == 0)
            return CellInRow(k, slot) && CellInRow(k, slot + 1) && CellInRow(k + 1, SingleCol(k + 1, slot));
        return CellInRow(k + 1, slot) && CellInRow(k + 1, slot + 1) && CellInRow(k, SingleCol(k, slot));
    }

    private static bool PlacementEmpty(Board b, int rotation, int slot, int k)
    {
        if ((rotation & 1) == 0)
            return CellEmpty(b, k, slot) && CellEmpty(b, k, slot + 1) && CellEmpty(b, k + 1, SingleCol(k + 1, slot));
        return CellEmpty(b, k + 1, slot) && CellEmpty(b, k + 1, slot + 1) && CellEmpty(b, k, SingleCol(k, slot));
    }

    /// <summary>
    /// 最深可停基准行：被占行阻挡，几何非法的相位行（列越界）直接路过。
    /// slot 8 下 ▲ 只有偶行、▽ 只有奇行是合法落点，不能按单行是否空闲直推。
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
                break;
            k = nk;
        }
        while (k < Rows && !PlacementInRow(rotation, slot, k))
            k++;
        return k;
    }
}
