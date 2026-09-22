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
/// 键盘输入源（Editor 灰盒调试；触屏虚拟按键随 1-6 接入，1-7 正式化双输入源）：
/// A/← 长按左移，D/→ 长按右移，E 顺时针旋转，Q 逆时针旋转，空格/S/↓ 长按加速下落。
/// 长按键走 GetKey 每渲染帧发送，旋转走 GetKeyDown 按下边缘一次。
/// 走 legacy Input（工程 Active Input Handling = Both）
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
