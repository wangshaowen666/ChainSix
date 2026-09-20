namespace LoginServer.Models;

/// <summary>玩家数据（对应 Player 表）</summary>
public class PlayerData
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public string? CreatedAt { get; set; }
}
