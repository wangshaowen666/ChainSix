namespace GameServer;

/// <summary>
/// 结算：游戏结束的合法性校验与奖励计算。
/// 防作弊原则：
/// - 客户端只能报告"打完了"（end_frame），奖励数值一律服务器推导，上报无决定力
/// - end_frame 越界判定为作弊嫌疑：奖励 0 且不入账（客户端消费的帧必来自服务器广播，
///   正常流程 end_frame <= 房间当前帧恒成立，越界只可能是伪造）
/// - 奖励封顶：防极端长局/异常值（波次玩法配置化后随公式调整）
/// </summary>
public static class Settlement
{
    /// <summary>波次间隔帧数（与客户端 BattleLogic.SpawnEveryFrames 一致；波次玩法 Excel 配置化后统一收口此处）</summary>
    private const int SpawnEveryFrames = 60;

    /// <summary>保底奖励（进了游戏就给）</summary>
    private const int BaseReward = 20;

    /// <summary>每坚持一波的奖励</summary>
    private const int RewardPerWave = 10;

    /// <summary>单局奖励上限（封顶）</summary>
    private const int MaxReward = 1000;

    /// <summary>
    /// 校验结束帧：合法区间 [startFrame, roomFrame]（不可能早于开局帧，也不可能超过服务器已广播的帧）。
    /// 返回 true=合法（clamped=endFrame）；false=越界（clamped 为钳制值，仅供广播展示对齐双端，奖励 0 不入账）
    /// </summary>
    public static bool ValidateEndFrame(int endFrame, int startFrame, int roomFrame, out int clamped)
    {
        if (endFrame < startFrame || endFrame > roomFrame)
        {
            Console.WriteLine($"[结算拒绝] end_frame={endFrame} 越界 [start={startFrame}, cur={roomFrame}]，判定作弊嫌疑，奖励 0 不入账");
            clamped = Math.Clamp(endFrame, startFrame, roomFrame);
            return false;
        }
        clamped = endFrame;
        return true;
    }

    /// <summary>计算奖励金币：保底 + 坚持波次 x 单波奖励，封顶 MaxReward（波次玩法配置化后公式在此调整）</summary>
    public static int CalcReward(int startFrame, int endFrame)
    {
        var waves = (endFrame - startFrame) / SpawnEveryFrames;
        return Math.Min(BaseReward + waves * RewardPerWave, MaxReward);
    }
}
