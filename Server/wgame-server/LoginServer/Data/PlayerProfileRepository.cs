using Dapper;
using LoginServer.Models;
using Microsoft.Data.Sqlite;

namespace LoginServer.Data;

/// <summary>玩家养成数据访问层</summary>
public class PlayerProfileRepository
{
    private readonly string _connStr;

    public PlayerProfileRepository(IConfiguration config)
    {
        _connStr = config.GetConnectionString("Default") ?? "Data Source=login.db";
    }

    /// <summary>建表（幂等）</summary>
    public void InitDatabase()
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS PlayerProfile (
                PlayerId INTEGER PRIMARY KEY,
                Gold INTEGER DEFAULT 0,
                StageProgress INTEGER DEFAULT 0,
                TowerLevels TEXT DEFAULT '{}',
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (PlayerId) REFERENCES Player(Id)
            );");
    }

    /// <summary>按玩家 Id 查养成数据</summary>
    public async Task<PlayerProfile?> GetByPlayerId(int playerId)
    {
        using var conn = new SqliteConnection(_connStr);
        return await conn.QueryFirstOrDefaultAsync<PlayerProfile>(
            "SELECT * FROM PlayerProfile WHERE PlayerId = @id", new { id = playerId });
    }

    /// <summary>创建默认养成数据（注册时调用）</summary>
    public async Task CreateDefault(int playerId)
    {
        using var conn = new SqliteConnection(_connStr);
        await conn.ExecuteAsync(
            "INSERT INTO PlayerProfile (PlayerId) VALUES (@id)", new { id = playerId });
    }

    // 注：全量 Update 已随玩家侧 SaveData 接口移除（阶段 7-3 防作弊）。
    // 养成数据写入一律由服务器主导：GameServer 结算入账（ProfileStore）、
    // 未来的商店/升级等业务接口各自提供窄口径写入，不再开放客户端全量覆盖
}
