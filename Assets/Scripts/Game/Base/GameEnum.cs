/*--------------------------------------------------------------
 * File: GameEnum.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/07 13:24:27
 *--------------------------------------------------------------
 */


/// <summary>
/// 游戏事件名
/// </summary>
public enum GameEvent
{
    UIPanelOn = 1,
    
    // 流程相关
    ProcedureExitMain = 100,
    ProcedureExitBattle = 101, // 战斗内请求退战斗回主界面（2-11 死亡结算按钮 -> ProcedureBattle 切 ProcedureChangeScene）
    
    ResUpdateFinish = 200,
    
    // UI相关
    
    // 战斗相关
    EnterBattle = 2001,

    // 吸血鬼战斗输入（解耦：GameJoystick 由战斗 UI BattlePanel 登记到 GameMgr.Battle，
    // 战斗侧 VampireView 经"查缓存 + 就绪事件"获取，双方互不持有引用）
    VsJoystickReady = 2101,   // 战斗 UI 登记摇杆后广播，携带 GameJoystick（面板异步加载完成才发）

    // 吸血鬼升级选牌（解耦：LevelUpPanel 与 VampireView 经事件交互，互不持有引用）
    VsLevelUpChosen = 2102,        // 玩家在选牌面板确认，携带 int 选项下标（面板 -> 战斗）
    VsLevelUpChoicesChanged = 2103, // 选项组刷新（连升多组时换题），携带 List<VsLevelUpCard>（战斗 -> 面板；首开走 userData）
    VsStartWeaponChosen = 2104,     // 开局武器选择确认，携带 int 武器表 id（WeaponChoosePanel -> 战斗）
}

