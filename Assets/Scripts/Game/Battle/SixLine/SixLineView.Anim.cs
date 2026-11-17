using System.Collections.Generic;
using UnityEngine;

public partial class SixLineView
{
    private class CircleAnim
    {
        public Transform Tr;
        public MeshRenderer Mr;
        public int Id;
        public int Color;
        public Vector3 Pos;
        public int Stamp;
    }

    private class BoardView
    {
        public Transform Root;
        public int PlayerIndex;
        public float Cell;
        public bool Initialized;
        public readonly Dictionary<int, CircleAnim> Live = new Dictionary<int, CircleAnim>();
        public List<CircleAnim> Free;
        public readonly List<CircleAnim> Recycle = new List<CircleAnim>();
        public bool TriActive;
        public int TriSeqPos = -1;
        public CircleAnim[] TriAnims;
        public Vector2 FromCenter;
        public Vector2 ToCenter;
        public float TriAngle;
        public float TargetAngle;
        public bool Playing;
        public int LastPresentationFrame = -1;
        public int EventCount;
        public float EventTime;
        public float EventEndTime;
        public SlVisualEvent[] Events;
        public bool[] Started;
        public bool[] Done;
        public Vector3[] EventFrom;
        public int[] FinalIds;
        public int[] FinalColors;
        public int ReconcileStamp;
        public int LastChain;
    }

    private void UpdateAnims(BoardView bv, float alpha, float dt)
    {
        if (bv.Playing)
        {
            bv.EventTime += dt;
            PlayEvents(bv, bv.EventTime);
            if (bv.EventTime >= bv.EventEndTime)
                FinishPresentation(bv);
        }
        if (!bv.TriActive)
            return;
        var center = Vector2.Lerp(bv.FromCenter, bv.ToCenter, alpha);
        bv.TriAngle = Mathf.MoveTowards(bv.TriAngle, bv.TargetAngle, 720f * dt);
        var radius = bv.Cell * RowPitch * (2f / 3f);
        for (int b = 0; b < 3; b++)
        {
            var angle = (90f + 120f * b + bv.TriAngle) * Mathf.Deg2Rad;
            var a = bv.TriAnims[b];
            a.Pos = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, .1f);
            a.Tr.localPosition = a.Pos;
        }
    }

    private void PlayEvents(BoardView bv, float time)
    {
        for (int i = 0; i < bv.EventCount; i++)
        {
            var e = bv.Events[i];
            if (bv.Done[i] || time < e.StartFrame * LogicStepSeconds)
                continue;
            var from = bv.EventFrom[i];
            var to = new Vector3(e.ToX.AsFloat * bv.Cell, e.ToY.AsFloat * bv.Cell, .1f);
            var a = GetCircle(bv, e.PieceId, e.Color, from);
            if (!bv.Started[i])
            {
                bv.Started[i] = true;
                if (e.Kind == SlVisualKind.Clear && e.Chain > bv.LastChain)
                {
                    bv.LastChain = e.Chain;
                    _hud?.ShowChain(bv.PlayerIndex, e.Chain, e.Special);
                }
            }
            var u = Mathf.Clamp01((time - e.StartFrame * LogicStepSeconds) / (e.DurationFrames * LogicStepSeconds));
            if (u >= 1f)
            {
                bv.Done[i] = true;
                if (e.Kind == SlVisualKind.Clear)
                {
                    RecycleCircle(bv, a);
                    continue;
                }
                a.Pos = to;
                a.Tr.localPosition = to;
                a.Tr.localScale = Vector3.one * (bv.Cell * .96f);
                a.Mr.sharedMaterial = ViewMats.GetMat(s_palette[a.Color - 1]);
                continue;
            }

            var progress = u;
            switch (e.Kind)
            {
                case SlVisualKind.Place:
                    progress = Mathf.SmoothStep(0f, 1f, u);
                    a.Pos = Vector3.Lerp(from, to, progress);
                    break;
                case SlVisualKind.Fall:
                    var frames = e.DurationFrames;
                    var t = u * frames;
                    var v = e.StartSpeed.AsFloat;
                    var g = SixLineLogic.SettleGravity.AsFloat * .5f;
                    progress = (v * t + g * t * t) / (v * frames + g * frames * frames);
                    a.Pos = Vector3.Lerp(from, to, progress);
                    break;
                case SlVisualKind.Roll:
                    var pivot = new Vector2(e.PivotX.AsFloat * bv.Cell, e.PivotY.AsFloat * bv.Cell);
                    var startVector = (Vector2)from - pivot;
                    var endVector = (Vector2)to - pivot;
                    var startAngle = Mathf.Atan2(startVector.y, startVector.x) * Mathf.Rad2Deg;
                    var endAngle = Mathf.Atan2(endVector.y, endVector.x) * Mathf.Rad2Deg;
                    var angle = (startAngle + Mathf.DeltaAngle(startAngle, endAngle) * Mathf.SmoothStep(0f, 1f, u)) * Mathf.Deg2Rad;
                    var radius = Mathf.Lerp(startVector.magnitude, endVector.magnitude, u);
                    a.Pos = new Vector3(pivot.x + Mathf.Cos(angle) * radius, pivot.y + Mathf.Sin(angle) * radius, .1f);
                    break;
                case SlVisualKind.Land:
                    a.Pos = to;
                    var squash = Mathf.Sin(u * Mathf.PI) * .05f;
                    a.Tr.localScale = new Vector3(1f + squash, 1f - squash, 1f) * (bv.Cell * .96f);
                    break;
                case SlVisualKind.Clear:
                    var size = u < .35f ? 1f + u * .4f : (1f - u) / .65f * 1.14f;
                    a.Tr.localScale = Vector3.one * (bv.Cell * .96f * size);
                    a.Mr.sharedMaterial = ViewMats.GetMat(u < .4f ? Color.white : s_palette[a.Color - 1]);
                    break;
            }
            a.Tr.localPosition = a.Pos;
        }
    }
}
