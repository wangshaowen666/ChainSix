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
/// 六连珠表现层 · 视觉圈与插值（partial）：逻辑帧定目标位，渲染帧匀速趋近。
/// </summary>
public partial class SixLineView
{
    private class CircleAnim
    {
        public Transform Tr;
        public MeshRenderer Mr;
        public int Color;      // 颜色 id（0 = 未着色）
        public Vector3 Pos;    // 视觉位置（棋盘局部）
        public Vector3 Target;
        public float Speed;    // 单位/秒
        public int LastCell;   // 上一逻辑帧占用的格子下标（-1 = 无）
        public int Stamp;      // 最近被认领的帧号（同帧防重复认领）
    }

    private class BoardView
    {
        public Transform Root;
        public float Cell;
        public CircleAnim[] CellAnims; // 格槽 -> 视觉圈（null = 空）
        public int[] Prev;             // 上一逻辑帧棋盘（来源匹配）
        public bool TriActive;
        public int TriSeqPos = -1;     // 变化 = 旧组落定 + 新组出生
        public int TriRotation;        // 变化 = 触发旋转过渡
        public CircleAnim[] TriAnims;  // 按球序绑定 0=A 1=B 2=C
        public List<CircleAnim> Free;  // 空闲池（含刚释放待认领的）
    }

    private static void UpdateAnims(BoardView bv, float dt)
    {
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
