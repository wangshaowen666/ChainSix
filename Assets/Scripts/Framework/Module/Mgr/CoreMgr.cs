/*--------------------------------------------------------------
 * File: CoreMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/07/31 15:58:44
 *--------------------------------------------------------------
 */

using System;

public static class CoreMgr
{
    public static readonly ProcedureMgr Procedure = new();
    public static readonly UpdateMgr Update = new();
    public static readonly TimerMgr Timer = new();
    public static readonly ClassPoolMgr ClassPool = new();
    public static readonly ResMgr Res = new();
    public static readonly ObjectPoolMgr ObjectPool = new();
    public static readonly HttpMgr Http = new();
    //public static readonly NetMgr Net = new();
    
    private static readonly ManagerBase[] s_managers;

    static CoreMgr()
    {
        s_managers = new ManagerBase[]
        {
            Procedure,
            Update,
            Timer,
            ClassPool,
            Res,
            ObjectPool,
            Http,
            //Net,
        };
    }

    public static void Init()
    {
        foreach (var mgr in s_managers)
            mgr.OnInit();
    }

    public static void OnSceneExit()
    {
        for (int i = s_managers.Length - 1; i >= 0; i--)
            s_managers[i].OnSceneExit();
    }
    public static void OnGameRestart()
    {
        for (int i = s_managers.Length - 1; i >= 0; i--)
            s_managers[i].OnGameRestart();
    }
}
