using Dapper;
using LoginServer.Models;
using Microsoft.Data.Sqlite;

namespace LoginServer.Data;

/// <summary>玩家数据访问层</summary>
public class PlayerRepository
{
    private readonly string _connStr;

    public PlayerRepository(IConfiguration config)
    {
        _connStr = config.GetConnectionString("Default") ?? "Data Source=login.db";
    }

    /// <summary>建表（幂等）</summary>
    public void InitDatabase()
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS Player (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT UNIQUE NOT NULL,
                PasswordHash TEXT NOT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );");
    }

    /// <summary>按用户名查玩家</summary>
    public async Task<PlayerData?> GetByUsername(string username)
    {
        using var conn = new SqliteConnection(_connStr);
        return await conn.QueryFirstOrDefaultAsync<PlayerData>(
            "SELECT * FROM Player WHERE Username = @u", new { u = username });
    }

    /// <summary>按 Id 查玩家</summary>
    public async Task<PlayerData?> GetById(int id)
    {
        using var conn = new SqliteConnection(_connStr);
        return await conn.QueryFirstOrDefaultAsync<PlayerData>(
            "SELECT * FROM Player WHERE Id = @id", new { id });
    }

    /// <summary>创建玩家，返回新 Id</summary>
    public async Task<int> CreatePlayer(string username, string passwordHash)
    {
        using var conn = new SqliteConnection(_connStr);
        // 插入后立即查询 last_insert_rowid() 获取自增 Id
        var id = await conn.ExecuteScalarAsync<long>(
            "INSERT INTO Player (Username, PasswordHash) VALUES (@u, @p);" +
            "SELECT last_insert_rowid();",
            new { u = username, p = passwordHash });
        return (int)id;
    }
}
