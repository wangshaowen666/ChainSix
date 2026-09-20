/*--------------------------------------------------------------
 * File: Log.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2024/03/20 15:46:16
 * Copyright: Copyright © 2024 wangshaowen. All rights reserved.
 *--------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 各方法上的 [HideInCallstack] 用于把 Log 封装帧从 Console 调用堆栈中隐藏，
/// 需要在 Console 窗口右上角菜单中勾选 "Strip logging callstack" 后生效。
/// 双击跳转到业务调用处的逻辑见 Editor 目录下的 LogJumpFixer.cs
/// </summary>
public static class Log
{
    public enum LogColor
    {
        White,
        Red,
        Yellow,
        Green,
        Cyan,
        Orange,
        Gray,
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void Info(params object[] msgs)
    {
        Debug.Log(DealMsg(false, msgs));
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void Info(LogColor color, params object[] msgs)
    {
        var msg = DealMsg(false, msgs);
        Debug.Log($"<color={color}>{msg}</color>");
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void InfoLine(params object[] msgs)
    {
        Debug.Log(DealMsg(true, msgs));
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void InfoLine(LogColor color, params object[] msgs)
    {
        var msg = DealMsg(true, msgs);
        Debug.Log($"<color={color}>{msg}</color>");
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void Warning(params object[] msgs)
    {
        Debug.LogWarning(DealMsg(false, msgs));
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void Warning(LogColor color, params object[] msgs)
    {
        var msg = DealMsg(false, msgs);
        Debug.LogWarning($"<color={color}>{msg}</color>");
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void WarningLine(params object[] msgs)
    {
        Debug.LogWarning(DealMsg(true, msgs));
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void WarningLine(LogColor color, params object[] msgs)
    {
        var msg = DealMsg(true, msgs);
        Debug.LogWarning($"<color={color}>{msg}</color>");
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void Error(params object[] msgs)
    {
        Debug.LogError(DealMsg(false, msgs));
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void Error(LogColor color, params object[] msgs)
    {
        var msg = DealMsg(false, msgs);
        Debug.LogError($"<color={color}>{msg}</color>");
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void ErrorLine(params object[] msgs)
    {
        Debug.LogError(DealMsg(true, msgs));
    }

    [Conditional("LOG_ON")]
    [HideInCallstack]
    public static void ErrorLine(LogColor color, params object[] msgs)
    {
        var msg = DealMsg(true, msgs);
        Debug.LogError($"<color={color}>{msg}</color>");
    }

    private static string DealMsg(bool lineBreak, params object[] msgs)
    {
        StringBuilder sb = new StringBuilder();
        var str = lineBreak ? "\n" : "  ";
        for (int i = 0; i < msgs.Length; i++)
        {
            sb.Append(msgs[i] + str);
        }

        return sb.ToString();
    }
}
