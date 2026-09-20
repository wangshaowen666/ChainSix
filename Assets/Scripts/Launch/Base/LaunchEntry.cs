/*--------------------------------------------------------------
 * File: GameEntry.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/12 16:41:23 
 *--------------------------------------------------------------
 */

using UnityEngine;

public class LaunchEntry : MonoBehaviour
{
    void Awake()
    {
        CoreMgr.Init();
        InitProcedure();
        DontDestroyOnLoad(gameObject);
    }
    
    void Update()
    {
        CoreMgr.Update.MyUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    }

    private void InitProcedure()
    {
        CoreMgr.Procedure.AddProcedure(new ProcedureLaunch());
        CoreMgr.Procedure.AddProcedure(new ProcedureVersionCheck());
        CoreMgr.Procedure.AddProcedure(new ProcedureResCheckAA());
        CoreMgr.Procedure.AddProcedure(new ProcedureLoadDll());
        
        CoreMgr.Procedure.ChangeProcedure<ProcedureLaunch>();
    }
}