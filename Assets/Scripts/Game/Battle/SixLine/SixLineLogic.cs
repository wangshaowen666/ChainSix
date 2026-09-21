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
/// - Puyo 式双棋盘对战：双方各下各的棋盘，同色 6 连（横/竖/斜）消除向对方投垃圾子，堆过高度线即负
/// - Tick(absFrame, inputs) 以绝对帧号锚定：同帧号 + 同操作序列 => 任意端状态完全一致
/// - 构造传入对局种子（阶段 1 本地生成；阶段 3 联机由 StartGamePush 下发，双端一致）
/// - 双驱动零改动：人机/本地双人 = LocalDriver（1-7 接入），好友对战 = FrameSyncMgr（阶段 3）
/// - 确定性纪律：禁止 float/double、UnityEngine.Random/System.Random、Time.*、Dictionary 遍历、
///   Tick 内 new/装箱/LINQ/字符串拼接（数值全走 Fix + XRng，实体走池化）
/// </summary>
public class SixLineLogic
{
    /// <summary>逻辑帧间隔（毫秒），与 FrameSyncMgr.LogicFrameMs 一致（50ms = 20 帧/秒）</summary>
    public const int LogicFrameMs = 50;

    public bool GameOver { get; private set; }
    public int LastTickFrame { get; private set; }

    private readonly XRng _rng;

    public SixLineLogic(long seed)
    {
        _rng = new XRng((ulong)seed);
    }

    /// <summary>
    /// 推进一逻辑帧（帧号从 1 连续递增，空帧也要 tick）。步骤固定序即确定性本身，
    /// 后续任务按下列编排填实现，禁止调整步骤顺序：
    /// 1. 应用输入（选列落子/加速）(1-2)
    /// 2. 当前子固定步长下落（Fix 速度，加速落地、触底入格）(1-2)
    /// 3. 消除与连锁（6 连横/竖/斜扫描 -> 消除 -> 重力下落 -> 落定复扫，单 Tick 内递归到稳定）(1-3)
    /// 4. 垃圾子投放（向对方棋盘投随机色子，单线 3/双线十字 5/连锁每级 +2）(1-4)
    /// 5. 胜负判定（任一棋盘堆过高度线 GameOver，区分胜负方）(1-5)
    /// </summary>
    public void Tick(int absFrame, IList<SlInput> inputs)
    {
        if (GameOver) return;
        LastTickFrame = absFrame;

        // TODO(1-2) 1. 应用输入
        // TODO(1-2) 2. 当前子下落
        // TODO(1-3) 3. 消除与连锁
        // TODO(1-4) 4. 垃圾子投放
        // TODO(1-5) 5. 胜负判定
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
        // TODO(1-2 起) 棋盘格子/出子队列/当前子/垃圾子等状态按固定序追加
        return h;
    }

    private static void Mix(ref ulong h, long v)
    {
        h = (h ^ (ulong)v) * 1099511628211UL;
    }
}
