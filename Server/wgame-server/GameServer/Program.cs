using System.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace GameServer;

/// <summary>
/// 帧同步游戏服务器入口：装配各模块并驱动主循环，不含业务逻辑。
/// - 模块：NetServer（网络）/ MsgDispatcher（分发）/ SessionMgr（会话+顶号）/
///   RoomMgr（房间）/ MatchMaker（撮合）/ Handlers/（各域消息处理）、JwtValidator
/// - 新增协议消息：在对应 Handler 注册即可，本文件与分发器均无需改动
/// - 逻辑帧循环：每 50ms 把期间收到的所有玩家操作打包成一帧按房间广播（空帧也发，保证帧号连续）
/// </summary>
public static class Program
{
    /// <summary>逻辑帧间隔（20 帧/秒），需与客户端 FrameSyncMgr 一致</summary>
    private const int LogicFrameMs = 50;

    public static void Main()
    {
        // ---- 配置（Jwt:Secret 须与 LoginServer 一致，否则 token 校验必然失败）----
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        var jwtSecret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("缺少配置 Jwt:Secret");

        // ---- 模块装配（构造注入，依赖单向无循环）----
        var dispatcher = new MsgDispatcher();
        var net = new NetServer(dispatcher);
        var sessions = new SessionMgr(net);
        var store = new ProfileStore(config.GetConnectionString("Default") ?? "../LoginServer/login.db");
        var rooms = new RoomMgr(store);
        var matchMaker = new MatchMaker(net, sessions, rooms);

        // 连接清理钩子：会话清理（断线/顶号）时同步清匹配队列与房间归属
        sessions.PeerCleanedUp += matchMaker.Dequeue;
        sessions.PeerCleanedUp += peer => rooms.RemoveFromRoom(peer);

        // 各域消息处理（构造时向分发器注册消息，注册完成即可收消息）
        var jwt = new JwtValidator(jwtSecret);
        new AuthHandler(dispatcher, net, sessions, jwt);
        new RoomHandler(dispatcher, net, sessions, rooms);
        new MatchHandler(dispatcher, net, sessions, rooms, matchMaker);
        new BattleHandler(dispatcher, net, sessions, rooms);

        // ---- 启动 ----
        const int port = 7777;
        if (!net.Start(port))
        {
            Console.WriteLine($"服务器启动失败（端口可能被占用）: {port}");
            return;
        }
        Console.WriteLine($"服务器已启动，监听 UDP 端口 {port}，逻辑帧 {LogicFrameMs}ms/帧（{1000 / LogicFrameMs} 帧/秒）");
        Console.WriteLine("等待客户端连接...（按 Ctrl+C 退出）");

        // ---- 主循环：轮询网络事件 + 固定步长逻辑帧（累加器模式，业内通用做法）----
        // 相比 Restart() 计时：处理耗时不被丢弃（时间进累加器），帧率更稳；
        // 相比 Sleep(1) 忙等：睡到距下一帧边界最多 5ms 的小片，既省 CPU 又保证网络事件及时处理
        var clock = Stopwatch.StartNew();
        long lastTicks = clock.ElapsedTicks;
        double accumulatorMs = 0;
        const double MaxCatchUpMs = LogicFrameMs * 3; // 钳制累积量：最多补 3 帧，防卡顿后死亡螺旋
        while (true)
        {
            net.PollEvents();

            // 累积流逝时间（Stopwatch 高精度单调时钟，不受系统改时间影响）
            var nowTicks = clock.ElapsedTicks;
            accumulatorMs += (nowTicks - lastTicks) * 1000.0 / Stopwatch.Frequency;
            lastTicks = nowTicks;

            if (accumulatorMs > MaxCatchUpMs)
                accumulatorMs = MaxCatchUpMs;

            // 固定步长消费：超时补帧（钳制下最多 3 帧）
            while (accumulatorMs >= LogicFrameMs)
            {
                accumulatorMs -= LogicFrameMs;
                rooms.BroadcastAllFrames();
            }

            // 分片睡眠：距下一帧边界还有多少睡多少（单片上限 5ms），期间保持轮询网络
            var remainMs = LogicFrameMs - accumulatorMs;
            var sleepMs = (int)Math.Min(remainMs, 5);
            Thread.Sleep(Math.Max(0, sleepMs));
        }
    }
}
