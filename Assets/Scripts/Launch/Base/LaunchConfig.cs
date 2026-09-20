/*--------------------------------------------------------------
 * File: AOTConfig.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/04 18:18:27 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public static class LaunchConfig
{
    // 热更程序集列表
    public static readonly string[] HotfixDll = new string[] {"Game.dll"};
    // 热更程序集入口程序集名
    public const string HotEntryDll = "Game";
    // 热更入口类名
    public const string HotEntryClass = "GameLaunch";
    // 热更入口方法名
    public const string HotEntryMethod = "StartGame";
    
    // 屏幕标准尺寸
    public static Vector2Int ScreenSize = new Vector2Int(1080, 1920);
    // 游戏ui根节点名
    public const string UIRoot = "UIRoot";
    // ui相机名
    public const string UICamera = "UICamera";
    // 登陆界面路径
    public const string LoginPath = "LoginPanel/LoginPanel";
    // 启动游戏获取服务器设置网址
    public const string ServerSettingURL = "http://localhost:6666/?CH=Api&Opt=GetServerSetting";
    // 跳过版本检查
    public const bool SkipVersionCheck = true;
    // 流程登陆界面key
    public const string LoginPanel = "loginPanel";
}