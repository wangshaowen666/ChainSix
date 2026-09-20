/*--------------------------------------------------------------
 * File: ProcedureChangeScene.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/22 18:26:05
 *--------------------------------------------------------------
 */

using cfg;
using Cysharp.Threading.Tasks;

public class ProcedureChangeScene : ProcedureBase
{
    private string _curScene;
    public override void OnEnter()
    {
        base.OnEnter();

        var nm = _fsm.GetData<string>(ProcedureKey.SceneName);
        if (string.IsNullOrEmpty(nm))
        {
            Log.Error("不存在的场景：", nm);
            return;
        }

        // _curScene为空意味着首次从登陆进入主界面
        if (!string.IsNullOrEmpty(_curScene))
        {
            GameMgr.UI.PanelOn(DPnlId.LoadingPanel);
            GameMgr.OnSceneExit(_curScene);
            CoreMgr.OnSceneExit();
        }

        LoadSceneAsync(nm).Forget();
    }

    private async UniTaskVoid LoadSceneAsync(string nm)
    {
        await CoreMgr.Res.LoadSceneAsync(nm);
        
        GameMgr.UI.PanelOff(DPnlId.LoadingPanel);
        switch (nm)
        {
            case GameConfig.MainScene:
                ChangeTo<ProcedureMain>();
                break;

            case GameConfig.BattleScene:
            case GameConfig.NetworkScene:
                ChangeTo<ProcedureBattle>();
                break;
            
            case GameConfig.TestScene:
                ChangeTo<ProcedureTest>();
                break;
        }

        if (string.IsNullOrEmpty(_curScene))
        {
            var loadingPanel = _fsm.GetData<LaunchPanel>(LaunchConfig.LaunchPanel);
            _fsm.RemoveData(LaunchConfig.LaunchPanel);
            UnityEngine.Object.Destroy(loadingPanel.gameObject);
        }
        
        _curScene = nm;
    }
}
