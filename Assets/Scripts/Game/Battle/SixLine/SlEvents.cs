/*--------------------------------------------------------------
 * File: SlEvents.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

/// <summary>六连珠跨层结构体：逻辑层不产字符串，字段追加只允许加在尾部</summary>

/// <summary>玩家操作（形状对齐 NetMsg.PlayerInput，阶段 3 由 FrameData.Inputs 直接转换）</summary>
public struct SlInput
{
    public int PlayerIndex; // 0/1
    public int OpType;
    public int Param1;
    public int Param2;
}

/// <summary>操作类型（阶段 3 与 proto 对齐）</summary>
public static class SlOp
{
    public const int MoveLeft = 1;   // 长按
    public const int MoveRight = 2;  // 长按
    public const int RotateCW = 3;   // 点按：60° 顺时针
    public const int RotateCCW = 4;  // 点按：60° 逆时针
    public const int FastDrop = 5;   // 长按
}
