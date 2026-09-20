/*--------------------------------------------------------------
 * File: Fsm.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/07 17:04:18 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;

public class Fsm : IResetable
{
    private readonly Dictionary<Type, FsmState> _states = new Dictionary<Type, FsmState>();
    // 序号比较会将字符串视为一系列的UTF-16代码单元进行精确的二进制比较,只严格检查每个字符的编码值是否完全一致,性能最高
    private Dictionary<string, Variable> _datas = new Dictionary<string, Variable>(StringComparer.Ordinal);
    private FsmState _curState;

    public FsmState CurState => _curState;
    
    public static Fsm Create()
    {
        Fsm fsm = CoreMgr.ClassPool.Get<Fsm>();
        fsm.Init();
        return fsm;
    }

    public static Fsm Create<T>(params T[] states) where T : FsmState
    {
        Fsm fsm = CoreMgr.ClassPool.Get<Fsm>();
        fsm.Init(states);
        return fsm;
    }

    public static Fsm Create<T>(List<T> states) where T : FsmState
    {
        Fsm fsm = CoreMgr.ClassPool.Get<Fsm>();
        fsm.Init(states);
        return fsm;
    }

    public void ChangeState<T>() where T : FsmState
    {
        var state = GetState(typeof(T));
        if (state == null)
        {
            Log.Error("无效的状态类型", typeof(T).Name);
            return;
        }

        if (_curState == state)
            return;

        _curState?.OnExit();
        _curState = state;
        state.OnEnter();
    }

    public void AddState<T>(T state) where T : FsmState
    {
        Type type = state.GetType();
        if (!_states.TryAdd(type, state))
        {
            Log.Error("状态已经存在, 不可重复添加：", type.Name);
            return;
        }

        state.OnInit(this);
    }

    public T GetData<T>(string name)
    {
        if (_datas.TryGetValue(name, out var data))
        {
            if (data is Variable<T> varT)
                return varT.Value;

            Log.Error("状态机数据类型不匹配:", name, "期望:", typeof(T).Name, "实际:", data.GetType().Name);
            return default;
        }

        Log.Warning("状态机不存在的key：", name);
        return default;
    }

    public void SetData<T>(string name, T value)
    {
        if (_datas.TryGetValue(name, out var old))
            CoreMgr.ClassPool.Recycle(old);

        if (value is Variable varValue)
        {
            _datas[name] = varValue;
            return;
        }

        var newVar = CoreMgr.ClassPool.Get<Variable<T>>();
        newVar.Value = value;
        _datas[name] = newVar;
    }

    public void RemoveData(string name)
    {
        if (_datas.Remove(name, out var old))
            CoreMgr.ClassPool.Recycle(old);
    }

    public void Reset()
    {
        foreach (var kv in _datas)
            CoreMgr.ClassPool.Recycle(kv.Value);

        _datas.Clear();
        _states.Clear();
        _curState = null;
    }

    private void Init()
    {
        
    }
    
    private void Init<T>(IList<T> states) where T : FsmState
    {
        if (states == null || states.Count == 0)
        {
            Log.Error("状态机拥有状体不可为空");
            return;
        }
        
        foreach (var state in states)
        {
            if (state == null)
            {
                Log.Error("无效的状态");
                return;
            }
            
            Type type = state.GetType();
            if (_states.ContainsKey(type))
            {
                Log.Error("状态已经存在, 不可重复添加：", type.Name);
                return;
            }
            
            _states.Add(type, state);
            state.OnInit(this);
        }
    }

    private FsmState GetState(Type type)
    {
        if (type == null)
        {
            return null;
        }

        _states.TryGetValue(type, out var state);
        return state;
    }
}