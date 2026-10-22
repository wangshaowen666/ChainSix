/*--------------------------------------------------------------
 * File: SixLineView.Anim.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:40:00
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 六连珠表现层 · 视觉圈与插值（partial）：
/// - CircleAnim：单颗视觉圈（目标位由逻辑帧对账确定，渲染帧向目标匀速趋近）
/// - BoardView：单方棋盘表现（格槽绑定 + 上一帧棋盘 + 下落组三颗 + 空闲池）
/// - 渲染帧推进：格子圈与下落组圈统一按速度趋近（逻辑 20Hz 与渲染帧率解耦）
/// </summary>
public partial class SixLineView
{
    /// <summary>单颗视觉圈：逻辑帧对账确定目标位，渲染帧向目标匀速趋近</summary>
    private class CircleAnim
    {
        public Transform Tr;
        public MeshRenderer Mr;
        public int Color;      // 颜色 id（0 = 未着色）
        public Vector3 Pos;    // 视觉位置（棋盘局部）
        public Vector3 Target; // 目标位置（棋盘局部）
        public float Speed;    // 趋近速度（单位/秒）
        public int LastCell;   // 上一逻辑帧占用的格子下标（-1 = 无，如刚落定的下落组）
        public int Stamp;      // 最近被认领的逻辑帧号（同帧防重复认领）
    }

    /// <summary>单方棋盘表现（格子下标与 Board.Cells 对齐）</summary>
    private class BoardView
    {
        public Transform Root;
        public float Cell;
        public CircleAnim[] CellAnims; // 格槽 -> 绑定视觉圈（null = 空格）
        public int[] Prev;             // 上一逻辑帧棋盘（来源匹配）
        public bool TriActive;         // 下落组是否激活
        public int TriSeqPos = -1;     // 当前下落组对应的出子游标（变化 = 旧组落定 + 新组出生）
        public int TriRotation;        // 下落组已同步的旋转姿态（变化 = 触发旋转过渡动画）
        public CircleAnim[] TriAnims;  // 下落组三颗（按球序绑定：0=A 1=B 2=C，颜色身份不变）
        public List<CircleAnim> Free;  // 空闲视觉圈池（含刚释放待认领的）
    }

    private static void UpdateAnims(BoardView bv, float dt)
    {
        // 格子圈 + 下落组圈统一按速度趋近
        for (int i = 0; i < bv.CellAnims.Length; i++)
        {
            var a = bv.CellAnims[i];
            if (a != null)
                StepAnim(a, dt);
        }

        if (bv.TriActive)
            for (int i = 0; i < 3; i++)
                StepAnim(bv.TriAnims[i], dt);
    }

    private static void StepAnim(CircleAnim a, float dt)
    {
        a.Pos = Vector3.MoveTowards(a.Pos, a.Target, a.Speed * dt);
        a.Tr.localPosition = a.Pos;
    }

    /// <summary>设置下落组单颗的目标位与颜色（颜色身份不变，仅目标位变化）</summary>
    private static void SetTriTarget(CircleAnim a, float x, float y, int color)
    {
        if (a.Color != color)
        {
            a.Color = color;
            a.Mr.material.color = s_palette[color - 1];
        }
        a.Target = new Vector3(x, y, 0.2f);
        a.Tr.gameObject.SetActive(true);
    }
}
