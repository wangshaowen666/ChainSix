/*--------------------------------------------------------------
 * File: Variable.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/11/07 18:15:41
 *--------------------------------------------------------------
 */

/// <summary>
/// 池化变量基类，配合Fsm等系统使用，避免值类型装箱的GC开销
/// </summary>

public abstract class Variable : IResetable
{
    public virtual void Reset(){ }
}

public sealed class Variable<T> : Variable
{
    private T _value;

    public T Value
    {
        get => _value;
        set => _value = value;
    }

    public static implicit operator Variable<T>(T value)
    {
        var varValue = CoreMgr.ClassPool.Get<Variable<T>>();
        varValue.Value = value;
        return varValue;
    }

    public static implicit operator T(Variable<T> value)
    {
        return value.Value;
    }
    
    public override void Reset()
    {
        _value = default;
    }
    
    public override string ToString()
    {
        return (_value == null ? "<Null>" : _value.ToString());
    }
}
