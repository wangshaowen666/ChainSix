/*--------------------------------------------------------------
 * File: GameConfig.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/07 13:28:00 
 *--------------------------------------------------------------
 */

using System;
using UnityEngine;

public static class GameConfig 
{
    // 游戏内场景
    public const string MainScene = "Main";
    public const string BattleScene = "Battle";
    public const string NetworkScene = "Network";
    public const string TestScene = "Test";
    
    // 登录服务器地址（本机调试用 localhost，真机/双端测试用局域网 IP：本机 WiFi en1 的 192.168.14.x 网段）
    public const string LoginServerUrl = "http://192.168.14.200:5049";
}