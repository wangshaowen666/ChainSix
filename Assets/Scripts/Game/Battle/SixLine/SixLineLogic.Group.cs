/*--------------------------------------------------------------
 * File: SixLineLogic.Group.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:30:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠逻辑层 · 三角组状态机（partial）：
/// - 操作：旋转（60° 刚体旋转的格点量化）、长按横移（三球球面贴面钳位）
/// - 状态：Falling 刚性下落 -> Resting 可沉降（容错窗口内可继续调整）-> Collapse 一次性结算
/// - 出子：从共享序列按本方游标取一组（双方第 N 组一致）
/// 结算后的沉降/消除见 Settle / Match 两个 partial 文件
/// </summary>
public partial class SixLineLogic
{
    /// <summary>
    /// 旋转 60°（Rotation + step mod 6）：目标足迹在棋盘内的格须全为空才生效，否则拒绝（不做踢墙）。
    /// 足迹列越界（如 slot 8 下 ▽ 的奇行对需要不存在的 col 9）由 SlotUsable 直接判不可用；
    /// 行 ≥ Rows（棋盘顶之上）视为空；基准行取 Y 截断（Fix.Int，纯整数确定性）。
    /// 表现层按居中相位渲染（绕三角中心、无左右位移，见 SixLineView），落定时吸附归位
    /// </summary>
    private bool TryRotate(Player p, int step)
    {
        var tri = p.Tri;
        var nr = (tri.Rotation + step) % 6;
        var s = SlotFor(nr, tri.X); // 按目标姿态取槽位（▲ round / ▽ floor），渲染位不变（绕中心旋转）

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
    /// 横移球面贴面钳位：按三球圆形外形与已占格（球直径 BallTouch）沿 X 方向求贴面边界，
    /// dir=+1 右移 / -1 左移，返回钳位后的 X（球面贴住即停，不收敛到格点/半格）。
    /// 只考虑前进方向一侧的障碍；已在重叠位时不反向推回
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
                    continue; // 纵向距离已超球径，不构成横移约束

                var dx = Fix.Sqrt(BallTouchSq - dy2); // 该球水平方向允许的最小中心距
                var cx = CellCenterX(cr, _colOf[i]);
                if (dir > 0)
                {
                    if (cx - ox <= curX)
                        continue; // 障碍不在前方（右），不约束右移
                    var limit = cx - dx - ox; // X' ≤ limit 才不重叠
                    if (limit < result) result = limit;
                }
                else
                {
                    if (cx - ox >= curX)
                        continue; // 障碍不在前方（左），不约束左移
                    var limit = cx + dx - ox; // X' ≥ limit
                    if (limit > result) result = limit;
                }
            }
        }

        if (dir > 0 && result < curX) result = curX; // 已重叠：不反向推回
        if (dir < 0 && result > curX) result = curX;
        return result;
    }

    /// <summary>
    /// 三角组步进（下落/可沉降状态机）：
    /// - 长按左右移动：连续球面贴面（不关注落点格位/奇偶，不收敛半格），被棋子/墙贴住即停
    /// - Falling：持续下落，到达最深可停位（真实接触棋子/地板）转 Resting 并启动容错计时
    /// - Resting：容错到期或按住加速 => 一次性结算；移动/旋转后脱离接触（ReevaluateContact）则回 Falling
    /// </summary>
    private void StepGroup(int absFrame, Player p)
    {
        var tri = p.Tri;
        if (tri.State == PieceState.None)
            return;

        // 左右平移：按住按 MoveSpeed 连续积分；不关注落点格位/奇偶、不做半格收敛，
        // 只按三球圆形外形与已占格做球面贴面钳位（贴住即停），外加左右墙范围限制。
        // 左右墙只限横移范围，绝不改变下落状态（不触发吸附窗口）
        var dir = 0;
        if (p.MoveLeftHeld)
            dir = -1;
        else if (p.MoveRightHeld)
            dir = 1;

        if (dir != 0)
        {
            var wish = tri.X + (dir > 0 ? MoveSpeed : -MoveSpeed);
            var newX = ClampXByBalls(p, tri.Rotation, tri.X, wish, tri.Y, dir);

            // 左右墙范围：仅限制横移，不改下落状态、不触发吸附。▲ 槽位取 round(X)，右端 X=8（槽位 8，最右球心 9.5 贴边）；
            // ▽ 槽位取 floor(X)，右端 X<8（槽位 7，渲染三球同样贴到 9.5 附近）——两边贴墙视觉一致
            var maxX = Fix.FromInt(SlotCount - 1);
            if ((tri.Rotation & 1) != 0)
                maxX = maxX - Fix.FromRaw(1);
            if (newX < Fix.FromInt(0))
                newX = Fix.FromInt(0);
            if (newX > maxX)
                newX = maxX;

            if (newX != wish)
                LastReject = RejectReason.MoveBlocked; // 被棋子/墙贴面挡下（诊断用）

            tri.X = newX;
            ReevaluateContact(p); // 横移后重判接触（可沉降态可能脱离回下落态）
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

    // ---- 出子序列 ----

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
}
