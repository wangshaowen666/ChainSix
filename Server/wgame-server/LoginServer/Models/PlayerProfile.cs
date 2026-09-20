namespace LoginServer.Models;

/// <summary>玩家养成数据（对应 PlayerProfile 表）</summary>
public class PlayerProfile
{
    public int PlayerId { get; set; }
    public int Gold { get; set; }
    public int StageProgress { get; set; }
    /// <summary>塔等级 JSON：{"tower_1": 3, "tower_2": 1}</summary>
    public string? TowerLevels { get; set; }
    public string? UpdatedAt { get; set; }
}
