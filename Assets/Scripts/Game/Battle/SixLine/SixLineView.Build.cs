/*--------------------------------------------------------------
 * File: SixLineView.Build.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/10/22 15:40:00
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 六连珠表现层 · 灰盒图元构建（partial）：
/// 棋盘底板/危险区罩/圆形棋子（共享圆盘网格 + 实例材质上色）、格中心局部坐标。
/// 当前用 Sprites/Default（Editor 灰盒够用），真机材质兜底链随 1-6 重建为 ViewMats
/// </summary>
public partial class SixLineView
{
    // 5 色灰盒调色板（颜色 id 1~5：红/黄/蓝/绿/紫）
    private static readonly Color[] s_palette =
    {
        new(0.90f, 0.25f, 0.25f, 1f),
        new(0.95f, 0.80f, 0.25f, 1f),
        new(0.25f, 0.55f, 0.95f, 1f),
        new(0.30f, 0.80f, 0.35f, 1f),
        new(0.70f, 0.40f, 0.90f, 1f),
    };

    private static Mesh _circleMesh; // 圆盘网格（扇形三角化，半径 0.5，全局共享）

    private BoardView BuildBoard(string name, Vector2 origin, float cell)
    {
        var bv = new BoardView
        {
            Root = new GameObject(name).transform,
            Cell = cell,
            CellAnims = new CircleAnim[SixLineLogic.TotalCells],
            Prev = new int[SixLineLogic.TotalCells],
            TriAnims = new CircleAnim[3],
            Free = new List<CircleAnim>(SixLineLogic.TotalCells + 3),
        };
        bv.Root.SetParent(_root, false);
        bv.Root.position = origin; // 棋盘左下角锚定

        var w = BoardWidth * cell;
        var h = SixLineLogic.Rows * cell;

        // z 越大越靠后：底板 -> 危险区罩 -> 棋子
        MakeQuad(bv.Root, "Bg", w, h, new Color(0.13f, 0.14f, 0.17f, 1f))
            .localPosition = new Vector3(w * 0.5f, h * 0.5f, 0.5f);

        var dh = SixLineLogic.DangerRows * cell;
        MakeQuad(bv.Root, "Danger", w, dh, new Color(0.9f, 0.2f, 0.2f, 0.15f))
            .localPosition = new Vector3(w * 0.5f, h - dh * 0.5f, 0.3f);

        // 视觉圈池（TotalCells 格 + 3 颗下落组，全部先入空闲池隐藏）
        for (int i = 0; i < SixLineLogic.TotalCells + 3; i++)
        {
            var a = new CircleAnim
            {
                Tr = MakeCircle(bv.Root, $"C_{i}", cell * 0.96f, Color.white),
                Color = 0,
                LastCell = -1,
            };
            a.Mr = a.Tr.GetComponent<MeshRenderer>();
            a.Tr.gameObject.SetActive(false);
            bv.Free.Add(a);
        }
        return bv;
    }

    /// <summary>格中心局部坐标（蜂窝错行：偶行 x=col+0.5，奇行 x=col+1）</summary>
    private static Vector3 CellLocal(int row, int col, float cell, float z)
    {
        var x = col + 0.5f + (SixLineLogic.RowIsWide(row) ? 0f : 0.5f);
        return new Vector3(x * cell, (row + 0.5f) * cell, z);
    }

    /// <summary>图元方块（底板/危险区罩；Sprites/Default，Editor 灰盒够用，真机兜底随 1-6 重建 ViewMats）</summary>
    private static Transform MakeQuad(Transform parent, string name, float w, float h, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>()); // 图元自带碰撞体，纯表现去掉

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };

        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.localScale = new Vector3(w, h, 1f);
        return tr;
    }

    /// <summary>圆形棋子（共享圆盘网格 + 实例材质上色）</summary>
    private static Transform MakeCircle(Transform parent, string name, float diameter, Color color)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = CircleMesh();
        var mr = go.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };

        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.localScale = new Vector3(diameter, diameter, 1f);
        return tr;
    }

    /// <summary>圆盘网格（24 段扇形，半径 0.5，随缩放适配格尺寸）</summary>
    private static Mesh CircleMesh()
    {
        if (_circleMesh != null)
            return _circleMesh;

        const int seg = 24;
        var verts = new Vector3[seg + 1];
        var tris = new int[seg * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < seg; i++)
        {
            var a = (float)(i * Math.PI * 2.0 / seg);
            verts[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + i;
            tris[i * 3 + 2] = 1 + (i + 1) % seg;
        }
        _circleMesh = new Mesh { vertices = verts, triangles = tris };
        _circleMesh.RecalculateNormals();
        return _circleMesh;
    }
}
