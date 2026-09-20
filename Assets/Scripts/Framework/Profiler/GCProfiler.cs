/*--------------------------------------------------------------
 * File: GCProfiler.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/05/19 15:57:52 
 *--------------------------------------------------------------
 */

using System;
using Unity.Profiling;
using UnityEngine;

public static class GCProfiler
{
    private static readonly ProfilerCategory MemoryCategory = ProfilerCategory.Memory;
    private const string GcAllocatedBytesCounter = "GC Allocated In Frame";
    
    public static void RecordGC(Action action)
    {
        using var bytesRecorder = ProfilerRecorder.StartNew(MemoryCategory, GcAllocatedBytesCounter);
        
        action?.Invoke();
        
        if (bytesRecorder.CurrentValue > 0)
        {
            Log.Info($"[Profiler GC统计] : {FileUtil.GetFileLength(bytesRecorder.CurrentValue)}");
        }
        else
        {
            Log.Info($"[Profiler GC统计]零GC分配 ✅");
        }
    }
}