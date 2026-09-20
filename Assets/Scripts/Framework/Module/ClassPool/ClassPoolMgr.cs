/*--------------------------------------------------------------
 * File: ClassPoolFactory.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2024/03/20 18:23:22 
 * Copyright: Copyright © 2024 wangshaowen. All rights reserved.
 *--------------------------------------------------------------
 */

/*
 * 每帧100次获取持续5s 测试发现 耗时比直接new更久(无任何成员的空类) （已优化）
 * 实测当类字段增多，所占内存字节增多，new耗时指数级增长，对象池只会在预创建的时候有一次开销，后续缓存获取耗时变化不大
 *
 * 主要性能问题   
   1. 字典查找开销过大   
   每次Get/Recycle都要进行字典查找，这是最大的性能瓶颈。   
   2. 类型检查过于严格   
   泛型方法中不必要的类型检查增加了开销。   
   3. 堆栈操作可能不如数组高效   
   Stack的Push/Pop操作相比数组索引有额外开销。
   
   自己测试统计发现var type = typeof(T);同样有一定开销
   类型获取 58ms    字典查找85ms    池类型检查 58ms  栈操作 66ms
   
   修改后测试，原版208ms  优化了工厂类型获取和字典查找 取消了池的类型检查  101ms   用数组替换栈后 34ms， 纯new开销 41ms   
   如果去掉[MethodImpl(MethodImplOptions.AggressiveInlining)]  耗时变成69ms，所以数组替换栈的提升没有翻倍这么大
   
   [MethodImpl(MethodImplOptions.AggressiveInlining)] 强制内联建议
   方法内联是JIT（Just-In-Time）编译器的一种优化技术，它将方法调用替换为方法体本身的代码，从而消除方法调用的开销。
   消除的开销：方法调用指令（call指令）参数压栈/出栈 栈帧创建和销毁 寄存器保存和恢复
   性能提升：减少CPU指令数 更好的CPU缓存局部性 减少分支预测失败
   缺点：内联后，每个调用点都会插入完整的代码，可能导致生成的机器代码体积过大；难以断点；调用堆栈不完整；
   
 *
 * 对象池的主要优势是减少GC压力，而不是提升速度。在GC频繁触发时，对象池可以减少GC带来的卡顿。所以，即使对象池的获取速度稍慢，但整体性能可能更稳定。 
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class ClassPoolMgr : ManagerBase
{
    // 新增池的缓存，使工厂字典查找池的操作由每次都查询改成只在构造时查询一次
    private static class PoolCache<T> where T: class, IResetable, new()
    {
        public static ClassPool Pool;
        static PoolCache()
        {
            var type = typeof(T);
            if (!_poolMap.TryGetValue(type, out Pool))
            {
                Pool = new ClassPool();
                _poolMap[type] = Pool;
            }
        }
    }
    
    private static readonly Dictionary<Type, ClassPool> _poolMap = new Dictionary<Type, ClassPool>();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PreAllocate<T>(int count) where T : class, IResetable, new()
    {
        PoolCache<T>.Pool.PreAllocate<T>(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : class, IResetable, new()
    {
        return PoolCache<T>.Pool.Get<T>();
    }

    public void Recycle(IResetable item)
    {
        var type = item.GetType();
        if (!_poolMap.TryGetValue(type, out var pool))
        {
            Log.Error(Log.LogColor.Red, "正在回收未经对象池工厂创建的对象：", type.Name);
            return;
        }
        
        pool.Recycle(item);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Recycle<T>(T item) where T : class, IResetable, new()
    {
        PoolCache<T>.Pool.Recycle(item);
    }

    public void Release(Type type)
    {
        if (!_poolMap.TryGetValue(type, out var pool))
        {
            Log.Error(Log.LogColor.Red, "正在清除工厂中不存在的池子类型：", type.Name);
            return;
        }
        
        pool.Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release<T>() where T : class, IResetable, new()
    {
        PoolCache<T>.Pool.Release();
    }

    public void ReleaseAll()
    {
        foreach (var pool in _poolMap.Values)
        {
            pool.Release();
        }
        
        // _poolMap始终不清理，清理之后，PoolCache<T>中的Container依旧存在，get依然能够获取，但是非泛型方法_poolMap会找不到。
        // 静态PoolCache在构造时创建了Container，无销毁逻辑
        //_poolMap.Clear();
    }
        
#if STATS_ON && UNITY_EDITOR
    public List<string> DealPoolStats()
    {
        List<string> result = new List<string>();
        foreach (var pool in _poolMap)
        {
            var stats = pool.Value.GetStats();
            result.Add($"{pool.Key.Name},{stats.capacity},{stats.preAllocate}," +
                       $"{stats.createNum},{stats.totalNum},{stats.peakNUm}," +
                       $"{stats.totalGets},{stats.totalPuts},{stats.releaseNum}");
        }
        
        return result;
    }
#endif
}