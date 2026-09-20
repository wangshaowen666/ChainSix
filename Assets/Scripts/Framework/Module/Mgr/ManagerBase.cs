/*--------------------------------------------------------------
 * File: ManagerBase.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/05/20 10:43:14 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public abstract class ManagerBase 
{
    /// <summary>
    /// 初始化
    /// </summary>
    public virtual void OnInit()
    {

    }

    /// <summary>
    /// 场景退出，不关心具体场景，框架层使用
    /// </summary>
    public virtual void OnSceneExit()
    {
    }
    
    /// <summary>
    /// 场景退出，关心具体场景，业务层使用
    /// </summary>
    public virtual void OnSceneExit(string sceneNm)
    {
        OnSceneExit();
    }

    /// <summary>
    /// 游戏重启
    /// </summary>
    public virtual void OnGameRestart()
    {

    }
}