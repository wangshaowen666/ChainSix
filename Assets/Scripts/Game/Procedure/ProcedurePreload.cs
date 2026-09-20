/*--------------------------------------------------------------
 * File: ProcedurePreload.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/13 13:36:48 
 *--------------------------------------------------------------
 */

using Cysharp.Threading.Tasks;
using UnityEngine;

public static class ProcedureKey
{
    public const string SceneName = "sceneNm";
}

public class ProcedurePreload : ProcedureBase
{
    private LaunchPanel _loginPanel;
    
    public override void OnEnter()
    {
        base.OnEnter();
        AsyncRun().Forget();
    }
    
    private async UniTaskVoid AsyncRun()
    {
        _loginPanel = _fsm.GetData<LaunchPanel>(LaunchConfig.LaunchPanel);
        _loginPanel.SetTip("编译着色器中...", 0.9f);

        // 加载配置表（表bytes必须先异步预载，WebGL/小游戏无同步加载能力）
        await GameMgr.DataTable.PreloadTableBytesAsync();
        GameMgr.DataTable.LoadTable();

        // 预载 GPU 动画 shader（真机显式加载后 Shader.Find 才能命中，加载后常驻）
        await FrameAnimGpuUtil.PreloadShadersAsync();
        
        await UniTask.Yield();
        
        _fsm.SetData(ProcedureKey.SceneName, GameConfig.MainScene);
        //_fsm.SetData(ProcedureKey.SceneName, GameConfig.TestScene);
        
        ChangeTo<ProcedureChangeScene>();
    }
}