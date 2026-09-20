/*--------------------------------------------------------------
 * File: EventCenter.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/30 17:34:10 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

/// <summary>
/// 抛事件时，如果注册的回调参数结构、数量不一致都将不会触发;
/// 事件 ID 类型提升为类级泛型参数（EventMgr&lt;TEventId&gt;）：
/// 业务实例化 EventMgr&lt;GameEvent&gt;（GameMgr.Event）后，Register/Send 只需提供回调/参数，
/// 类型由编译器推断，无需再写 &lt;GameEvent, T&gt; 双泛型噪音。
/// </summary>
public class EventMgr<TEventId> : ManagerBase where TEventId : struct, Enum
{
    // 具体的enum类型 .Net做过优化，EqualityComparer 直接比较底层int值，无装箱
    private readonly Dictionary<int, List<Delegate>> _eventMap = new Dictionary<int, List<Delegate>>();

    private int GetKey(TEventId eventId)
    {
        return EqualityComparer<TEventId>.Default.GetHashCode(eventId);
    }

    public void Register(TEventId eventId, Action callback)
    {
        RegisterEvent(eventId, callback);
    }

    public void Register<T>(TEventId eventId, Action<T> callback)
    {
        RegisterEvent(eventId, callback);
    }

    public void Register<T1, T2>(TEventId eventId, Action<T1, T2> callback)
    {
        RegisterEvent(eventId, callback);
    }

    public void Register<T1, T2, T3>(TEventId eventId, Action<T1, T2, T3> callback)
    {
        RegisterEvent(eventId, callback);
    }

    public void UnRegister(TEventId eventId, Action callback)
    {
        UnRegisterEvent(eventId, callback);
    }

    public void UnRegister<T>(TEventId eventId, Action<T> callback)
    {
        UnRegisterEvent(eventId, callback);
    }

    public void UnRegister<T1, T2>(TEventId eventId, Action<T1, T2> callback)
    {
        UnRegisterEvent(eventId, callback);
    }

    public void UnRegister<T1, T2, T3>(TEventId eventId, Action<T1, T2, T3> callback)
    {
        UnRegisterEvent(eventId, callback);
    }

    public void UnRegisterAll(TEventId eventId)
    {
        var key = GetKey(eventId);
        _eventMap.Remove(key);
    }

    public void Clear()
    {
        _eventMap.Clear();
    }

    public void Send(TEventId eventId)
    {
        var key = GetKey(eventId);
        if (_eventMap.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                (list[i] as Action)?.Invoke();
            }
        }
    }

    public void Send<T>(TEventId eventId, T arg)
    {
        var key = GetKey(eventId);
        if (_eventMap.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                (list[i] as Action<T>)?.Invoke(arg);
            }
        }
    }

    public void Send<T1, T2>(TEventId eventId, T1 arg1, T2 arg2)
    {
        var key = GetKey(eventId);
        if (_eventMap.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                (list[i] as Action<T1, T2>)?.Invoke(arg1, arg2);
            }
        }
    }

    public void Send<T1, T2, T3>(TEventId eventId, T1 arg1, T2 arg2, T3 arg3)
    {
        var key = GetKey(eventId);
        if (_eventMap.TryGetValue(key, out var list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                (list[i] as Action<T1, T2, T3>)?.Invoke(arg1, arg2, arg3);
            }
        }
    }

    private void RegisterEvent(TEventId eventId, Delegate callback)
    {
        int key = GetKey(eventId);
        if (!_eventMap.TryGetValue(key, out var list))
        {
            list = new List<Delegate>();
            _eventMap[key] = list;
        }

        if (list.Contains(callback))
        {
            Log.Warning("重复注册事件回调", eventId);
            return;
        }

        list.Add(callback);
    }

    private void UnRegisterEvent(TEventId eventId, Delegate callback)
    {
        int key = GetKey(eventId);
        if (_eventMap.TryGetValue(key, out var list))
        {
            var ret = list.Remove(callback);
            if (!ret)
            {
                Log.Error("正在注销未被监听的回调，请检查注册、注销是否为同一个方法", eventId);
            }
            else
            {
                if (list.Count == 0)
                {
                    _eventMap.Remove(key);
                }
            }
        }
        else
        {
            Log.Error("正在注销未被注册的事件", eventId);
        }
    }
}
