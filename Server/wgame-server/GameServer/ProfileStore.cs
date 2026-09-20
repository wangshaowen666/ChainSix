using Dapper;
using Microsoft.Data.Sqlite;

namespace GameServer;

/// <summary>
/// 玩家养成数据持久化（结算入账）：
/// - 与 LoginServer 共享同一个 login.db（相对路径见 appsettings.json ProfileDb；
///   启动时多根目录探测定位，见 ResolveDbPath，不依赖进程工作目录）
/// - 启动时开 WAL 模式：库级持久设置，读写不互斥，两个进程并发访问不再互锁
///   （不开 WAL 时默认 journal 模式写锁粒度大，LoginServer 写档与结算入账并发会 database is locked）
/// - AddSettlement 事务入账：所有成员要么全部到账要么全部不动；金币用 Gold = Gold + x 原子自增（非读改写）
/// </summary>
public sealed class ProfileStore
{
    private readonly string _connStr;

    /// <param name="dbPath">login.db 路径（相对路径按开发目录结构探测，也可直接配绝对路径）</param>
    public ProfileStore(string dbPath)
    {
        var resolved = ResolveDbPath(dbPath);
        _connStr = $"Data Source={resolved};Default Timeout=5";

        using (var conn = new SqliteConnection(_connStr))
        {
            conn.Execute("PRAGMA journal_mode=WAL;"); // 幂等，库级持久生效

            // 表存在性检查：GameServer 不负责建表（LoginServer 注册时建），缺表直接报错
            var exists = conn.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PlayerProfile'");
            if (exists == 0)
                throw new InvalidOperationException(
                    $"PlayerProfile 表不存在，请先启动 LoginServer 完成建表。db 路径: {resolved}");
        }

        Console.WriteLine($"[持久化] ProfileStore 就绪, db: {resolved}, WAL 已开启");
    }

    /// <summary>
    /// 解析 db 路径：相对路径时依次尝试"当前工作目录"与"程序输出目录逐级向上"作为基准
    /// （IDE 运行/dotnet run/直接跑 exe 的工作目录各不相同，靠探测兜住；LoginServer 与 GameServer
    /// 平级的开发目录结构下必命中）。找到已存在的文件用其绝对路径；找不到保留原值交给 SQLite 报错
    /// </summary>
    private static string ResolveDbPath(string rawPath)
    {
        if (Path.IsPathRooted(rawPath)) return rawPath;

        var roots = new List<string> { Directory.GetCurrentDirectory() };
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            roots.Add(dir.FullName);
            dir = dir.Parent!;
        }

        foreach (var root in roots)
        {
            var candidate = Path.GetFullPath(Path.Combine(root, rawPath));
            if (File.Exists(candidate))
                return candidate;
        }
        return rawPath;
    }

    /// <summary>
    /// 结算入账：事务内给所有成员加金币（失败整体回滚并打日志，不影响游戏结束流程）
    /// </summary>
    public void AddSettlement(List<int> playerIds, int goldReward)
    {
        try
        {
            using var conn = new SqliteConnection(_connStr);
            conn.Open();
            using var tx = conn.BeginTransaction();
            foreach (var pid in playerIds)
            {
                conn.Execute(
                    "UPDATE PlayerProfile SET Gold = Gold + @gold, UpdatedAt = CURRENT_TIMESTAMP WHERE PlayerId = @id",
                    new { gold = goldReward, id = pid }, tx);
            }
            tx.Commit();
            Console.WriteLine($"[持久化] 结算入账: [{string.Join(",", playerIds)}] 各 +{goldReward} 金币");
        }
        catch (Exception e)
        {
            // 持久化失败不阻断游戏结束（奖励照发、房间照常重置），记日志排查
            Console.WriteLine($"[持久化] 结算入账失败: {e.Message}");
        }
    }
}
