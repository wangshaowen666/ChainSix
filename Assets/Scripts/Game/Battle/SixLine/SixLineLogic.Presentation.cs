using System;

public partial class SixLineLogic
{
    public const int SnapFrames = 2;
    public const int RollFrames = 2;
    public const int LandFrames = 1;
    public const int ClearFrames = 4;
    public static readonly Fix SettleGravity = Fix.FromDouble(0.48);

    private static Fix CellY(int row) => Fix.FromInt(row) * RowPitch + Half;

    private static int BallColorSlot(int rotation, int ball)
    {
        if ((rotation & 1) == 0)
            return ball == 0 ? EvenLeftColor[rotation] : ball == 1 ? EvenRightColor[rotation] : OddLeftColor[rotation];
        return ball == 0 ? OddLeftColor[rotation] : ball == 1 ? OddRightColor[rotation] : EvenRightColor[rotation];
    }

    private static void BallPosition(int rotation, Fix x, Fix y, int ball, out Fix bx, out Fix by)
    {
        BallOffset(rotation, ball, out var ox, out var oy);
        bx = x + ox;
        by = (y + oy - Half) * RowPitch + Half;
    }

    private void EnsurePieceIds(Player p)
    {
        for (int i = 0; i < TotalCells; i++)
        {
            if (p.Board.Cells[i] == EmptyColor)
                p.Board.PieceIds[i] = 0;
            else if (p.Board.PieceIds[i] == 0)
                p.Board.PieceIds[i] = p.NextPieceId++;
        }
    }

    private void BeginPresentation(Player p)
    {
        p.VisualEventCount = 0;
        p.VisualEndFrame = 0;
        p.PresentationFrame = LastTickFrame;
        EnsurePieceIds(p);
    }

    private static void RecordVisual(Player p, SlVisualKind kind, int id, int color, Fix fromX, Fix fromY,
        Fix toX, Fix toY, int start, int duration, Fix pivotX = default, Fix pivotY = default,
        Fix startSpeed = default, int chain = 0, bool special = false)
    {
        if (p.VisualEventCount >= p.VisualEvents.Length)
            throw new InvalidOperationException("六连珠结算轨迹缓冲区不足");
        p.VisualEvents[p.VisualEventCount++] = new SlVisualEvent
        {
            Kind = kind, PieceId = id, Color = color, FromX = fromX, FromY = fromY,
            ToX = toX, ToY = toY, StartFrame = start, DurationFrames = duration,
            PivotX = pivotX, PivotY = pivotY, StartSpeed = startSpeed, Chain = chain, Special = special,
        };
        p.VisualEndFrame = Math.Max(p.VisualEndFrame, start + duration);
    }

    private static int FallFrames(Fix distance, ref Fix speed)
    {
        var frames = 0;
        while (distance > Fix.Zero)
        {
            speed += SettleGravity;
            distance -= speed;
            frames++;
        }
        return Math.Max(1, frames);
    }
}
