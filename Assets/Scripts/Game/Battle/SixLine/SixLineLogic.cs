/*--------------------------------------------------------------
 * File: SixLineLogic.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;

/// <summary>
/// 六连珠确定性逻辑层（纯 C#，无 UnityEngine）：
/// - 双棋盘对战：圆形棋子蜂窝错行摆放（相邻行错开半格，结构上禁止上下对齐），
///   每组正三角三连出生即持续下落（▲上1下2 / ▽上2下1 可旋转），同色 6 连（横/左斜/右斜三轴）
///   消除向对方投垃圾子，堆过高度线即负
/// - Tick(absFrame, inputs) 以绝对帧号锚定：同帧号 + 同操作序列 => 任意端状态完全一致
/// - 构造传入对局种子（阶段 1 本地生成；阶段 3 联机由 StartGamePush 下发，双端一致）
/// - 双驱动零改动：人机/本地双人 = SlLocalDriver（已接入），好友对战 = FrameSyncMgr（阶段 3）
/// - 确定性纪律：禁止 float/double、UnityEngine.Random/System.Random、Time.*、Dictionary 遍历、
///   Tick 内 new/装箱/LINQ/字符串拼接（数值全走 Fix + XRng，拒绝原因用枚举不用字符串）
/// </summary>
public class SixLineLogic
{
    // ---- 规则常量（1-8 迁入 Luban 调参表，先以常量跑通灰盒）----
    /// <summary>逻辑帧间隔（毫秒），与 FrameSyncMgr.LogicFrameMs 一致（50ms = 20 帧/秒）</summary>
    public const int LogicFrameMs = 50;

    public const int Rows = 12;         // 行数（row 0 = 底部）
    public const int DangerRows = 2;    // 顶部危险区行数（堆入即负，1-5 判定）
    public const int PlayerCount = 2;   // 对战人数（本地双人/人机/联机同构）
    public const int PreviewCount = 3;  // 出子队列预览组数
    public const int EmptyColor = 0;    // 空格
    public const int ColorMin = 1;      // 最小颜色 id
    public const int ColorMax = 5;      // 最大颜色 id（含），共 5 色（颜色数是难度总开关）
    public const int SlotCount = 9;     // 横向相位数（偶行对 Slot/Slot+1，奇行单颗对 Slot）
    public const int MoveIntervalFrames = 4; // 长按左右移动的步进间隔（帧）

    /// <summary>常速下落（格/帧）：0.08 格/帧 = 1.6 格/秒</summary>
    public static readonly Fix SlowFallSpeed = Fix.FromDouble(0.08);

    /// <summary>加速下落（格/帧）：按住加速抢节奏用，1 格/帧 = 20 格/秒</summary>
    public static readonly Fix FastFallSpeed = Fix.FromInt(1);

    // ---- 蜂窝错行几何（偶数行 10 格中心 x=col+0.5，奇数行 9 格中心 x=col+1，错开半格）----

    public static bool RowIsWide(int row) => (row & 1) == 0;
    public static int RowCells(int row) => RowIsWide(row) ? 10 : 9;

    /// <summary>行首扁平下标（偶行 10 格 + 奇行 9 格 = 每两行 19 格，共 114 格）</summary>
    public static int RowStart(int row) => (row >> 1) * 19 + (RowIsWide(row) ? 0 : 10);
    public const int TotalCells = 114;

    /// <summary>
    /// 下方左斜位扁平下标（1-3 消除的六邻接查询之一）；-1 = 越界（墙）。
    /// 偶行 (r,c) 斜下 = 奇行 (r-1,c-1)/(r-1,c)；奇行 (r,c) 斜下 = 偶行 (r-1,c)/(r-1,c+1)
    /// </summary>
    public static int BelowLeft(int row, int col)
    {
        if (row <= 0) return -1;
        if (RowIsWide(row))
            return col - 1 >= 0 ? RowStart(row - 1) + (col - 1) : -1;
        return RowStart(row - 1) + col;
    }

    /// <summary>下方右斜位扁平下标（1-3 消除的六邻接查询之一）；-1 = 越界（墙）</summary>
    public static int BelowRight(int row, int col)
    {
        if (row <= 0) return -1;
        if (RowIsWide(row))
            return col <= 8 ? RowStart(row - 1) + col : -1;
        return col + 1 <= 9 ? RowStart(row - 1) + (col + 1) : -1;
    }

    /// <summary>当前子状态</summary>
    public enum PieceState : byte
    {
        None,    // 无组（入格后的瞬时态）
        Falling, // 下落中（出生即下落，玩家移动/旋转/加速干预）
    }

    /// <summary>操作被拒原因（枚举替代字符串保证 Tick 零 GC；文案由表现层组装）</summary>
    public enum RejectReason : byte
    {
        None = 0,
        BadPlayerIndex, // 玩家下标非法
    }

    /// <summary>
    /// 下落三角组：Colors = 原始 ▲ 槽位色 [A=顶, B=左下, C=右下]；Rotation 0..5（偶=▲单颗在上，奇=▽单颗在下），
    /// 旋转只置换三颗颜色到列路径的映射（落位由重力决定，见 OddPathColor/EvenLeftColor/EvenRightColor）。
    /// Slot = 横向相位（偶行对 Slot/Slot+1，奇行单颗对 Slot）；Y = 偶行对所在行坐标（连续）
    /// </summary>
    public class Triomino
    {
        public readonly int[] Colors = new int[3];
        public int Rotation;
        public int Slot;
        public Fix Y;
        public PieceState State;
    }

    /// <summary>
    /// 旋转状态 -> 颜色槽映射（元素 = Colors 下标 0/1/2 = A/B/C）：{ 奇行路径, 偶行左路径, 偶行右路径 }。
    /// 顺时针一次 = ▲(A 顶/B 左下/C 右下) -> ▽(上 B/A，下 C)，与需求描述一致；6 次回环
    /// </summary>
    public static readonly int[] OddPathColor = { 0, 2, 1, 0, 2, 1 };
    public static readonly int[] EvenLeftColor = { 1, 1, 2, 2, 0, 0 };
    public static readonly int[] EvenRightColor = { 2, 0, 0, 1, 1, 2 };

    /// <summary>棋盘：蜂窝错行扁平格子（行优先，下标经 RowStart/RowCells 换算）</summary>
    public class Board
    {
        public readonly int[] Cells = new int[TotalCells];
        public int Get(int row, int col) => Cells[RowStart(row) + col];
        public void Set(int row, int col, int color) => Cells[RowStart(row) + col] = color;
    }

    /// <summary>参战方（双方各一块棋盘）</summary>
    public class Player
    {
        public int PlayerIndex;
        public readonly Board Board = new Board();
        public readonly List<int> Queue = new List<int>(PreviewCount * 3); // 待出组颜色（每 3 个一组：A/B/C），队首组即预览
        public readonly Triomino Tri = new Triomino();
        public bool MoveLeftHeld;  // 本帧长按标志（应用输入置位，下落步消费后复位）
        public bool MoveRightHeld;
        public bool FastDropHeld;
    }

    public readonly List<Player> Players = new List<Player>(PlayerCount);

    public bool GameOver { get; private set; }
    public int LastTickFrame { get; private set; }

    /// <summary>最近一次被拒操作原因（诊断用，不影响确定性）</summary>
    public RejectReason LastReject { get; private set; }

    private readonly XRng _rng;

    public SixLineLogic(long seed)
    {
        _rng = new XRng((ulong)seed);

        for (int i = 0; i < PlayerCount; i++)
        {
            var p = new Player { PlayerIndex = i };
            FillQueue(p);
            DrawNext(p); // 首组（出生即下落）
            Players.Add(p);
        }
    }

    /// <summary>
    /// 推进一逻辑帧（帧号从 1 连续递增，空帧也要 tick）。步骤固定序即确定性本身，
    /// 后续任务按下列编排填实现，禁止调整步骤顺序：
    /// 1. 应用输入（长按移动/加速、点按旋转）(1-2)
    /// 2. 三角组移动与下落（触底落定，三颗各自沿列路径垂直堆叠到最低空位）(1-2)
    /// 3. 消除与连锁（6 连横/左斜/右斜三轴扫描 -> 消除 -> 重力下落 -> 落定复扫，单 Tick 内递归到稳定）(1-3)
    /// 4. 垃圾子投放（向对方棋盘投随机色子，单线 3/双线十字 5/连锁每级 +2）(1-4)
    /// 5. 胜负判定（任一棋盘堆过高度线 GameOver，区分胜负方）(1-5)
    /// </summary>
    public void Tick(int absFrame, IList<SlInput> inputs)
    {
        if (GameOver) return;
        LastTickFrame = absFrame;

        // 1. 应用输入（按帧内顺序，双端一致）
        if (inputs != null)
            for (int i = 0; i < inputs.Count; i++)
                ApplyInput(inputs[i]);

        // 2. 三角组移动与下落
        for (int i = 0; i < Players.Count; i++)
        {
            FallStep(absFrame, Players[i]);
            Players[i].MoveLeftHeld = false;  // 长按均为帧内瞬时标志
            Players[i].MoveRightHeld = false;
            Players[i].FastDropHeld = false;
        }

        // TODO(1-3) 3. 消除与连锁
        // TODO(1-4) 4. 垃圾子投放
        // TODO(1-5) 5. 胜负判定
    }

    private void ApplyInput(SlInput input)
    {
        if (input.PlayerIndex < 0 || input.PlayerIndex >= Players.Count)
        {
            LastReject = RejectReason.BadPlayerIndex;
            return;
        }

        var p = Players[input.PlayerIndex];
        var tri = p.Tri;
        switch (input.OpType)
        {
            case SlOp.MoveLeft: // 长按：按住期间每帧发送
                p.MoveLeftHeld = true;
                break;
            case SlOp.MoveRight:
                p.MoveRightHeld = true;
                break;
            case SlOp.RotateCW: // 点按：顺时针 60°（▲/▽ 互转 + 颜色轮换）
                tri.Rotation = (tri.Rotation + 1) % 6;
                break;
            case SlOp.RotateCCW: // 点按：逆时针 60°
                tri.Rotation = (tri.Rotation + 5) % 6;
                break;
            case SlOp.FastDrop: // 长按：按住期间每帧发送
                p.FastDropHeld = true;
                break;
        }
    }

    /// <summary>
    /// 三角组移动与下落：长按按固定间隔步进槽位（同帧同按先左后右，确定性）；
    /// 任一颗到达其落位行（取最低触地者）即整组落定，三颗各自沿列路径垂直堆叠到最低空位
    /// </summary>
    private void FallStep(int absFrame, Player p)
    {
        var tri = p.Tri;
        if (tri.State != PieceState.Falling)
            return;

        // 长按左右移动（间隔步进，夹在棋盘内）
        if (absFrame % MoveIntervalFrames == 0)
        {
            if (p.MoveLeftHeld)
                tri.Slot--;
            else if (p.MoveRightHeld)
                tri.Slot++;
            if (tri.Slot < 0)
                tri.Slot = 0;
            if (tri.Slot >= SlotCount)
                tri.Slot = SlotCount - 1;
        }

        tri.Y -= p.FastDropHeld ? FastFallSpeed : SlowFallSpeed;

        // 触底判定：三颗各自的路径最低空行，按"先触者"取最大（▲ 单颗在上 rest-1 触发 / ▽ 单颗在下 rest+1 触发）
        var restOdd = LowestEmptyRow(p.Board, tri.Slot, false);
        var restL = LowestEmptyRow(p.Board, tri.Slot, true);
        var restR = LowestEmptyRow(p.Board, tri.Slot + 1, true);
        var oddStop = restOdd + ((tri.Rotation & 1) == 0 ? -1 : 1);
        var stop = restL > restR ? restL : restR;
        if (oddStop > stop)
            stop = oddStop;
        if (restOdd < 0 || restL < 0 || restR < 0)
            stop = Rows; // 满列：立即落定（残局由 1-5 判负收尾）

        if (tri.Y <= Fix.FromInt(stop))
            LandTriomino(p);
    }

    /// <summary>
    /// 落定：三颗按当前旋转的颜色映射，各自落到列路径最低空位；路径已满（-1）则该颗不落（1-5 判负收尾）。
    /// 平地上 ▽ 的下单颗会因奇行路径最低为 row1 而呈现"翻正"（灰盒已知表现，1-9 体感评估）
    /// </summary>
    private void LandTriomino(Player p)
    {
        var tri = p.Tri;
        var r = tri.Rotation;
        var rowOdd = LowestEmptyRow(p.Board, tri.Slot, false);
        var rowL = LowestEmptyRow(p.Board, tri.Slot, true);
        var rowR = LowestEmptyRow(p.Board, tri.Slot + 1, true);
        if (rowOdd >= 0)
            p.Board.Set(rowOdd, tri.Slot, tri.Colors[OddPathColor[r]]);
        if (rowL >= 0)
            p.Board.Set(rowL, tri.Slot, tri.Colors[EvenLeftColor[r]]);
        if (rowR >= 0)
            p.Board.Set(rowR, tri.Slot + 1, tri.Colors[EvenRightColor[r]]);

        tri.State = PieceState.None;
        DrawNext(p);
    }

    /// <summary>
    /// 路径最低空行（垂直堆叠位）：该列相位自底向上第一个空行，-1 = 路径已满。
    /// 堆叠恒被路径下方圆子垂直承接（路径紧凑不变量），1-3 消除后的重力下落复用同一规则
    /// </summary>
    private static int LowestEmptyRow(Board b, int col, bool evenPath)
    {
        for (int r = evenPath ? 0 : 1; r < Rows; r += 2)
            if (b.Get(r, col) == EmptyColor)
                return r;
        return -1;
    }

    /// <summary>从队列头出一组（3 色：A/B/C）为当前三角组（出生即下落），并补充队列至预览组数（XRng 独立出色）</summary>
    private void DrawNext(Player p)
    {
        var tri = p.Tri;
        tri.Colors[0] = p.Queue[0];
        tri.Colors[1] = p.Queue[1];
        tri.Colors[2] = p.Queue[2];
        p.Queue.RemoveAt(2);
        p.Queue.RemoveAt(1);
        p.Queue.RemoveAt(0);
        FillQueue(p);
        tri.Rotation = 0;
        tri.Slot = SlotCount / 2;   // 默认中间槽位出生
        tri.Y = Fix.FromInt(Rows);  // 从棋盘顶部之上起落
        tri.State = PieceState.Falling;
    }

    private void FillQueue(Player p)
    {
        while (p.Queue.Count < PreviewCount * 3)
            p.Queue.Add(_rng.NextInt(ColorMin, ColorMax + 1));
    }

    /// <summary>
    /// 全量状态哈希（FNV-1a 64）：双端/同种子重放逐帧对比此值验证确定性。
    /// 新增哈希字段只允许追加到方法尾部，禁止重排已有混合顺序
    /// </summary>
    public ulong StateHash()
    {
        var h = 14695981039346656037UL;
        Mix(ref h, LastTickFrame);
        Mix(ref h, GameOver ? 1L : 0L);

        // 1-2 玩家状态（玩家序 -> 行优先格子序 -> 队列序 -> 三角组字段序）
        for (int i = 0; i < Players.Count; i++)
        {
            var p = Players[i];
            Mix(ref h, p.PlayerIndex);
            for (int c = 0; c < TotalCells; c++)
                Mix(ref h, p.Board.Cells[c]);
            for (int q = 0; q < p.Queue.Count; q++)
                Mix(ref h, p.Queue[q]);
            Mix(ref h, p.Tri.Colors[0]);
            Mix(ref h, p.Tri.Colors[1]);
            Mix(ref h, p.Tri.Colors[2]);
            Mix(ref h, p.Tri.Slot);
            Mix(ref h, p.Tri.Y.Raw);
            Mix(ref h, (int)p.Tri.State);
            Mix(ref h, p.Tri.Rotation); // 1-2 追加：旋转状态
        }

        // TODO(1-3 起) 消除/连锁/垃圾子等状态按固定序追加
        return h;
    }

    private static void Mix(ref ulong h, long v)
    {
        h = (h ^ (ulong)v) * 1099511628211UL;
    }
}
