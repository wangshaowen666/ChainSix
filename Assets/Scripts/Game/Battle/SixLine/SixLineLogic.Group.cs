/*--------------------------------------------------------------
 * File: SixLineLogic.Group.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:30:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠逻辑层 · 三角组状态机（partial）：旋转、长按横移、Falling/Resting 步进、一次性结算、出子。
/// 结算后的沉降与消除见 Settle / Match。
/// </summary>
public partial class SixLineLogic
{
    /// <summary>旋转 60°：目标足迹须全空，否则拒绝（不做踢墙）</summary>
    private bool TryRotate(Player p, int step)
    {
        var tri = p.Tri;
        var nr = (tri.Rotation + step) % 6;
        var s = SlotFor(nr, tri.X); // 按目标姿态取槽位，渲染位不变（绕中心旋转）

        var y = tri.Y.Int;

        var ok = SlotUsable(p.Board, y + 1, s);                  // 奇行左格（▲ 顶点 / ▽ 左上，都占）
        if (ok && (nr & 1) == 0)
            ok = SlotUsable(p.Board, y, s) && SlotUsable(p.Board, y, s + 1)
                && SlotUsable(p.Board, y + 1, SingleCol(y + 1, s));
        else if (ok)
            ok = SlotUsable(p.Board, y + 1, s + 1) && SlotUsable(p.Board, y, SingleCol(y, s));

        if (!ok)
        {
            LastReject = RejectReason.RotationBlocked;
            return false;
        }

        tri.Rotation = nr;
        return true;
    }

    /// <summary>
    /// 横移球面贴面钳位：按三球圆形外形与已占格求 X 方向贴面边界（贴住即停，不收敛格点）。
    /// 只考虑前进方向一侧的障碍，已重叠时不反向推回。
    /// </summary>
    private Fix ClampXByBalls(Player p, int rotation, Fix curX, Fix wishX, Fix y, int dir)
    {
        var cells = p.Board.Cells;
        var result = wishX;
        for (int b = 0; b < 3; b++)
        {
            BallOffset(rotation, b, out var ox, out var oy);
            var by = y + oy;
            for (int i = 0; i < TotalCells; i++)
            {
                if (cells[i] == EmptyColor)
                    continue;

                var cr = _rowOf[i];
                var dy = by - (Fix.FromInt(cr) + Half);
                var dy2 = dy * dy;
                if (dy2 >= BallTouchSq)
                    continue; // 纵向不重叠，不构成横移约束

                var dx = Fix.Sqrt(BallTouchSq - dy2); // 该球水平方向允许的最小中心距
                var cx = CellCenterX(cr, _colOf[i]);
                if (dir > 0)
                {
                    if (cx - ox <= curX)
                        continue;
                    var limit = cx - dx - ox;
                    if (limit < result) result = limit;
                }
                else
                {
                    if (cx - ox >= curX)
                        continue;
                    var limit = cx + dx - ox;
                    if (limit > result) result = limit;
                }
            }
        }

        if (dir > 0 && result < curX) result = curX; // 已重叠：不反向推回
        if (dir < 0 && result > curX) result = curX;
        return result;
    }

    /// <summary>三角组步进：横移 -> Falling 下落/转接触 -> Resting 容错到期或加速则结算</summary>
    private void StepGroup(int absFrame, Player p)
    {
        var tri = p.Tri;
        if (tri.State == PieceState.None)
            return;

        var dir = 0;
        if (p.MoveLeftHeld)
            dir = -1;
        else if (p.MoveRightHeld)
            dir = 1;

        if (dir != 0)
        {
            var wish = tri.X + (dir > 0 ? MoveSpeed : -MoveSpeed);
            var newX = ClampXByBalls(p, tri.Rotation, tri.X, wish, tri.Y, dir);

            // 墙只限横移范围，不改下落状态、不触发吸附。▽ 槽位取 floor，右端要少一格才能与 ▲ 视觉一致
            var maxX = Fix.FromInt(SlotCount - 1);
            if ((tri.Rotation & 1) != 0)
                maxX = maxX - Fix.FromRaw(1);
            if (newX < Fix.FromInt(0))
                newX = Fix.FromInt(0);
            if (newX > maxX)
                newX = maxX;

            if (newX != wish)
                LastReject = RejectReason.MoveBlocked;

            tri.X = newX;
            ReevaluateContact(p);
        }

        if (tri.State == PieceState.Falling)
        {
            tri.Y -= p.FastDropHeld ? FastFallSpeed : SlowFallSpeed;
            var stop = DeepestStop(p.Board, tri.Rotation, tri.Slot, tri.Y.Int);
            if (tri.Y <= Fix.FromInt(stop))
            {
                tri.Y = Fix.FromInt(stop);
                tri.State = PieceState.Resting;
                p.RestEndFrame = absFrame + GraceFrames;
            }
            return;
        }

        if (absFrame >= p.RestEndFrame || p.FastDropHeld)
            Collapse(p);
    }

    /// <summary>移动/旋转后重判接触：出现更深合法位即脱离回 Falling，容错计时作废</summary>
    private void ReevaluateContact(Player p)
    {
        var tri = p.Tri;
        if (tri.State != PieceState.Resting)
            return;
        if (DeepestStop(p.Board, tri.Rotation, tri.Slot, tri.Y.Int) < tri.Y.Int)
            tri.State = PieceState.Falling;
    }

    /// <summary>一次性结算：入板（行 >= Rows 不写）-> 沉降 -> 消除连锁 -> 下一组出生</summary>
    private void Collapse(Player p)
    {
        var tri = p.Tri;
        var r = tri.Rotation;
        var s = tri.Slot;
        var k = tri.Y.Int;
        if ((r & 1) == 0) // ▲：偶行对 + 顶点
        {
            Place(p, k, s, tri.Colors[EvenLeftColor[r]]);
            Place(p, k, s + 1, tri.Colors[EvenRightColor[r]]);
            Place(p, k + 1, SingleCol(k + 1, s), tri.Colors[OddLeftColor[r]]);
        }
        else // ▽：奇行对 + 顶点
        {
            Place(p, k + 1, s, tri.Colors[OddLeftColor[r]]);
            Place(p, k + 1, s + 1, tri.Colors[OddRightColor[r]]);
            Place(p, k, SingleCol(k, s), tri.Colors[EvenRightColor[r]]);
        }

        tri.State = PieceState.None;
        SettlePass(p);
        SettleChains(p);
        DrawNext(p);
    }

    private static void Place(Player p, int row, int col, int color)
    {
        if (row < Rows && color >= 0)
            p.Board.Set(row, col, color);
    }

    // ---- 出子序列 ----

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
        tri.Y = Fix.FromInt(Rows);         // 从棋盘顶之上起落
        tri.State = PieceState.Falling;
    }

    private void EnsureSeq(int need)
    {
        while (SharedSeq.Count < need)
            SharedSeq.Add(_rng.NextInt(ColorMin, ColorMax + 1));
    }
}
