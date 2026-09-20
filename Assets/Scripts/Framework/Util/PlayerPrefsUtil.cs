/*--------------------------------------------------------------
 * File: PlayerPrefsUtil.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/11 15:15:01 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 本地存档，string默认值为“”，int和float默认值为0
/// </summary>
public static class PlayerPrefsUtil 
{
    private static readonly Dictionary<string, string> StrCache = new Dictionary<string, string>();
    private static readonly Dictionary<string, int> IntCache = new Dictionary<string, int>();
    private static readonly Dictionary<string, float> FloatCache = new Dictionary<string, float>();
    
    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        StrCache[key] = value;
    }

    public static string GetString(string key)
    {
        if (!StrCache.TryGetValue(key, out string value))
        {
            value = PlayerPrefs.GetString(key, "");
            StrCache[key] = value;
        }
        return value;
    }
    
    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        IntCache[key] = value;
    }

    public static int GetInt(string key)
    {
        if (!IntCache.TryGetValue(key, out int value))
        {
            value = PlayerPrefs.GetInt(key, 0);
            IntCache[key] = value;
        }
        return value;
    }
    
    public static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        FloatCache[key] = value;
    }

    public static float GetFloat(string key)
    {
        if (!FloatCache.TryGetValue(key, out float value))
        {
            value = PlayerPrefs.GetFloat(key, 0f);
            FloatCache[key] = value;
        }
        return value;
    }

    public static void DeleteKey(string key)
    {
        if (StrCache.ContainsKey(key))
            StrCache[key] = "";
        
        else if (IntCache.ContainsKey(key))
            IntCache[key] = 0;

        else if (FloatCache.ContainsKey(key))
            FloatCache[key] = 0f;
        
        PlayerPrefs.DeleteKey(key);
    }

    public static void DeleteAll()
    {
        StrCache.Clear();
        IntCache.Clear();
        FloatCache.Clear();
        
        PlayerPrefs.DeleteAll();
    }
}