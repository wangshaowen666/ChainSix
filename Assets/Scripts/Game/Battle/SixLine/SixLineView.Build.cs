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
/// 六连珠表现层 · 灰盒图元构建（partial），材质由 ViewMats 共用。
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

    public static Color PieceColor(int color) => s_palette[color - 1];

    private static Mesh _circleMesh; // 圆盘网格（半径 0.5，全局共享）

    private BoardView BuildBoard(string name, Vector2 origin, float cell, int playerIndex)
    {
        var bv = new BoardView
        {
            Root = new GameObject(name).transform,
            Cell = cell,
            PlayerIndex = playerIndex,
            Events = new SlVisualEvent[_logic.Players[playerIndex].VisualEvents.Length],
            Started = new bool[_logic.Players[playerIndex].VisualEvents.Length],
            Done = new bool[_logic.Players[playerIndex].VisualEvents.Length],
            EventFrom = new Vector3[_logic.Players[playerIndex].VisualEvents.Length],
            FinalIds = new int[_logic.TotalCells],
            FinalColors = new int[_logic.TotalCells],
            TriAnims = new CircleAnim[3],
            Free = new List<CircleAnim>(_logic.TotalCells + 3),
        };
        bv.Root.SetParent(_root, false);
        bv.Root.position = origin;

        var w = _logic.Columns * cell;
        var h = BoardHeightCells * cell;

        // z 越大越靠后：底板 -> 危险区罩 -> 棋子
        MakeQuad(bv.Root, "Bg", w, h, new Color(0.13f, 0.14f, 0.17f, 1f))
            .localPosition = new Vector3(w * 0.5f, h * 0.5f, 0.5f);

        var dh = h - GridY(_logic.Rows - _logic.DangerRows) * cell;
        MakeQuad(bv.Root, "Danger", w, dh, new Color(0.9f, 0.2f, 0.2f, 0.15f))
            .localPosition = new Vector3(w * 0.5f, h - dh * 0.5f, 0.3f);

        // 视觉圈池：TotalCells 格 + 3 颗下落组，先全部隐藏入池
        for (int i = 0; i < _logic.TotalCells + 3; i++)
        {
            var a = new CircleAnim
            {
                Tr = MakeCircle(bv.Root, $"C_{i}", cell * 0.96f, Color.white),
                Color = 0,
            };
            a.Mr = a.Tr.GetComponent<MeshRenderer>();
            a.Tr.gameObject.SetActive(false);
            bv.Free.Add(a);
        }
        return bv;
    }

    private static Transform MakeQuad(Transform parent, string name, float w, float h, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        var collider = go.GetComponent<Collider>();
        if (Application.isPlaying)
            Object.Destroy(collider);
        else
            Object.DestroyImmediate(collider);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = ViewMats.GetMat(color);

        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.localScale = new Vector3(w, h, 1f);
        return tr;
    }

    private static Transform MakeCircle(Transform parent, string name, float diameter, Color color)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = CircleMesh();
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = ViewMats.GetMat(color);

        var tr = go.transform;
        tr.SetParent(parent, false);
        tr.localScale = new Vector3(diameter, diameter, 1f);
        return tr;
    }

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
