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
    private static readonly Fix Third = Fix.One / Fix.FromInt(3);

    private bool TryRotate(Player p, int step)
    {
        var tri = p.Tri;
        var next = (tri.Rotation + step) % 6;
        // 翻转 ▲/▽ 时保持重心不动，否则视觉上每次旋转都会上下跳动。
        var nextY = tri.Y + ((tri.Rotation & 1) == 0 ? -Third : Third);
        if (!BallsFit(p.Board, next, tri.X, nextY))
        {
            LastReject = RejectReason.RotationBlocked;
            return false;
        }
        tri.Rotation = next;
        tri.RotationTurns += step == 1 ? 1 : -1;
        tri.Y = nextY;
        return true;
    }

    private bool BallsFit(Board board, int rotation, Fix x, Fix y)
    {
        if (x < Fix.Zero || x > Fix.FromInt(Columns - 2) || y < Fix.Zero)
            return false;
        for (int ball = 0; ball < 3; ball++)
        {
            BallPosition(rotation, x, y, ball, out var bx, out var by);
            for (int i = 0; i < TotalCells; i++)
            {
                if (board.Cells[i] == EmptyColor)
                    continue;
                var dx = bx - CellCenterX(_rowOf[i], _colOf[i]);
                var dy = by - CellY(_rowOf[i]);
                if (dx * dx + dy * dy < BallTouchSq - Fix.FromRaw(4))
                    return false;
            }
        }
        return true;
    }

    private Fix ContactY(Player p, int rotation, Fix x, Fix fromY)
    {
        var stop = Fix.Zero;
        for (int ball = 0; ball < 3; ball++)
        {
            BallOffset(rotation, ball, out var ox, out var oy);
            for (int i = 0; i < TotalCells; i++)
            {
                if (p.Board.Cells[i] == EmptyColor)
                    continue;
                var dx = x + ox - CellCenterX(_rowOf[i], _colOf[i]);
                var dxSq = dx * dx;
                if (dxSq >= BallTouchSq)
                    continue;
                var contact = Fix.FromInt(_rowOf[i]) + Half - oy + Fix.Sqrt(BallTouchSq - dxSq) / RowPitch;
                if (contact <= fromY + Fix.FromRaw(8) && contact > stop)
                    stop = contact;
            }
        }
        return stop;
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
                var dy = (by - (Fix.FromInt(cr) + Half)) * RowPitch;
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

            // 墙只限横移范围；下落接触按圆形外沿计算。
            var maxX = Fix.FromInt(SlotCount - 1);
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
            var stop = ContactY(p, tri.Rotation, tri.X, tri.Y);
            var nextY = tri.Y - (p.FastDropHeld ? FastFallSpeed : SlowFallSpeed);
            if (nextY <= stop)
            {
                tri.Y = stop;
                tri.State = PieceState.Resting;
                p.RestEndFrame = absFrame + GraceFrames;
            }
            else
                tri.Y = nextY;
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
        if (ContactY(p, tri.Rotation, tri.X, tri.Y) < tri.Y - Fix.FromRaw(8))
            tri.State = PieceState.Falling;
    }

    private bool FindLockPlacement(Player p, out int row, out int slot)
    {
        var tri = p.Tri;
        row = Rows;
        slot = tri.Slot;
        var best = Fix.FromInt(100000);
        var found = false;
        for (int r = 0; r <= Rows; r++)
        {
            var dy = Fix.FromInt(r) - tri.Y;
            if (dy < -Fix.One || dy > Fix.One)
                continue;
            for (int c = 0; c < SlotCount; c++)
            {
                if (!PlacementInRow(tri.Rotation, c, r) || !PlacementEmpty(p.Board, tri.Rotation, c, r))
                    continue;
                var anchor = Fix.FromInt(c) + (((r & 1) == (tri.Rotation & 1)) ? Fix.Zero : Half);
                var dx = anchor - tri.X;
                if (dx < -Half || dx > Half)
                    continue;
                var cost = dx * dx + dy * dy * RowPitch * RowPitch;
                if (cost >= best)
                    continue;
                best = cost;
                row = r;
                slot = c;
                found = true;
            }
        }
        return found;
    }

    private void Collapse(Player p)
    {
        BeginPresentation(p);
        var tri = p.Tri;
        var rotation = tri.Rotation;
        if (!FindLockPlacement(p, out var row, out var slot))
            p.Overflow = true;
        else
        {
            for (int ball = 0; ball < 3; ball++)
            {
                var colorSlot = BallColorSlot(rotation, ball);
                var r = ball == 2 ? ((rotation & 1) == 0 ? row + 1 : row) : ((rotation & 1) == 0 ? row : row + 1);
                var c = ball == 2 ? SingleCol(r, slot) : slot + ball;
                BallPosition(rotation, tri.X, tri.Y, ball, out var fromX, out var fromY);
                Place(p, r, c, tri.Colors[colorSlot], tri.PieceIds[colorSlot], fromX, fromY);
            }
        }
        p.VisualEndFrame = System.Math.Max(p.VisualEndFrame, SnapFrames);
        tri.State = PieceState.None;
        SettlePass(p);
        SettleChains(p);
        p.LockedThisFrame = true;
    }

    private void Place(Player p, int row, int col, int color, int pieceId, Fix fromX, Fix fromY)
    {
        if (row < 0 || row >= Rows || col < 0 || col >= RowCells(row) || p.Board.Get(row, col) != EmptyColor)
        {
            p.Overflow = true;
            return;
        }
        var index = RowStart(row) + col;
        p.Board.Cells[index] = color;
        p.Board.PieceIds[index] = pieceId;
        RecordVisual(p, SlVisualKind.Place, pieceId, color, fromX, fromY, CellCenterX(row, col), CellY(row), 0, SnapFrames);
    }

    // ---- 出子序列 ----

    private void DrawNext(Player p)
    {
        var tri = p.Tri;
        for (int i = 0; i < 3; i++)
        {
            tri.Colors[i] = p.PreviewColors[i];
            tri.PieceIds[i] = p.NextPieceId++;
        }
        for (int i = 0; i < p.PreviewColors.Length - 3; i++)
            p.PreviewColors[i] = p.PreviewColors[i + 3];
        for (int i = p.PreviewColors.Length - 3; i < p.PreviewColors.Length; i++)
            p.PreviewColors[i] = p.PieceRng.NextInt(ColorMin, ColorMax + 1);
        p.SeqPos += 3;
        tri.Rotation = 0;
        tri.RotationTurns = 0;
        tri.X = Fix.FromInt(SlotCount / 2); // 默认中间列出生
        tri.Y = Fix.FromInt(Rows);         // 从棋盘顶之上起落
        tri.State = PieceState.Falling;
    }

}
