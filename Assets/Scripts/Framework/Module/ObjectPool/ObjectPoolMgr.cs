/*--------------------------------------------------------------
 * File: ObjectFactory.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/15 10:16:15 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic; 
using UnityEngine;
using Object = UnityEngine.Object;

public class ObjectPoolMgr : ManagerBase
{
    // 如果多人游戏，所有角色是共用工厂中的对象池的
    // 存储所有对象池
    private readonly Dictionary<Type, IObjectPool> _poolMap = new Dictionary<Type, IObjectPool>();
    
    /// <summary>
    /// 获取对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <returns>对象池实例</returns>
    public ObjectPool<T> GetPool<T>() where T : Object
    {
        Type type = typeof(T);
        if (!_poolMap.TryGetValue(type, out var value))
        {
            Log.Error(typeof(T).Name, "对象池不存在，请先注册");
            return null;
        }
        
        return value as ObjectPool<T>;
    }
    
    /// <summary>
    /// 获取带有配置的对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="autoReleaseTime">对象池自动释放闲置对象时间</param>
    /// <returns>对象池实例</returns>
    public ObjectPool<T> RegisterPool<T>(float autoReleaseTime = 0) where T : Object
    {
        Type type = typeof(T);
        if (_poolMap.TryGetValue(type, out var value))
        {
            return value as ObjectPool<T>;
        }
        
        var pool = new ObjectPool<T>(autoReleaseTime);
        _poolMap.Add(type, pool);
        return pool;
    }

    /// <summary>
    /// 释放指定对象池
    /// </summary>
    /// <param name="pool">要释放的对象池</param>
    public void ReleasePool(IObjectPool pool) 
    {
        if (pool == null)
        {
            Log.Error(GetType(), "要释放的对象池不能为空");
            return;
        }
        
        // 从对象池映射中移除
        foreach (var kv in new Dictionary<Type, IObjectPool>(_poolMap))
        {
            if (kv.Value == pool)
            {
                pool.Release();
                _poolMap.Remove(kv.Key);
                return;
            }
        }
    }
    
    /// <summary>
    /// 释放指定类型的对象池
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public void ReleasePool<T>() where T : Object
    {
        Type type = typeof(T);
        if (_poolMap.TryGetValue(type, out IObjectPool pool))
        {
            ReleasePool(pool);
        }
    }

    /// <summary>
    /// 释放所有对象池
    /// </summary>
    public void ReleaseAll()
    {
        // 释放所有对象池
        foreach (var kv in new Dictionary<Type, IObjectPool>(_poolMap))
        {
            kv.Value.Release();
        }
        
        _poolMap.Clear();
    }
    
    
#if STATS_ON && UNITY_EDITOR
    public List<string> DealPoolStats()
    {
        List<string> result = new List<string>();
        foreach (var pool in _poolMap)
        {
            foreach (var statMap in pool.Value.GetStats())
            {
                result.Add($"{pool.Key.Name},{statMap.Key},{statMap.Value.totalObjects},{statMap.Value.activeObjects}," +
                           $"{statMap.Value.inactiveObjects},{statMap.Value.peakObjects},{statMap.Value.totalGets}," +
                           $"{statMap.Value.totalPuts},{statMap.Value.totalRelease},{statMap.Value.totalLoad},{statMap.Value.externalObjects}");
            }
        }
        
        return result;
    }
#endif
}