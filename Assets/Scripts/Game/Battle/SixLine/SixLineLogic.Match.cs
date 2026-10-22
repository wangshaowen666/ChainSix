/*--------------------------------------------------------------
 * File: SixLineLogic.Match.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:30:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠逻辑层 · 消除与连锁（partial）：
/// - 特殊图形 6 种（横/右斜/左斜直线 6 连、上1中2下3、上3中2下1 金字塔、六球环）-> 命中色全板消除
/// - 普通消除：同色连通块（六邻接）≥ ClearCount 即整块消除
/// - 消除 -> 沉降 -> 复扫递归到稳定，每轮连锁 +1；结果写入 Player 供垃圾子公式/HUD 消费
/// 标记/栈/暂存数组均为构造期预分配，Tick 内零分配
/// </summary>
public partial class SixLineLogic
{
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
}
