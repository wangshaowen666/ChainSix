using System.Collections.Generic;

/// <summary>纯输入状态，由 UGUI 指针事件写入，不向热更脚本动态挂 MonoBehaviour。</summary>
public class SlTouchInput : ISlInputSource
{
    private readonly int _playerIndex;
    private readonly bool[] _held = new bool[SlOp.FastDrop + 1];
    private readonly List<SlInput> _edges = new List<SlInput>(8);

    public SlTouchInput(int playerIndex)
    {
        _playerIndex = playerIndex;
    }

    public void Press(int op)
    {
        if (op == SlOp.RotateCW || op == SlOp.RotateCCW)
            _edges.Add(new SlInput { PlayerIndex = _playerIndex, OpType = op });
        else if (op > 0 && op < _held.Length)
            _held[op] = true;
    }

    public void Release(int op)
    {
        if (op > 0 && op < _held.Length)
            _held[op] = false;
    }

    public void Collect(List<SlInput> pending)
    {
        pending.AddRange(_edges);
        _edges.Clear();
    }

    public void CollectHeld(List<SlInput> pending)
    {
        for (int op = 1; op < _held.Length; op++)
            if (_held[op])
                pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = op });
    }

    public void Reset()
    {
        System.Array.Clear(_held, 0, _held.Length);
        _edges.Clear();
    }
}
