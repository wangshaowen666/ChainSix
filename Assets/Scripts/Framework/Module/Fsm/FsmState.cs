/*--------------------------------------------------------------
 * File: FsmState.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/07 17:04:01 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

/// <summary>
/// 有限状态机状态基类
/// </summary>
public abstract class FsmState
{
    protected Fsm _fsm;
    
    /// <summary>
    /// 状态初始化时调用
    /// </summary>
    /// <param name="fsm">状态机</param>
    public virtual void OnInit(Fsm fsm)
    {
        _fsm = fsm;
    }

    public virtual void OnEnter()
    {
    }

    public virtual void OnExit()
    {
    }
    
    public virtual void OnDestroy()
    {
    }
}