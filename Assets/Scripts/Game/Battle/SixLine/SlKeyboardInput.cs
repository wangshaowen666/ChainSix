using System.Collections.Generic;
using UnityEngine;

/// <summary>本地双人键盘：P0 A/D、Q/E、空格；P1 方向键、U/O、下。</summary>
public class SlKeyboardInput : ISlInputSource
{
    private readonly int _playerIndex;

    public SlKeyboardInput(int playerIndex)
    {
        _playerIndex = playerIndex;
    }

    public void Collect(List<SlInput> pending)
    {
        if (Input.GetKeyDown(_playerIndex == 0 ? KeyCode.E : KeyCode.O))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.RotateCW });
        if (Input.GetKeyDown(_playerIndex == 0 ? KeyCode.Q : KeyCode.U))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.RotateCCW });
    }

    public void CollectHeld(List<SlInput> pending)
    {
        if (Input.GetKey(_playerIndex == 0 ? KeyCode.A : KeyCode.LeftArrow))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.MoveLeft });
        if (Input.GetKey(_playerIndex == 0 ? KeyCode.D : KeyCode.RightArrow))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.MoveRight });
        if (Input.GetKey(_playerIndex == 0 ? KeyCode.Space : KeyCode.DownArrow))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.FastDrop });
    }

    public void Reset() { }
}
