/*--------------------------------------------------------------
 * File: SixLineView.Sync.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:40:00
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

/// <summary>
/// 六连珠表现层 · 逻辑帧对账（partial）：
/// - 下落组状态机：SeqPos 变化 = 旧组落定（三颗捐入池）+ 新组出生（从池取三颗按球序绑定）
/// - 旋转：姿态变化触发 RotAnimSec 过渡，其余时间按跟随速度走位
/// - 棋盘三趟对账：释放新空格 -> 占用格认领视觉圈（来源匹配：正上 2 行 / 斜上 / 同色就近 / 任意重着色）-> 隐藏未认领池圈
/// 只做目标位确定，位置插值在 Anim 文件
/// </summary>
public partial class SixLineView
{
    private void SyncPlayer(SixLineLogic.Player p, BoardView bv)
    {
        var cells = p.Board.Cells;
        var tri = p.Tri;

        // ---- 下落组状态机（SeqPos 变化 = 新组出生：同一 Tick 内旧组落定 + 新组出组，必须先移交再重生）----
        if (bv.TriSeqPos != p.SeqPos)
        {
            if (bv.TriActive)
            {
                // 旧组刚落定：三颗捐入空闲池（Pos 保持最后下落位，LastCell = -1，供落定格就近认领）
                for (int i = 0; i < 3; i++)
                {
                    bv.TriAnims[i].LastCell = -1;
                    bv.Free.Add(bv.TriAnims[i]);
                    bv.TriAnims[i] = null;
                }
                bv.TriActive = false;
            }

            // 新组出生：从池取三颗按球序绑定（0=A 1=B 2=C），Pos = 首帧理想渲染位（无位移感）
            var y0 = tri.Y.AsFloat;
            var x0 = tri.X.AsFloat;
            for (int b = 0; b < 3; b++)
            {
                BallIdealPos(tri.Rotation, x0, y0, b, out var bx, out var by);
                var a = TakeFromPool(bv);
                bv.TriAnims[b] = a;
                a.Speed = TriFollowSpeed;
                a.Pos = new Vector3(bx * bv.Cell, by * bv.Cell, 0.2f);
                a.Tr.localPosition = a.Pos;
                SetTriTarget(a, bx * bv.Cell, by * bv.Cell, tri.Colors[b]);
            }
            bv.TriActive = true;
            bv.TriSeqPos = p.SeqPos;
            bv.TriRotation = tri.Rotation;
        }

        if (bv.TriActive &&
            (tri.State == SixLineLogic.PieceState.Falling || tri.State == SixLineLogic.PieceState.Resting))
        {
            // 三颗按球序（0=A 1=B 2=C）走位，颜色身份不变：下落与容错期均按理想 60° 位渲染
            // （笔直不摆动、不吸附，玩家所见即调整基准）；结算入板时视觉圈滑向格点位
            // （整组按基准行相位刚性相移 ≤0.5 格 + 物理滚动，形状不变形）
            var r = tri.Rotation;
            var y = tri.Y.AsFloat;
            var x = tri.X.AsFloat;

            if (bv.TriRotation != r)
            {
                // 旋转过渡：三球按统一时长转向新姿态（走位过程即 60° 旋转表现）
                for (int b = 0; b < 3; b++)
                {
                    BallIdealPos(r, x, y, b, out var bx, out var by);
                    var tx = bx * bv.Cell;
                    var ty = by * bv.Cell;
                    var a = bv.TriAnims[b];
                    var d = Vector2.Distance(new Vector2(tx, ty), a.Pos);
                    a.Speed = d > 0.0001f ? d / RotAnimSec : TriFollowSpeed;
                    SetTriTarget(a, tx, ty, tri.Colors[b]);
                }
                bv.TriRotation = r;
            }
            else
            {
                for (int b = 0; b < 3; b++)
                {
                    BallIdealPos(r, x, y, b, out var bx, out var by);
                    var a = bv.TriAnims[b];
                    a.Speed = TriFollowSpeed; // 过渡已结束：恢复跟随速度，避免旋转低速残留导致移动掉队
                    SetTriTarget(a, bx * bv.Cell, by * bv.Cell, tri.Colors[b]);
                }
            }
        }

        // ---- 格子对账 pass1：新变空的格子释放视觉圈进池（保留位置，供同帧新占用格认领）----
        for (int i = 0; i < SixLineLogic.TotalCells; i++)
        {
            if (cells[i] != SixLineLogic.EmptyColor || bv.Prev[i] == SixLineLogic.EmptyColor || bv.CellAnims[i] == null)
                continue;
            bv.CellAnims[i].LastCell = i;
            bv.Free.Add(bv.CellAnims[i]);
            bv.CellAnims[i] = null;
        }

        // ---- 格子对账 pass2：占用格子绑定视觉圈 ----
        for (int r = 0; r < SixLineLogic.Rows; r++)
        {
            for (int c = 0; c < SixLineLogic.RowCells(r); c++)
            {
                var idx = SixLineLogic.RowStart(r) + c;
                var color = cells[idx];
                if (color == SixLineLogic.EmptyColor)
                    continue;

                var anim = bv.CellAnims[idx];
                if (anim != null && anim.Color == color)
                {
                    anim.LastCell = idx; // 延续在位：刷新来源戳供后续沉降匹配
                    anim.Stamp = _curFrame;
                    continue;
                }

                if (anim != null)
                    bv.Free.Add(anim); // 换色异常（理论不发生）：原圈归还池

                var take = TakeForCell(bv, idx, r, c, color);
                bv.CellAnims[idx] = take;
            }
        }

        // ---- pass3：隐藏本帧未被认领的池圈 ----
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp != _curFrame && a.Tr.gameObject.activeSelf)
                a.Tr.gameObject.SetActive(false);
        }

        Array.Copy(cells, bv.Prev, SixLineLogic.TotalCells);
    }

    /// <summary>为新占用格认领视觉圈：优先来源格（正上 2 行 = 垂直下落 / 斜上 = 滚落）同色，其次同色就近，再次任意重着色，最后新建</summary>
    private CircleAnim TakeForCell(BoardView bv, int idx, int row, int col, int color)
    {
        var target = CellLocal(row, col, bv.Cell, 0.1f);
        CircleAnim best;
        var bestDist = float.MaxValue;

        // 1. 垂直来源（正上 2 行刚空出）
        best = TakeSource(bv, color, row + 2 < SixLineLogic.Rows ? SixLineLogic.RowStart(row + 2) + col : -1);
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 2. 斜上来源（滚落）
        best = TakeSource(bv, color, SixLineLogic.Neighbor(row, col, 2));
        if (best != null)
            return Bind(bv, best, idx, color, target);
        best = TakeSource(bv, color, SixLineLogic.Neighbor(row, col, 3));
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 3. 同色就近
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp == _curFrame || a.Color != color)
                continue;
            var d = (a.Pos - target).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 4. 任意就近（重着色兜底）
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp == _curFrame)
                continue;
            var d = (a.Pos - target).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }
        if (best != null)
            return Bind(bv, best, idx, color, target);

        // 5. 池耗尽（理论不发生：圈数守恒）新建
        var na = new CircleAnim
        {
            Tr = MakeCircle(bv.Root, $"Anim_{idx}", bv.Cell * 0.96f, s_palette[color - 1]),
            Mr = null,
            Color = color,
            Pos = target,
            Target = target,
            Speed = SettleSpeed,
            LastCell = idx,
            Stamp = _curFrame,
        };
        na.Mr = na.Tr.GetComponent<MeshRenderer>();
        na.Tr.gameObject.SetActive(true);
        return na;
    }

    private CircleAnim Bind(BoardView bv, CircleAnim a, int idx, int color, Vector3 target)
    {
        bv.Free.Remove(a);
        if (a.Color != color)
        {
            a.Color = color;
            a.Mr.material.color = s_palette[color - 1];
        }
        a.Target = target;
        a.Speed = SettleSpeed;
        a.LastCell = idx;
        a.Stamp = _curFrame;
        a.Tr.gameObject.SetActive(true);
        return a;
    }

    /// <summary>从空闲池按来源格 + 同色认领（认领即出池）</summary>
    private CircleAnim TakeSource(BoardView bv, int color, int srcIdx)
    {
        if (srcIdx < 0)
            return null;
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp != _curFrame && a.Color == color && a.LastCell == srcIdx)
            {
                bv.Free.RemoveAt(i);
                return a;
            }
        }
        return null;
    }

    /// <summary>从空闲池取任意一个（未认领的）</summary>
    private CircleAnim TakeFromPool(BoardView bv)
    {
        for (int i = 0; i < bv.Free.Count; i++)
        {
            var a = bv.Free[i];
            if (a.Stamp != _curFrame)
            {
                bv.Free.RemoveAt(i);
                return a;
            }
        }
        var na = new CircleAnim
        {
            Tr = MakeCircle(bv.Root, "Anim", bv.Cell * 0.96f, Color.white),
            LastCell = -1,
        };
        na.Mr = na.Tr.GetComponent<MeshRenderer>();
        return na;
    }

    /// <summary>
    /// 球 ball 下落/容错期渲染位（格单位，x 为连续列位）：三球按三角形中心居中相位渲染，
    /// 旋转只是三球绕中心转位、不产生左右位移。▲ 偶行对 (x+0.5/x+1.5, y+0.5) + 奇行顶点 (x+1, y+1.5)；
    /// ▽ 奇行对 (x+0.5/x+1.5, y+1.5) + 偶行顶点 (x+1, y+0.5)。
    /// 与逻辑层 BallOffset 一致；横移为连续球面贴面（见 SixLineLogic.StepGroup），
    /// 结算入板时视觉圈滑向落点格位（≤0.5 格，全为物理滚动/吸附）
    /// </summary>
    private static void BallIdealPos(int r, float x, float y, int ball, out float bx, out float by)
    {
        if ((r & 1) == 0)
        {
            if (SixLineLogic.EvenLeftColor[r] == ball) { bx = x + 0.5f; by = y + 0.5f; }
            else if (SixLineLogic.EvenRightColor[r] == ball) { bx = x + 1.5f; by = y + 0.5f; }
            else { bx = x + 1f; by = y + 1.5f; } // OddLeftColor[r]
        }
        else
        {
            if (SixLineLogic.OddLeftColor[r] == ball) { bx = x + 0.5f; by = y + 1.5f; }
            else if (SixLineLogic.OddRightColor[r] == ball) { bx = x + 1.5f; by = y + 1.5f; }
            else { bx = x + 1f; by = y + 0.5f; } // EvenRightColor[r]
        }
    }
}
