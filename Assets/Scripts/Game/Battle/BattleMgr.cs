/*--------------------------------------------------------------
 * File: BattleMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/26 14:53:41 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public class BattleMgr : ManagerBase
{
    public BattleView CurrentBattleView => _battleView;
    private BattleView _battleView;

    public GameJoystick Joystick { get; private set; }

    public void RegisterJoystick(GameJoystick joystick)
    {
        Joystick = joystick;
        GameMgr.Event.Send(GameEvent.VsJoystickReady, joystick);
    }

    public void ClearJoystick()
    {
        Joystick = null;
    }

    public void EnterBattle(BattleMode mode = BattleMode.TowerDefense)
    {
        switch (mode)
        {
            case BattleMode.TowerDefense:
                if (GameMgr.Room.StartFrame == 0)
                {
                    Log.Warning("未收到开战推送（需房间内全员就绪），拒绝进入战斗");
                    return;
                }
                break;

            case BattleMode.VampireSurvivor:
                // 单机玩法：无房间/联机依赖，直接进入（LocalDriver 本地固定步长驱动，阶段 1-3）
                break;

            case BattleMode.SixLine:
                // 六连珠阶段 1 本地灰盒：无房间/联机依赖直接进入（LocalDriver 驱动，1-7 接入）；阶段 3 好友对战改 FrameSyncMgr 驱动
                break;

            default:
                Log.Error("未知战斗模式:", mode);
                return;
        }

        _battleView?.Dispose();
        switch (mode)
        {
            // case BattleMode.TowerDefense:
            //     _battleView = new TdView();
            //     break;
            // case BattleMode.VampireSurvivor:
            //     _battleView = new VampireView();
            //     break;
            case BattleMode.SixLine:
                _battleView = new SixLineView();
                break;
        }
        _battleView?.Init();
    }

    public override void OnSceneExit(string sceneNm)
    {
        if (sceneNm == GameConfig.BattleScene)
        {
            _battleView?.Dispose();
            _battleView = null;
        }
    }

    public override void OnGameRestart()
    {
        base.OnGameRestart();
        _battleView?.Dispose();
        _battleView = null;
    }
}
