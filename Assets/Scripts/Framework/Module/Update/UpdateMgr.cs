/*--------------------------------------------------------------
 * File: GameFrameworkEntry.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/11 11:29:18 
 *--------------------------------------------------------------
 */

using System.Collections.Generic;

/// <summary>
/// 规避GF框架很多空的Update调用，采用按需注册的形式
/// </summary>
public class UpdateMgr : ManagerBase
{
    private readonly List<IUpdateable> _updateList = new List<IUpdateable>();
    // Update时可能触发注册和移除，放到缓存中，在下一帧的遍历中才生效
    private readonly List<IUpdateable> _addCaches = new List<IUpdateable>();
    // 避免注册时列表每次Contains遍历的性能开销
    private readonly HashSet<IUpdateable> _registered = new();
    // 允许同一个IUpdateable多次调用移除，通过HashSet保证唯一
    private readonly HashSet<IUpdateable> _rmvCaches = new HashSet<IUpdateable>();
    // 合并用的复用列表，避免每次 DealCache 分配 GC
    private readonly List<IUpdateable> _merged = new();

    private bool _isAdd;
    private bool _isRmv;
    
#if STATS_ON && UNITY_EDITOR
    private readonly Unity.Profiling.ProfilerMarker _updateMarker = new ("WGame.Update");
#endif

    public void RegisterUpdate(IUpdateable updateable)
    {
        // 同一帧 添加 移除 又添加会出现的问题，但实际触发可能极低，先不考虑
        // if (_rmvCaches.Remove(updateable))
        // {
        //     _registered.Add(updateable);
        //     return;
        // }

        if (!_registered.Add(updateable))
        {
            Log.Error("重复注册 Updateable:", updateable.GetType().Name);
            return;
        }

        _isAdd = true;
        for (int i = 0; i < _addCaches.Count; i++)
        {
            if (updateable.Priority > _addCaches[i].Priority)
            {
                _addCaches.Insert(i, updateable);
                return;
            }
        }
        
        _addCaches.Add(updateable);
    }

    public void UnRegisterUpdate(IUpdateable updateable)
    {
        _isRmv = true;
        _rmvCaches.Add(updateable);
        _registered.Remove(updateable);
    }
    
    public void MyUpdate(float deltaTime, float realDeltaTime)
    {
        if (_isAdd || _isRmv)
        {
            DealCache();
            _isAdd = false;
            _isRmv = false;
        }

#if STATS_ON && UNITY_EDITOR
        using (_updateMarker.Auto())
        {
            foreach (var u in _updateList)
            {
                u.MyUpdate(deltaTime, realDeltaTime);
            }
        }
#else
        foreach (var u in _updateList)
        {
            u.MyUpdate(deltaTime, realDeltaTime);
        }
#endif
    }

    private void DealCache()
    {
        if (_isRmv && _rmvCaches.Count > 0)
        {
            for (int i = _updateList.Count - 1; i >= 0; i--)
            {
                if (_rmvCaches.Remove(_updateList[i]))
                    _updateList.RemoveAt(i);
            }
        }

        if (_isAdd && _addCaches.Count > 0)
        {
            // 过滤掉同时被移除的项
            if (_isRmv)
            {
                for (int i = _addCaches.Count - 1; i >= 0; i--)
                {
                    if (_rmvCaches.Remove(_addCaches[i]))
                        _addCaches.RemoveAt(i);
                }
            }

            // _updateList 和 _addCaches 都按优先级降序，O(n+m)
            if (_addCaches.Count > 0)
            {
                _merged.Clear();
                int i = 0, j = 0;
                while (i < _updateList.Count && j < _addCaches.Count)
                {
                    if (_updateList[i].Priority >= _addCaches[j].Priority)
                        _merged.Add(_updateList[i++]);
                    else
                        _merged.Add(_addCaches[j++]);
                }
                while (i < _updateList.Count) _merged.Add(_updateList[i++]);
                while (j < _addCaches.Count) _merged.Add(_addCaches[j++]);

                _updateList.Clear();
                _updateList.AddRange(_merged);
            }

            _addCaches.Clear();
        }

        if (_rmvCaches.Count > 0)
        {
            Log.Error("尝试移除未注册的Updateable");
            _rmvCaches.Clear();
        }
    }

    public override void OnSceneExit()
    {
        // _updateList.Clear();
        // _addCaches.Clear();
        // _registered.Clear();
        // _rmvCaches.Clear();
        // _merged.Clear();
        // _isAdd = false;
        // _isRmv = false;
    }
}
