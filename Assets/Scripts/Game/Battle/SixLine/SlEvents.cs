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
/// - 选列落子（Param1=列号）
/// - 加速下落（"独立 op vs 落子附带标志"的协议形状在阶段 1 调参期拍板，见任务清单待定决策）
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
    public const int SelectColumn = 1; // 选列落子（Param1=列号）
    public const int FastDrop = 2;     // 加速下落（协议形状待拍板）
}
