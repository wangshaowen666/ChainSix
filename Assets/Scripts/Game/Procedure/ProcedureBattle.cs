/*--------------------------------------------------------------
 * File: ProcedureBattle.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/06 15:58:41
 *--------------------------------------------------------------
 */

using System;
using cfg;
using UnityEngine;

public class ProcedureBattle : ProcedureBase
{
    public override void OnEnter()
    {
        base.OnEnter();

        GameCamera.SetMainCamera(GameCamera.BattleCameraName);
        GameMgr.UI.PanelOn(DPnlId.BattlePanel);

        // 战斗内退战斗回主界面（2-11 死亡结算按钮，事件解耦对齐 ProcedureMain.ProcedureExitMain 模式）
        GameMgr.Event.Register(GameEvent.ProcedureExitBattle, RunProcedure);

        // 进入指定玩法战斗
        GameMgr.Battle.EnterBattle(BattleMode.VampireSurvivor);
    }

    public override void OnExit()
    {
        base.OnExit();
        GameMgr.Event.UnRegister(GameEvent.ProcedureExitBattle, RunProcedure);
        GameMgr.UI.PanelOff(DPnlId.BattlePanel); // 对称 OnEnter 的 PanelOn：离开战斗流程即关战斗界面（含 HUD/摇杆）
    }

    private void RunProcedure()
    {
        _fsm.SetData(ProcedureKey.SceneName, GameConfig.MainScene);
        ChangeTo<ProcedureChangeScene>();
    }
}
