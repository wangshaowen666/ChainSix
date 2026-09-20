/*--------------------------------------------------------------
 * File: TimeProfiler.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2024/03/20 14:19:09 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// 耗时不能说明全部，同一个代码块重复测试三次，打印时间会出现其中一次和另外两次差距挺大的情况，可能是内存扩容集中发生
/// </summary>
public static class TimeProfiler 
{
    private static readonly Dictionary<string, StopwatchItem> _timeMap = new Dictionary<string, StopwatchItem>();
    
    /// <summary>
    /// 微秒级精度，更准确
    /// </summary>
    /// <param name="cb"></param>
    public static void LogTime(Action cb)
    {
        var sw = new Stopwatch();
        sw.Start();
        cb?.Invoke();
        sw.Stop();
        Log.Info(Log.LogColor.Cyan, "共耗时", sw.Elapsed.TotalMilliseconds, "毫秒");
    }

    /// <summary>
    /// 真实开销会略小于这里记录的，回调执行的方式占用了一点点开销，不受帧率影响
    /// </summary>
    /// <param name="key"></param>
    /// <param name="cb"></param>
    public static void RecordTime(string key, Action cb)
    {
        if (!_timeMap.TryGetValue(key, out var r))
        {
            r = new StopwatchItem(key);
            _timeMap[key] = r;
        }
        
        r.Start();
        cb.Invoke();
        r.Stop();
    }

    public static void RecordTimeStart(string key)
    {
        if (!_timeMap.TryGetValue(key, out var r))
        {
            r = new StopwatchItem(key);
            _timeMap[key] = r;
        }
        
        r.Start();
    }

    public static void RecordTimeStop(string key)
    {
        // 不做key是否存在检查，尽量避免非测试逻辑开销
        var item = _timeMap[key];
        item.Stop();
    }
    
    public static void LogDurationTime(bool isClear = true, bool isSaveLocal = false)
    {
        string ret = "";
        foreach (var kv in _timeMap)
        {
            var str = kv.Value.LogTime();
            if (isSaveLocal)
            {
                ret += str + "\n";
            }
        }
        
        if (isClear)
        {
            _timeMap.Clear();
        }

        if (isSaveLocal)
        {
            FileUtil.WriteFile(ret, "耗时统计.txt");
        }
    }
}

public class StopwatchItem
{
    private readonly Stopwatch _sw;
    private readonly string _key;
    private int _count;

    public StopwatchItem(string key)
    {
        _key = key;
        _sw = new Stopwatch();
    }

    public void Start()
    {
        _count++;
        _sw.Start();
    }

    public void Stop()
    {
        _sw.Stop();
    }

    public string LogTime(bool reset = true)
    {
        var str = $"记录耗时: {_key}  {_sw.Elapsed.TotalMilliseconds}毫秒  执行次数: {_count}";
        Log.Info(Log.LogColor.Cyan, str);
        
        if (reset) _sw.Reset();
        
        return str;
    }
}