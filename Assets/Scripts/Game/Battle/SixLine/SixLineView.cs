/*--------------------------------------------------------------
 * File: SixLineView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

using System;

/// <summary>
/// 六连珠（表现层）：
/// - 创建 SixLineLogic，由驱动器以固定逻辑帧驱动（阶段 1 本地 = LocalDriver，1-7 接入；阶段 3 好友对战 = FrameSyncMgr.OnFrame），
///   逻辑层双驱动零改动（VS 已验证该模式）
/// - 本类只做组装与帧回调编排：棋盘/棋子视图对账、下落插值、特效与调试 HUD（1-6 起填充）
/// - 表现层读逻辑层数据只允许 Fix.AsFloat
/// </summary>
public class SixLineView : BattleView
{
    private SixLineLogic _logic;
    private long _seed;

    public override void Init()
    {
        // 阶段 1 本地对局：本地生成种子（同种子可重放验证 StateHash）；阶段 3 改读 Room.Seed（StartGamePush 下发）
        _seed = GenerateSeed();
        _logic = new SixLineLogic(_seed);

        // TODO(1-7) 创建 LocalDriver（双输入源：调试键盘 P1/P2 或同屏分区触摸）并订阅 OnFrame/OnRenderFrame；
        // 阶段 3 好友对战改订阅 GameMgr.FrameSync.OnFrame（种子/起始帧/参战玩家读 StartGamePush，SlInput 由 FrameData.Inputs 转换）

        Log.Info("[六连珠] 进入战斗(骨架), 种子:", _seed);
    }

    public override void Dispose()
    {
        // TODO(1-7) 退订驱动器事件；清理棋盘表现根节点
        _logic = null;
    }

    /// <summary>
    /// 每逻辑帧回调（驱动器在 Logic.Tick 后触发，形状对齐 FrameSyncMgr.OnFrame）：
    /// 灰盒阶段先做周期哈希日志（同种子重放逐帧对比验证确定性），1-6 起加视图对账/调试 HUD
    /// </summary>
    private void OnFrame(int frame)
    {
        if (frame % 50 == 0)
            Log.Info("[六连珠][校验] 帧:", frame, "哈希:", _logic.StateHash());
    }

    /// <summary>本地对局种子（联机阶段改为服务器下发，本方法删除）</summary>
    private static long GenerateSeed()
    {
        return DateTime.UtcNow.Ticks;
    }
}
