/*--------------------------------------------------------------
 * File: PlayerDataMgr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/13 15:00:00 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

/// <summary>
/// 养成数据服务：获取/保存玩家养成数据（需登录，token 来自 AccountMgr）。
/// 对外全部为回调 API（内部由 HttpMsgHandler 用 UniTask 异步执行）
/// </summary>
public class PlayerDataMgr : ManagerBase
{
    // 养成数据字段
    public int Gold { get; private set; }
    public int StageProgress { get; private set; }
    public Dictionary<string, object> TowerLevels { get; private set; } = new();

    /// <summary>是否已从服务器加载过养成数据</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// 获取养成数据（GET /data，proto），成功后更新本地缓存，完成后回调。
    /// 注：客户端只有读权限，写入由服务器主导（结算入账/未来业务接口），阶段 7-3 已移除 Save
    /// </summary>
    public void Load(Action<NetMsg.GetDataResp> onDone)
    {
        GameMgr.HttpMsg.Get<NetMsg.GetDataResp>(resp =>
        {
            if (resp.ErrorCode == NetMsg.ErrorCode.ErrorNone)
            {
                Gold = resp.Gold;
                StageProgress = resp.StageProgress;
                TowerLevels = ParseUtil.DeJson(resp.TowerLevels) as Dictionary<string, object> ?? new Dictionary<string, object>();
                IsLoaded = true;
                Log.Info("养成数据加载成功: 金币", Gold, "关卡", StageProgress);
            }
            else
            {
                Log.Error("养成数据加载失败, 错误码:", resp.ErrorCode);
            }
            onDone?.Invoke(resp);
        });
    }
}
