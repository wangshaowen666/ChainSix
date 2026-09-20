/*--------------------------------------------------------------
 * File: ProcedureBase.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/12 15:35:03 
 *--------------------------------------------------------------
 */


public class ProcedureBase : FsmState
{
    protected void ChangeTo<T>() where T : ProcedureBase
    {
        CoreMgr.Procedure.ChangeProcedure<T>();
    }
}