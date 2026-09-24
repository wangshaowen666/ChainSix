/*--------------------------------------------------------------
 * File: SlEvents.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/21 11:00:00
 *--------------------------------------------------------------
 */

/// <summary>
/// 六连珠跨层结构体（对齐 VampireSurvivor 的 VsEvents 范式）：
/// 逻辑层不产字符串，文案由表现层组装；结构体字段即协议形状，字段追加只允许加在尾部
/// </summary>

/// <summary>
/// 玩家操作（形状对齐 NetMsg.PlayerInput，阶段 3 联机由 FrameData.Inputs 直接转换，Logic 零改动）：
/// 长按类（按住期间输入源每帧发送）：左移/右移/加速下落；点按类（按下边缘一次）：顺/逆时针旋转
/// </summary>
public struct SlInput
{
    public int PlayerIndex; // 0/1（本地双人/人机灰盒）；联机由 PlayerId 映射
    public int OpType;
    public int Param1;
    public int Param2;
}

/// <summary>操作类型常量（阶段 3 定稿 proto 后与 NetMsg.PlayerInput.OpType 对齐）</summary>
public static class SlOp
{
    public const int MoveLeft = 1;   // 长按：左移（连续平移，速度 SixLineLogic.MoveSpeed）
    public const int MoveRight = 2;  // 长按：右移（连续平移）
    public const int RotateCW = 3;   // 点按：60° 顺时针（▲/▽ 互转 + 颜色轮换）
    public const int RotateCCW = 4;  // 点按：60° 逆时针
    public const int FastDrop = 5;   // 长按：加速下落
}
