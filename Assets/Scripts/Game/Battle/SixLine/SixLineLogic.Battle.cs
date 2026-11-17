using System;

public partial class SixLineLogic
{
    private void ResolveGarbage()
    {
        // 双方先结算攻击，再投放；先遍历的玩家不会抢先结束对手的同帧操作。
        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            var attack = 0;
            for (int r = 0; r < p.RoundCount; r++)
            {
                var units = Math.Max(p.RoundClusters[r], p.RoundPatterns[r]);
                if (units == 0)
                    continue;
                attack += Rules.SingleGarbage + (units - 1) * (Rules.DoubleGarbage - Rules.SingleGarbage)
                    + r * Rules.ChainGarbage;
            }
            p.SentThisFrame = attack;
            p.TotalSent += attack;
            var opponent = Players[1 - i];
            // 超出棋盘容量的待投仍保留一个溢出标记，无需储存不可能装下的无界队列。
            opponent.PendingGarbage = Math.Min(TotalCells + 1, opponent.PendingGarbage + attack);
        }

        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            if (!p.LockedThisFrame || p.PendingGarbage == 0)
                continue;
            var count = p.PendingGarbage;
            p.PendingGarbage = 0;
            var start = p.VisualEndFrame;
            for (int c = 0; c < TotalCells; c++)
                p.CellReady[c] = start;
            for (int n = 0; n < count; n++)
            {
                var row = Rows - 1;
                var col = _rng.NextInt(RowCells(row));
                var color = _rng.NextInt(ColorMin, ColorMax + 1);
                if (p.Board.Get(row, col) != EmptyColor)
                {
                    p.Overflow = true;
                    break;
                }
                DropGarbage(p, col, color, start + n);
                p.ReceivedThisFrame++;
                p.TotalReceived++;
            }
            // 垃圾可能为对手补色，但接收当帧不反击，等其下一次落子统一扫描。
            SettlePass(p);
        }
    }

    private void CheckGameOver()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            p.Lost = p.Overflow;
            for (int r = Rows - DangerRows; r < Rows && !p.Lost; r++)
                for (int c = 0; c < RowCells(r); c++)
                    if (p.Board.Get(r, c) != EmptyColor)
                    {
                        p.Lost = true;
                        break;
                    }
        }

        if (!Players[0].Lost && !Players[1].Lost)
            return;
        GameOver = true;
        WinnerIndex = Players[0].Lost == Players[1].Lost ? -1 : Players[0].Lost ? 1 : 0;
    }
}
