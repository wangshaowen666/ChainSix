/*--------------------------------------------------------------
 * File: MainPanel.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/31 18:43:16 
 *--------------------------------------------------------------
 */

using System;
using cfg;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class MainPanel : UIPanelBase
{
    public Button btnStart;
    public Button btnSetting;

    public override void OnInit(DUIPanel cfg)
    {
        base.OnInit(cfg);
        Log.Info("进入主界面");
        btnSetting.onClick.AddListener(OnClickSetting);
        btnStart.onClick.AddListener(OnClickStart);
    }

    public override void OnOpen(object userData = null)
    {
        base.OnOpen(userData);
    }

    private void OnClickSetting()
    {
        //GameMgr.UI.PanelOn(DPnlId.SettingPanel);
    }

    private void OnClickStart()
    {
        GameMgr.Event.Send(GameEvent.ProcedureExitMain);
        GameMgr.UI.PanelOff(this);
    }
}