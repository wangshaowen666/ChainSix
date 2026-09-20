using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

// 提供给Mgr存储的类型，字典没法直接存范型类
public interface IObjectPool
{
    void Release();
#if STATS_ON && UNITY_EDITOR
    Dictionary<string, ObjectPoolStats> GetStats();
#endif
}

// 对象从Addressable加载为异步，池只提供缓存功能，不负责创建；预热功能也由外部自行按需实现
// 外部创建的对象，需要清理时，只需执行将其放回池中，池负责清理
public class ObjectPool<T> : IObjectPool where T : Object
{
    private readonly Dictionary<string, Stack<T>> _freePool = new();
    private readonly Dictionary<string, HashSet<T>> _activeObjs = new();
    private readonly Dictionary<string, float> _lastPutTime = new();
    private readonly float _autoReleaseTime;
    private readonly int _maxCapacity;

    private CancellationTokenSource _cancel;

#if STATS_ON && UNITY_EDITOR
    private readonly Dictionary<string, ObjectPoolStats> _stats = new();
#endif

    public ObjectPool(float autoReleaseTime = 0, int maxCapacity = 100)
    {
        _autoReleaseTime = autoReleaseTime;
        _maxCapacity = maxCapacity;
        if (autoReleaseTime > 0)
            _cancel = CoreMgr.Timer.StartSecondDelay(autoReleaseTime, AutoReleaseObj);
    }

    public T GetObj(string key)
    {
        if (!_freePool.TryGetValue(key, out var stack) || stack.Count == 0)
        {
            RecordStats(key, 5);
            return null;
        }

        var obj = stack.Pop();
        if (!_activeObjs.TryGetValue(key, out var active))
            _activeObjs[key] = active = new HashSet<T>();
        active.Add(obj);
        
        RecordStats(key, 2);
        return obj;
    }

    public void PutObj(string key, T obj)
    {
        if (obj == null) return;

        if (!_freePool.TryGetValue(key, out var freeStack))
            _freePool[key] = freeStack = new Stack<T>();
        
        var wasActive = _activeObjs.TryGetValue(key, out var active) && active.Remove(obj);
        if (!wasActive && freeStack.Contains(obj))
        {
            Log.Error($"对象已在对象池中: {obj.name}");
            return;
        }

        if (freeStack.Count >= _maxCapacity)
        {
            ReleaseObj(key, obj);
            RecordStats(key, 3);
            return;
        }

        freeStack.Push(obj);
        _lastPutTime[key] = Time.time;
        RecordStats(key, wasActive ? 4 : 1);
    }

    public void Release()
    {
        CoreMgr.Timer.Stop(_cancel);

        foreach (var (key, stack) in _freePool)
            while (stack.Count > 0)
                ReleaseObj(key, stack.Pop());

        foreach (var (key, active) in _activeObjs)
            foreach (var obj in active)
                ReleaseObj(key, obj);

        _freePool.Clear();
        _activeObjs.Clear();
        _lastPutTime.Clear();
#if STATS_ON && UNITY_EDITOR
        _stats.Clear();
#endif
    }

    private void AutoReleaseObj()
    {
        try
        {
            float currentTime = Time.time;
            List<string> keysToRemove = null;

            foreach (var (key, stack) in _freePool)
            {
                if (stack.Count == 0) continue;
                if (currentTime - _lastPutTime.GetValueOrDefault(key) <= _autoReleaseTime) continue;

                while (stack.Count > 0)
                {
                    ReleaseObj(key, stack.Pop());
                    RecordStats(key, 3);
                }

                keysToRemove ??= new List<string>();
                keysToRemove.Add(key);
            }

            if (keysToRemove == null) return;

            foreach (var key in keysToRemove)
            {
                _freePool.Remove(key);
                _lastPutTime.Remove(key);
            }
        }
        finally
        {
            _cancel = CoreMgr.Timer.StartSecondDelay(_autoReleaseTime, AutoReleaseObj);
        }
    }

    private void ReleaseObj(string key, T obj)
    {
        if (obj == null) return;

        if (obj is MonoBehaviour mono)
            Object.Destroy(mono.gameObject);
        else
            Object.Destroy(obj);

        CoreMgr.Res.Unload(key);
    }

    // flag: 1-新对象放入, 2-取出, 3-释放, 4-旧对象放回, 5-取不到，自行加载
    [Conditional("STATS_ON")]
    private void RecordStats(string key, int flag)
    {
#if STATS_ON && UNITY_EDITOR
        if (!_stats.TryGetValue(key, out var stats))
            _stats[key] = stats = new ObjectPoolStats();

        switch (flag)
        {
            case 1:
                stats.totalObjects++;
                stats.externalObjects--;
                stats.totalPuts++;
                stats.inactiveObjects++;
                if (stats.activeObjects + stats.inactiveObjects > stats.peakObjects)
                    stats.peakObjects = stats.activeObjects + stats.inactiveObjects;
                break;
            case 2:
                stats.totalGets++;
                stats.activeObjects++;
                stats.inactiveObjects--;
                break;
            case 3:
                stats.totalObjects--;
                stats.inactiveObjects--;
                stats.totalRelease++;
                break;
            case 4:
                stats.activeObjects--;
                stats.totalPuts++;
                stats.inactiveObjects++;
                if (stats.activeObjects + stats.inactiveObjects > stats.peakObjects)
                    stats.peakObjects = stats.activeObjects + stats.inactiveObjects;
                break;
            case 5:
                stats.totalLoad++;
                stats.externalObjects++;
                break;
        }
#endif
    }

#if STATS_ON && UNITY_EDITOR
    public Dictionary<string, ObjectPoolStats> GetStats() => new(_stats);
#endif
}

#if STATS_ON && UNITY_EDITOR
public class ObjectPoolStats
{
    public int totalObjects;    // 总对象数
    public int activeObjects;   // 活跃对象数
    public int inactiveObjects; // 非活跃对象数
    public int peakObjects;     // 峰值对象数
    public long totalGets;      // 总获取次数
    public long totalPuts;      // 总放回次数
    public long totalRelease;   // 总释放次数
    public int totalLoad;       // 总加载次数（池中无对象时）
    public int externalObjects; // 外部对象数（尚未入池）
}
#endif
