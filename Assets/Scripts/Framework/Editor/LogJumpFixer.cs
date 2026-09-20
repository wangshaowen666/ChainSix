/*--------------------------------------------------------------
 * File: LogJumpFixer.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/19
 * Copyright: Copyright © 2026 wangshaowen. All rights reserved.
 *--------------------------------------------------------------
 */

using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using Object = UnityEngine.Object;

/// <summary>
/// 双击 Console 日志时，自动跳过 Log 封装类，直接定位到业务代码的调用行。
/// 原理：双击日志时，Unity 会打开堆栈中第一个带源码信息的帧，
/// 也就是 Log.cs 里的 Debug.Log 调用行。这里通过 OnOpenAsset 拦截
/// 对 Log.cs 的打开动作，反射 ConsoleWindow 的 m_ActiveText 拿到当前
/// 选中日志的完整堆栈文本，解析出第一条不属于封装类的帧，
/// 跳转打开对应的脚本和行号（走 Unity 标准打开流程，兼容 Rider/VSCode/VS）。
/// </summary>
public static class LogJumpFixer
{
    /// <summary>
    /// 日志封装类所在的脚本路径，双击跳转时这些帧会被跳过
    /// </summary>
    private static readonly string[] WrapperScriptPaths =
    {
        "Assets/Scripts/Framework/Base/Log.cs",
    };

    /// <summary>
    /// 匹配堆栈帧行尾的 "(at 文件路径:行号)"
    /// </summary>
    private static readonly Regex FrameRegex = new Regex(@"\(at (.+):(\d+)\)\s*$");

    [OnOpenAsset(0)]
    private static bool OnOpenAsset(int instanceID, int line)
    {
        // 只拦截对日志封装类（Log.cs）的打开请求，其它资源走默认逻辑
        if (!IsWrapperScript(AssetDatabase.GetAssetPath(instanceID)))
        {
            return false;
        }

        string stackTrace = GetConsoleActiveText();
        if (string.IsNullOrEmpty(stackTrace))
        {
            return false;
        }

        // 从上往下找第一条不属于封装类的源码帧，即业务调用处
        foreach (string frameLine in stackTrace.Split('\n'))
        {
            Match match = FrameRegex.Match(frameLine);
            if (!match.Success)
            {
                continue;
            }

            string filePath = match.Groups[1].Value.Trim().Replace('\\', '/');
            if (IsWrapperScript(filePath))
            {
                continue;
            }

            Object asset = AssetDatabase.LoadAssetAtPath<Object>(filePath);
            if (asset == null)
            {
                continue; // 帧对应的脚本不在工程内，继续找下一帧
            }

            int.TryParse(match.Groups[2].Value, out int lineNum);
            AssetDatabase.OpenAsset(asset, lineNum);
            return true;
        }

        // 没找到业务帧（例如调用方在无符号的程序集里），交回默认逻辑打开 Log.cs
        return false;
    }

    private static bool IsWrapperScript(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string path = assetPath.Replace('\\', '/');
        foreach (string wrapperPath in WrapperScriptPaths)
        {
            if (string.Equals(path, wrapperPath, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 反射获取 Console 窗口当前选中日志的完整文本（含调用堆栈）
    /// </summary>
    private static string GetConsoleActiveText()
    {
        var consoleWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
        if (consoleWindowType == null)
        {
            return null;
        }

        var consoleWindowField = consoleWindowType.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic);
        if (consoleWindowField == null)
        {
            return null;
        }

        // 只有 Console 窗口是焦点窗口时，m_ActiveText 才是有效的选中日志文本
        // （从 Project 窗口双击打开 Log.cs 本体时不会走这里的拦截逻辑）
        var consoleWindow = consoleWindowField.GetValue(null) as EditorWindow;
        if (consoleWindow == null || EditorWindow.focusedWindow != consoleWindow)
        {
            return null;
        }

        var activeTextField = consoleWindowType.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic);
        if (activeTextField == null)
        {
            return null;
        }

        return activeTextField.GetValue(consoleWindow) as string;
    }
}
