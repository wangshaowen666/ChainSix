/*--------------------------------------------------------------
 * File: SlKeyboardInput.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/22 12:00:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 键盘输入源（Editor 灰盒调试）：A/← 左移，D/→ 右移，E 顺时针，Q 逆时针，空格/S/↓ 加速。
/// 长按走 GetKey 每渲染帧发送，旋转走 GetKeyDown 边缘一次。
/// </summary>
public class SlKeyboardInput : ISlInputSource
{
    private readonly int _playerIndex;

    public SlKeyboardInput(int playerIndex)
    {
        _playerIndex = playerIndex;
    }

    public void Collect(List<SlInput> pending)
    {
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.MoveLeft });
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.MoveRight });
        if (Input.GetKeyDown(KeyCode.E))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.RotateCW });
        if (Input.GetKeyDown(KeyCode.Q))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.RotateCCW });
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            pending.Add(new SlInput { PlayerIndex = _playerIndex, OpType = SlOp.FastDrop });
    }
}
