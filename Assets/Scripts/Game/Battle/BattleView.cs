/*--------------------------------------------------------------
 * File: BattleView.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/26 14:40:45 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public enum BattleMode : byte
{
    TowerDefense,     // 帧同步塔防
    VampireSurvivor,  // 吸血鬼幸存者
}

public abstract class BattleView 
{
    public virtual void Init()
    {
        
    }

    /// <summary>
    /// 战斗结束/退出时清理（取消订阅、销毁表现层）
    /// </summary>
    public virtual void Dispose()
    {

    }
}
