using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>BattlePanel 内的灰盒控件；只渲染状态并写入输入源。</summary>
public sealed class SixLineHud
{
    private RectTransform _root;
    private readonly TMP_FontAsset _font;
    private readonly TMP_Text _status;
    private readonly TMP_Text[] _players = new TMP_Text[2];
    private readonly Image[][] _previews = new Image[2][];
    private static Sprite s_previewCircle;
    private readonly RectTransform[] _controls = new RectTransform[2];
    private readonly int[] _lastChain = new int[2];
    private readonly SlTouchInput[] _inputs;
    private readonly TMP_Text _result;
    private readonly TMP_Text[] _chainTexts = new TMP_Text[2];
    private readonly float[] _chainTimes = new float[2];

    public SixLineHud(Transform parent, TMP_FontAsset font, SlTouchInput[] inputs, Action restart, Action exit)
    {
        _font = font != null ? font : TMP_Settings.defaultFontAsset;
        _inputs = inputs;
        _root = Rect(parent, "SixLineHud", Vector2.zero, Vector2.one);
        _root.gameObject.AddComponent<SafeArea>();
        _status = Text(_root, "Status", "", 22, new Vector2(.27f, .95f), new Vector2(.73f, 1f));
        ActionButton(_root, "重开", new Vector2(.02f, .95f), new Vector2(.12f, 1f), restart);
        ActionButton(_root, "退出", new Vector2(.88f, .95f), new Vector2(.98f, 1f), exit);
        _result = Text(_root, "Result", "", 40, new Vector2(.2f, .43f), new Vector2(.8f, .57f));
        for (int i = 0; i < 2; i++)
        {
            var left = i == 0 ? .02f : .52f;
            _players[i] = Text(_root, "P" + i, "", 20, new Vector2(left, .88f), new Vector2(left + .46f, .95f));
            _chainTexts[i] = Text(_root, "Chain" + i, "", 30, new Vector2(left, .71f), new Vector2(left + .46f, .79f));
            var preview = Rect(_root, "Preview" + i, new Vector2(left, .80f), new Vector2(left + .46f, .88f));
            _previews[i] = new Image[3];
            const float diameter = 28f;
            var rowPitch = diameter * SixLineLogic.RowPitch.AsFloat;
            for (int b = 0; b < 3; b++)
            {
                // 用明确的圆直径定位，避免预览宽度和字体字形改变三球间距。
                var rt = Rect(preview, "Ball" + b, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
                rt.sizeDelta = new Vector2(diameter, diameter);
                rt.anchoredPosition = new Vector2(b == 0 ? 0f : b == 1 ? -diameter * .5f : diameter * .5f,
                    b == 0 ? rowPitch * .5f : -rowPitch * .5f);
                var image = rt.gameObject.AddComponent<Image>();
                image.sprite = PreviewCircle();
                image.raycastTarget = false;
                _previews[i][b] = image;
            }

            _controls[i] = Rect(_root, "Controls" + i, new Vector2(left, .015f), new Vector2(left + .46f, .14f));
            var labels = new[] { "左", "右", "顺转", "逆转", "加速" };
            for (int op = 1; op <= SlOp.FastDrop; op++)
                InputButton(_controls[i], labels[op - 1], op, inputs[i]);
            Text(_root, "Keys" + i, i == 0 ? "P0：A/D 移动 · Q/E 旋转 · 空格加速" : "P1：←/→ 移动 · U/O 旋转 · ↓加速", 17,
                new Vector2(left, .145f), new Vector2(left + .46f, .18f));
        }
    }

    public void Refresh(SixLineLogic logic, long seed, bool showResult = true)
    {
        if (_root == null)
            return;
        var sec = logic.LastTickFrame * SixLineLogic.LogicFrameMs / 1000;
        _status.text = $"帧 {logic.LastTickFrame}  {sec / 60:00}:{sec % 60:00}  种子 {seed}";
        for (int i = 0; i < 2; i++)
        {
            var p = logic.Players[i];
            if (p.LockedThisFrame)
                _lastChain[i] = p.LastSettleChain;
            _players[i].text = $"P{i}  最近连锁 {_lastChain[i]}  待投 {p.PendingGarbage}\n攻击 {p.TotalSent}  承受 {p.TotalReceived}";
            for (int c = 0; c < _previews[i].Length; c++)
                _previews[i][c].color = SixLineView.PieceColor(p.PreviewColors[c]);
            _controls[i].gameObject.SetActive(!logic.GameOver);
        }
        _result.text = !logic.GameOver || !showResult ? "" : logic.WinnerIndex < 0 ? "平局" : $"P{logic.WinnerIndex} 获胜";
    }

    public void ShowChain(int player, int chain, bool special)
    {
        if (_root == null)
            return;
        _chainTexts[player].text = special ? $"同色全消 · {chain} 连锁" : chain > 1 ? $"{chain} 连锁" : "六连消除";
        _chainTimes[player] = .7f;
    }

    public void UpdateEffects(float dt)
    {
        if (_root == null)
            return;
        for (int i = 0; i < 2; i++)
        {
            _chainTimes[i] = Mathf.Max(0f, _chainTimes[i] - dt);
            var text = _chainTexts[i];
            text.color = new Color(1f, .9f, .45f, Mathf.Clamp01(_chainTimes[i] / .25f));
            text.rectTransform.localScale = Vector3.one * (1f + .12f * Mathf.Clamp01((_chainTimes[i] - .55f) / .15f));
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _inputs.Length; i++)
            _inputs[i].Reset();
        if (_root == null)
            return;
        _root.gameObject.SetActive(false);
        Object.Destroy(_root.gameObject);
        _root = null;
    }

    private static Sprite PreviewCircle()
    {
        if (s_previewCircle != null)
            return s_previewCircle;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "SixLinePreviewCircle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var dx = x + .5f - size * .5f;
                var dy = y + .5f - size * .5f;
                var alpha = Mathf.Clamp01(size * .5f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        s_previewCircle = Sprite.Create(texture, new UnityEngine.Rect(0, 0, size, size), new Vector2(.5f, .5f),
            100f, 0, SpriteMeshType.FullRect);
        s_previewCircle.name = "SixLinePreviewCircle";
        return s_previewCircle;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private TMP_Text Text(Transform parent, string name, string value, float size, Vector2 min, Vector2 max)
    {
        var rt = Rect(parent, name, min, max);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private RectTransform ButtonRect(Transform parent, string label, Vector2 min, Vector2 max)
    {
        var rt = Rect(parent, label, min, max);
        rt.gameObject.AddComponent<Image>().color = new Color(.19f, .24f, .33f, .95f);
        Text(rt, "Label", label, 26, Vector2.zero, Vector2.one);
        return rt;
    }

    private void ActionButton(Transform parent, string label, Vector2 min, Vector2 max, Action action)
    {
        var rt = ButtonRect(parent, label, min, max);
        rt.gameObject.AddComponent<Button>().onClick.AddListener(() => action());
    }

    private void InputButton(Transform parent, string label, int op, SlTouchInput input)
    {
        var rt = ButtonRect(parent, label, new Vector2((op - 1) * .2f + .005f, 0), new Vector2(op * .2f - .005f, 1));
        var trigger = rt.gameObject.AddComponent<EventTrigger>();
        var pointers = new HashSet<int>();
        AddTrigger(trigger, EventTriggerType.PointerDown, data =>
        {
            pointers.Add(((PointerEventData)data).pointerId);
            input.Press(op);
        });
        Action<BaseEventData> release = data =>
        {
            pointers.Remove(((PointerEventData)data).pointerId);
            if (pointers.Count == 0)
                input.Release(op);
        };
        AddTrigger(trigger, EventTriggerType.PointerUp, release);
        AddTrigger(trigger, EventTriggerType.PointerExit, release);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => action(data));
        trigger.triggers.Add(entry);
    }
}
