/*--------------------------------------------------------------
 * File: Procedure.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/12 15:42:27 
 *--------------------------------------------------------------
 */


/// <summary>
/// 基于状态机的游戏流程
/// 各流程数据共享都靠fsm
/// </summary>
public class ProcedureMgr : ManagerBase
{
    private Fsm _fsm;

    public override void OnInit()
    {
        base.OnInit();
        _fsm = Fsm.Create();
    }

    public void AddProcedure(ProcedureBase procedure)
    {
        _fsm.AddState(procedure);
    }

    public void ChangeProcedure<T>() where T : ProcedureBase
    {
        _fsm.ChangeState<T>();
    }

    // 原本是通过反射自动收集所有流程，用华佗热更拆分程序集后，反射拿不到热更程序集中的流程
    // 通过反射获取业务逻辑的所有流程
    // private static List<ProcedureBase> GetAllProcedures()
    // {
    //     List<ProcedureBase> procedures = new List<ProcedureBase>();
    //     
    //     // 获取所有程序集
    //     Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
    //     foreach (var assembly in assemblies)
    //     {
    //         Type[] types;
    //         try
    //         {
    //             types = assembly.GetTypes();
    //         }
    //         catch (ReflectionTypeLoadException e)
    //         {
    //             // 如果某些类型无法加载，使用已成功加载的类型继续
    //             types = e.Types;
    //         }
    //
    //         foreach (var type in types)
    //         {
    //             if (type == null)
    //                 continue;
    //
    //             if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(ProcedureBase)))
    //             {
    //                 try
    //                 {
    //                     ProcedureBase procedure = Activator.CreateInstance(type) as ProcedureBase;
    //                     procedures.Add(procedure);
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     Log.Error($"创建流程实例失败：{type.FullName}, 错误：{e.Message}");
    //                 }
    //             }
    //         }
    //     }
    //     
    //     return procedures;
    // }
}