/*--------------------------------------------------------------
 * File: ClassPool.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2024/03/14 19:14:37 
 * Copyright: Copyright © 2024 wangshaowen. All rights reserved.
 *--------------------------------------------------------------
 */

using System;

public class ClassPool
{
    // 用数组替代栈，减少方法调用开销，比链表性能也好
    // 在现代CPU架构下，缓存命中率对性能影响巨大；无额外指针开销，内存使用更紧凑；支持批量预分配，减少运行时开销
    // 测试下来数组比栈耗时减少1倍多，主要是因为[MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IResetable[] _poolArray = new IResetable[Capacity];
    private int _index = 0;
    
    // 默认初始容量
    private const int Capacity = 8;

    /// <summary>
    /// 手动预分配对象
    /// </summary>
    /// <param name="count">分配数量</param>
    public void PreAllocate<T>(int count) where T : class, IResetable, new()
    {
        if (count <= 0)
        {
            Log.Error(Log.LogColor.Red, "预分配数量必须大于0");
            return;
        }

        int targetCount = _index + count;
        if (targetCount > _poolArray.Length)
        {
            int newCapacity = Math.Max(_poolArray.Length * 2, targetCount);
            Array.Resize(ref _poolArray, newCapacity);
        }
        
        for (int i = 0; i < count; i++)
        {
            var obj = new T();
            _poolArray[_index++] = obj;
        }

#if STATS_ON && UNITY_EDITOR
        UpdateStats(1, count);
#endif
    }

    public T Get<T>() where T : class, IResetable, new()
    {
        T ret;

        if (_index > 0)
        {
            ret = _poolArray[--_index] as T;
            _poolArray[_index] = null;
#if STATS_ON && UNITY_EDITOR
            UpdateStats(4);
#endif
        }
        else
        {
            ret = new T();
#if STATS_ON && UNITY_EDITOR
            UpdateStats(3);
#endif
        }

        return ret;
    }

    public void Recycle<T>(T item) where T : IResetable
    {
#if UNITY_EDITOR 
        for (int i = 0; i < _index; i++)
        {
            if (ReferenceEquals(_poolArray[i], item))
            {
                Log.Error(Log.LogColor.Red, "对象池中已经包含该对象，请检查回收逻辑");
                return;
            }
        }
#endif
        if (_index >= _poolArray.Length)
        {
            var newSize = _poolArray.Length * 2;
            Array.Resize(ref _poolArray, newSize);
        }
            
        item.Reset();
        _poolArray[_index++] = item;
        
#if STATS_ON && UNITY_EDITOR
        UpdateStats(2);
#endif
    }

    public void Release()
    {
#if STATS_ON && UNITY_EDITOR
        UpdateStats(5, _index + 1);
#endif
        // 释放后数组容量缩回默认
        if (_index > Capacity)
            _poolArray = new IResetable[Capacity];
        
        Array.Clear(_poolArray, 0, _index);
        _index = 0;
    }
       
#if STATS_ON && UNITY_EDITOR
    // 统计信息
    private ClassPoolStats _stats = new ClassPoolStats{capacity = Capacity};
    
    /// <summary>
    /// 更新统计信息
    /// </summary>
    /// <param name="flag">操作标识。1预热 2存 3取新 4取旧 5释放</param>
    /// <param name="count">数量</param>
    private void UpdateStats(int flag, int count = 1)
    {
        switch (flag)
        {
            case 1:
                _stats.preAllocate += count;
                _stats.capacity = _poolArray.Length;
                _stats.totalNum += count;
                _stats.createNum += count;
                
                if (_stats.peakNUm < _stats.totalNum)
                    _stats.peakNUm = _stats.totalNum;
                break;
            
            case 3:
            case 4:
                _stats.totalGets++;
                if (flag == 3)
                {
                    _stats.createNum++;
                }
                else
                {
                    _stats.totalNum--;
                }
                break;
            
            case 2:
                _stats.totalPuts++;
                _stats.totalNum++;
                _stats.capacity = _poolArray.Length;
                if (_stats.peakNUm < _stats.totalNum)
                    _stats.peakNUm = _stats.totalNum;
                break;
            
            case 5:
                _stats.totalNum -= count;
                _stats.releaseNum += count;
                break;
        }
    }
    
    /// <summary>
    /// 获取对象池统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public ClassPoolStats GetStats()
    {
        return _stats;
    }
#endif
}

#if STATS_ON && UNITY_EDITOR
/// <summary>
/// 对象池统计信息
/// </summary>
public class ClassPoolStats
{
    public int capacity;        // 容量
    public int preAllocate;     // 预热数量
    public int createNum;       // 创建数量
    public int totalNum;        // 池中总数量
    public int peakNUm;         // 峰值数量
    public long totalGets;      // 总获取次数
    public long totalPuts;      // 总放回次数
    public long releaseNum;     // 总释放数量
}
#endif