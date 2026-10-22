/*--------------------------------------------------------------
 * File: SixLineLogic.Settle.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:30:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠逻辑层 · 物理沉降（partial）：
/// 自底向上逐个圆子落到稳定位（V 支撑 / 地板 / 单侧支撑+墙楔），一遍到底无悬停。
/// 入板与每次消除后共用，任何浮空当帧消除；纯棋盘操作，不含随机与事件
/// </summary>
public partial class SixLineLogic
{
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
}
