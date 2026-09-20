/*--------------------------------------------------------------
 * File: GameMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/05/20 10:17:32
 *--------------------------------------------------------------
 */

/// <summary>
/// 游戏管理类容器，有状态需清除的加入此容器中，不需要的声明为静态
/// </summary>
public static class GameMgr
{
    public static readonly DataTableMgr DataTable = new();
    public static readonly AccountMgr Account = new();
    public static readonly PlayerDataMgr PlayerData = new();
    public static readonly NetMsgHandler NetMsg = new();
    public static readonly HttpMsgHandler HttpMsg = new();
    public static readonly FrameSyncMgr FrameSync = new();
    public static readonly RoomMgr Room = new();
    public static readonly UIMgr UI = new();
    public static readonly EntityPool EntityPool = new();
    public static readonly BattleMgr Battle = new();
    public static readonly FrameAnimMgr FrameAnim = new();
    public static readonly FrameAnimGpuMgr FrameAnimGpu = new();
    public static readonly EventMgr<GameEvent> Event = new();

    private static readonly ManagerBase[] s_managers;

    static GameMgr()
    {
        s_managers = new ManagerBase[]
        {
            DataTable,
            Account,
            PlayerData,
            NetMsg,
            HttpMsg,
            FrameSync,
            Room,
            UI,
            EntityPool, // 须在 Battle 之前注册：逆序清理时战斗先 Dispose 归还实体，EntityPool 再清池
            Battle,
            FrameAnim,
            FrameAnimGpu,
            Event,
        };
    }

    public static void Init()
    {
        foreach (var mgr in s_managers)
            mgr.OnInit();
    }

    public static void OnSceneExit(string sceneNm)
    {
        for (int i = s_managers.Length - 1; i >= 0; i--)
            s_managers[i].OnSceneExit(sceneNm);
    }
    public static void OnGameRestart()
    {
        for (int i = s_managers.Length - 1; i >= 0; i--)
            s_managers[i].OnGameRestart();
    }
}
