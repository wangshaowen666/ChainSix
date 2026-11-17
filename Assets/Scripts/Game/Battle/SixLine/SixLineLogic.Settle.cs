using System;

public partial class SixLineLogic
{
    private void SettlePass(Player p)
    {
        EnsurePieceIds(p);
        var cells = p.Board.Cells;
        // 邻球先落走后，原先被侧面夹住的球可能重新失稳，需要继续复扫。
        for (int pass = 0; pass < Rows + Columns; pass++)
        {
            var start = p.VisualEndFrame;
            for (int i = 0; i < TotalCells; i++)
                p.CellReady[i] = start;
            var moved = false;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < RowCells(r); c++)
                {
                    var index = RowStart(r) + c;
                    var color = cells[index];
                    if (color == EmptyColor)
                        continue;
                    var id = p.Board.PieceIds[index];
                    cells[index] = EmptyColor;
                    p.Board.PieceIds[index] = 0;
                    moved |= SettleOne(p, r, c, color, id, start);
                }
            if (!moved)
                break;
        }
    }

    private bool SettleOne(Player p, int row, int col, int color, int id, int start)
    {
        var cells = p.Board.Cells;
        var time = start;
        var speed = Fix.Zero;
        var moved = false;
        while (row > 0)
        {
            var bl = Neighbor(row, col, 5);
            var br = Neighbor(row, col, 4);
            var blOcc = bl >= 0 && cells[bl] != EmptyColor;
            var brOcc = br >= 0 && cells[br] != EmptyColor;
            if (blOcc && brOcc)
                break;
            if (blOcc != brOcc)
            {
                var target = blOcc ? br : bl;
                if (target < 0 || !ArcFree(cells, blOcc ? bl : br, CellCenterX(row, col), CellY(row),
                    CellCenterX(_rowOf[target], _colOf[target]), CellY(_rowOf[target])))
                    break;
                MovePiece(p, SlVisualKind.Roll, id, color, CellCenterX(row, col), CellY(row),
                    CellCenterX(_rowOf[target], _colOf[target]), CellY(_rowOf[target]),
                    RowStart(row) + col, target, blOcc ? bl : br, ref time, ref speed);
                row = _rowOf[target];
                col = _colOf[target];
                moved = true;
                continue;
            }

            var nr = row - 2;
            if (nr < 0)
            {
                var floor = bl >= 0 ? bl : br;
                MovePiece(p, SlVisualKind.Fall, id, color, CellCenterX(row, col), CellY(row),
                    CellCenterX(_rowOf[floor], _colOf[floor]), CellY(_rowOf[floor]),
                    RowStart(row) + col, floor, -1, ref time, ref speed);
                row = _rowOf[floor];
                col = _colOf[floor];
                moved = true;
                continue;
            }

            var below = RowStart(nr) + col;
            if (cells[below] != EmptyColor)
            {
                var leftV = bl >= 0 && VAt(cells, _rowOf[bl], _colOf[bl]);
                var rightV = br >= 0 && VAt(cells, _rowOf[br], _colOf[br]);
                var pivotX = CellCenterX(nr, col);
                var crestY = CellY(nr) + Fix.One;
                var leftFree = bl >= 0 && ArcFree(cells, below, pivotX, crestY,
                    CellCenterX(_rowOf[bl], _colOf[bl]), CellY(_rowOf[bl]));
                var rightFree = br >= 0 && ArcFree(cells, below, pivotX, crestY,
                    CellCenterX(_rowOf[br], _colOf[br]), CellY(_rowOf[br]));
                var target = leftV && leftFree ? bl : rightV && rightFree ? br : leftFree ? bl : rightFree ? br : -1;
                if (target < 0)
                    break;
                // 先落到支撑球的真实顶部，再绕支撑球滚下，不能斜穿支撑球。
                MovePiece(p, SlVisualKind.Fall, id, color, CellCenterX(row, col), CellY(row),
                    pivotX, crestY, RowStart(row) + col, -1, below, ref time, ref speed);
                MovePiece(p, SlVisualKind.Roll, id, color, pivotX, crestY,
                    CellCenterX(_rowOf[target], _colOf[target]), CellY(_rowOf[target]),
                    -1, target, below, ref time, ref speed);
                row = _rowOf[target];
                col = _colOf[target];
                moved = true;
                continue;
            }

            MovePiece(p, SlVisualKind.Fall, id, color, CellCenterX(row, col), CellY(row),
                CellCenterX(nr, col), CellY(nr), RowStart(row) + col, below, -1, ref time, ref speed);
            row = nr;
            moved = true;
        }

        var destination = RowStart(row) + col;
        cells[destination] = color;
        p.Board.PieceIds[destination] = id;
        var newPiece = id == p.Tri.PieceIds[0] || id == p.Tri.PieceIds[1] || id == p.Tri.PieceIds[2];
        if (moved || (newPiece && start == SnapFrames))
        {
            RecordVisual(p, SlVisualKind.Land, id, color, CellCenterX(row, col), CellY(row),
                CellCenterX(row, col), CellY(row), time, LandFrames);
            time += LandFrames;
        }
        p.CellReady[destination] = time;
        return moved;
    }

    private bool ArcFree(int[] cells, int pivot, Fix fromX, Fix fromY, Fix toX, Fix toY)
    {
        var px = CellCenterX(_rowOf[pivot], _colOf[pivot]);
        var py = CellY(_rowOf[pivot]);
        for (int sample = 1; sample <= 3; sample++)
        {
            var t = Fix.FromInt(sample) / Fix.FromInt(4);
            var vx = (fromX - px) * (Fix.One - t) + (toX - px) * t;
            var vy = (fromY - py) * (Fix.One - t) + (toY - py) * t;
            var radius = Fix.Sqrt(vx * vx + vy * vy);
            var x = px + vx / radius;
            var y = py + vy / radius;
            for (int i = 0; i < TotalCells; i++)
            {
                if (i == pivot || cells[i] == EmptyColor)
                    continue;
                var dx = x - CellCenterX(_rowOf[i], _colOf[i]);
                var dy = y - CellY(_rowOf[i]);
                if (dx * dx + dy * dy < BallTouchSq - Fix.FromRaw(8))
                    return false;
            }
        }
        return true;
    }

    private void MovePiece(Player p, SlVisualKind kind, int id, int color, Fix fromX, Fix fromY,
        Fix toX, Fix toY, int source, int destination, int pivot, ref int time, ref Fix speed)
    {
        if (destination >= 0)
            time = Math.Max(time, p.CellReady[destination]);
        if (pivot >= 0)
            time = Math.Max(time, p.CellReady[pivot]);
        var startSpeed = speed;
        var duration = kind == SlVisualKind.Roll ? RollFrames : FallFrames(fromY - toY, ref speed);
        if (kind == SlVisualKind.Roll)
            speed = Fix.Zero;
        RecordVisual(p, kind, id, color, fromX, fromY, toX, toY, time, duration,
            pivot >= 0 ? CellCenterX(_rowOf[pivot], _colOf[pivot]) : Fix.Zero,
            pivot >= 0 ? CellY(_rowOf[pivot]) : Fix.Zero, startSpeed);
        time += duration;
        if (source >= 0)
            p.CellReady[source] = Math.Max(p.CellReady[source], time);
        if (destination >= 0)
            p.CellReady[destination] = time;
    }

    private void DropGarbage(Player p, int col, int color, int start)
    {
        var row = Rows - 1;
        var id = p.NextPieceId++;
        var time = Math.Max(start, p.CellReady[RowStart(row) + col]);
        var speed = Fix.Zero;
        MovePiece(p, SlVisualKind.Fall, id, color, CellCenterX(row, col), CellY(Rows) + Fix.One,
            CellCenterX(row, col), CellY(row), -1, RowStart(row) + col, -1, ref time, ref speed);
        SettleOne(p, row, col, color, id, time);
    }

    private bool VAt(int[] cells, int row, int col)
    {
        var a = Neighbor(row, col, 5);
        var b = Neighbor(row, col, 4);
        return a >= 0 && b >= 0 && cells[a] != EmptyColor && cells[b] != EmptyColor;
    }
}
