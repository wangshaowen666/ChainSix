using System;
using UnityEngine;

public partial class SixLineView
{
    private void SyncPlayer(SixLineLogic.Player p, BoardView bv)
    {
        if (!bv.Initialized)
        {
            for (int r = 0; r < _logic.Rows; r++)
                for (int c = 0; c < _logic.RowCells(r); c++)
                {
                    var i = _logic.RowStart(r) + c;
                    if (p.Board.PieceIds[i] != 0)
                        GetCircle(bv, p.Board.PieceIds[i], p.Board.Cells[i], CellLocal(r, c, bv.Cell, .1f));
                }
            bv.Initialized = true;
        }

        if (p.PresentationFrame != bv.LastPresentationFrame && p.VisualEventCount > 0)
        {
            if (bv.Playing)
                FinishPresentation(bv);
            bv.LastPresentationFrame = p.PresentationFrame;
            bv.EventCount = p.VisualEventCount;
            bv.EventEndTime = p.VisualEndFrame * LogicStepSeconds;
            bv.EventTime = 0f;
            bv.LastChain = 0;
            bv.Playing = true;
            bv.TriActive = false;
            Array.Clear(bv.TriAnims, 0, bv.TriAnims.Length);
            Array.Copy(p.VisualEvents, bv.Events, bv.EventCount);
            Array.Clear(bv.Started, 0, bv.EventCount);
            Array.Clear(bv.Done, 0, bv.EventCount);
            Array.Copy(p.Board.PieceIds, bv.FinalIds, _logic.TotalCells);
            Array.Copy(p.Board.Cells, bv.FinalColors, _logic.TotalCells);
            for (int i = 0; i < bv.EventCount; i++)
            {
                var e = bv.Events[i];
                bv.EventFrom[i] = e.Kind == SlVisualKind.Place && bv.Live.TryGetValue(e.PieceId, out var a)
                    ? a.Pos : new Vector3(e.FromX.AsFloat * bv.Cell, e.FromY.AsFloat * bv.Cell, .1f);
            }
        }

        if (bv.Playing || p.Tri.State == SixLineLogic.PieceState.None)
            return;
        var tri = p.Tri;
        var center = new Vector2((tri.X.AsFloat + 1f) * bv.Cell,
            GridY(tri.Y.AsFloat + ((tri.Rotation & 1) == 0 ? 5f / 6f : 7f / 6f)) * bv.Cell);
        if (!bv.TriActive || bv.TriSeqPos != p.SeqPos)
        {
            bv.TriActive = true;
            bv.TriSeqPos = p.SeqPos;
            bv.FromCenter = bv.ToCenter = center;
            bv.TriAngle = -tri.RotationTurns * 60f;
            for (int b = 0; b < 3; b++)
                bv.TriAnims[b] = GetCircle(bv, tri.PieceIds[b], tri.Colors[b], center);
        }
        else
        {
            bv.FromCenter = bv.ToCenter;
            bv.ToCenter = center;
        }
        bv.TargetAngle = -tri.RotationTurns * 60f;
    }

    private CircleAnim GetCircle(BoardView bv, int id, int color, Vector3 position)
    {
        if (bv.Live.TryGetValue(id, out var circle))
            return circle;
        if (bv.Free.Count > 0)
        {
            var last = bv.Free.Count - 1;
            circle = bv.Free[last];
            bv.Free.RemoveAt(last);
        }
        else
        {
            var tr = MakeCircle(bv.Root, "Circle", bv.Cell * .96f, Color.white);
            circle = new CircleAnim { Tr = tr, Mr = tr.GetComponent<MeshRenderer>() };
        }
        circle.Id = id;
        circle.Color = color;
        circle.Pos = position;
        circle.Tr.localPosition = position;
        circle.Tr.localScale = Vector3.one * (bv.Cell * .96f);
        circle.Mr.sharedMaterial = ViewMats.GetMat(s_palette[color - 1]);
        circle.Tr.gameObject.SetActive(true);
        bv.Live.Add(id, circle);
        return circle;
    }

    private static void RecycleCircle(BoardView bv, CircleAnim circle)
    {
        bv.Live.Remove(circle.Id);
        circle.Tr.gameObject.SetActive(false);
        circle.Id = 0;
        bv.Free.Add(circle);
    }

    private void FinishPresentation(BoardView bv)
    {
        PlayEvents(bv, float.MaxValue);
        bv.Playing = false;
        bv.ReconcileStamp++;
        for (int r = 0; r < _logic.Rows; r++)
            for (int c = 0; c < _logic.RowCells(r); c++)
            {
                var index = _logic.RowStart(r) + c;
                var id = bv.FinalIds[index];
                if (id == 0)
                    continue;
                var a = GetCircle(bv, id, bv.FinalColors[index], CellLocal(r, c, bv.Cell, .1f));
                a.Pos = CellLocal(r, c, bv.Cell, .1f);
                a.Tr.localPosition = a.Pos;
                a.Tr.localScale = Vector3.one * (bv.Cell * .96f);
                a.Mr.sharedMaterial = ViewMats.GetMat(s_palette[a.Color - 1]);
                a.Stamp = bv.ReconcileStamp;
            }
        bv.Recycle.Clear();
        foreach (var pair in bv.Live)
            if (pair.Value.Stamp != bv.ReconcileStamp)
                bv.Recycle.Add(pair.Value);
        for (int i = 0; i < bv.Recycle.Count; i++)
            RecycleCircle(bv, bv.Recycle[i]);
    }

    private static Vector3 CellLocal(int row, int col, float cell, float z)
    {
        return new Vector3((col + .5f + (SixLineLogic.RowIsWide(row) ? 0f : .5f)) * cell,
            GridY(row + .5f) * cell, z);
    }
}
