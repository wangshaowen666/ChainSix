/*--------------------------------------------------------------
 * File: BattlePanel.cs
 * Author: Wsw
 * Time: 2026/05/12 14:28:45
 *--------------------------------------------------------------
 */

using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && WEIXINMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 战斗 HUD 面板（TD/VS 共用载体）：
/// - 摇杆：OnOpen 登记 GameJoystick 到 GameMgr.Battle（1-4，经 VsJoystickReady 事件解耦获取）
/// - VS HUD（2-10）：吸血鬼模式下代码构建血条/经验条+等级/倒计时/击杀数；
///   VampireView 逻辑帧驱动 UpdateHud 刷新（选牌暂停停步进，HUD 自然冻结），面板纯渲染
/// </summary>
public class BattlePanel : UIPanelBase
{
    [SerializeField] private TMP_FontAsset _font; // MiSans-Bold SDF（prefab 上拖引用，对齐 LevelUpPanel）

    // ---- VS HUD 元素（EnsureHud 一次构建，面板池化常驻；TD 模式不构建）----
    private RectTransform _hudRoot;    // VSHud 根（跨模式切换时显隐，防 TD 模式残留 VS HUD）
    private RectTransform _hpFill; // 血条前景（锚点宽度 = 血量比例，零 sprite 依赖）
    private TMP_Text _hpText;
    private RectTransform _xpFill; // 经验条前景
    private TMP_Text _levelText;
    private TMP_Text _timeText;
    private TMP_Text _killText;

    // 脏检查缓存（值变化才写，避免每帧 TMP mesh 重建）
    private long _lastHp = -1, _lastMaxHp = -1, _lastXp = -1, _lastXpToNext = -1, _lastKill = -1;
    private int _lastLevel = -1, _lastTimeSec = -1;

    public override void OnOpen(object userData = null)
    {
        base.OnOpen(userData);

        GameMgr.Battle.RegisterJoystick(GetComponentInChildren<GameJoystick>());
        // if (GameMgr.Battle.CurrentBattleView is VampireView)
        //     EnsureHud();
        // else if (_hudRoot != null) // TD 模式：隐藏 VS HUD（面板池化复用，节点常驻）
        //     _hudRoot.gameObject.SetActive(false);
    }

    public override void OnRecycle()
    {
        GameMgr.Battle.ClearJoystick();
        base.OnRecycle();
    }

    /// <summary>
    /// 刷新 VS HUD（VampireView 每逻辑帧调用）：countdown=true 时 timeSec 为剩余时长（倒计时），否则为已存活时长（正计时）。
    /// </summary>
    public void UpdateHud(long hp, long maxHp, int level, long xp, long xpToNext, long killCount, float timeSec, bool countdown)
    {
        if (_hpFill == null)
            EnsureHud(); // OnOpen 时序兜底（正常已在 OnOpen 构建过，此处幂等）

        if (hp != _lastHp || maxHp != _lastMaxHp)
        {
            _lastHp = hp;
            _lastMaxHp = maxHp;
            var ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            _hpFill.anchorMax = new Vector2(ratio, 1f);
            _hpText.text = $"{hp}/{maxHp}";
        }

        if (xp != _lastXp || xpToNext != _lastXpToNext)
        {
            _lastXp = xp;
            _lastXpToNext = xpToNext;
            var ratio = xpToNext > 0 ? Mathf.Clamp01((float)xp / xpToNext) : 0f;
            _xpFill.anchorMax = new Vector2(ratio, 1f);
        }

        if (level != _lastLevel)
        {
            _lastLevel = level;
            _levelText.text = $"Lv.{level}";
        }

        if (killCount != _lastKill)
        {
            _lastKill = killCount;
            _killText.text = $"击杀 {killCount}";
        }

        var sec = Mathf.FloorToInt(timeSec);
        if (sec != _lastTimeSec)
        {
            _lastTimeSec = sec;
            _timeText.text = $"{sec / 60:00}:{sec % 60:00}";
        }
    }

    // ---- HUD 构建（一次构建，池化复用；布局锚点自适应分辨率）----

    private void EnsureHud()
    {
        if (_hpFill != null)
        {
            _hudRoot.gameObject.SetActive(true); // 再次进入 VS：重新显隐 + 重置脏缓存（上局值可能相同但需刷新一致）
            _lastHp = _lastMaxHp = _lastXp = _lastXpToNext = _lastKill = -1;
            _lastLevel = _lastTimeSec = -1;
            return;
        }

        // VSHud 下全是功能元素（无背景），整体收进安全区（避开刘海/状态栏）
        // 注意顺序：先铺满再挂 SafeArea，挂载瞬间即生效，之后再改锚点会覆盖掉安全区内缩
        _hudRoot = new GameObject("VSHud", typeof(RectTransform)).GetComponent<RectTransform>();
        _hudRoot.SetParent(transform, false);
        Stretch(_hudRoot);
        _hudRoot.gameObject.AddComponent<SafeArea>();

        // 左上：血条 + HP 文本
        var hpBarRt = CreateBar(_hudRoot, "HpBar", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(420f, 26f),
            new Color(0.12f, 0.12f, 0.12f, 0.85f), new Color(0.85f, 0.25f, 0.25f, 1f), out _hpFill);
        _hpText = CreateText(hpBarRt, "HpText", 18, Color.white);
        Stretch(_hpText.rectTransform);

        // 血条下方：经验条 + 等级
        var xpBarRt = CreateBar(_hudRoot, "XpBar", new Vector2(0f, 1f), new Vector2(30f, -66f), new Vector2(420f, 16f),
            new Color(0.12f, 0.12f, 0.12f, 0.85f), new Color(0.35f, 0.55f, 1f, 1f), out _xpFill);
        _levelText = CreateText(xpBarRt, "LevelText", 14, Color.white);
        Stretch(_levelText.rectTransform);

        // 顶中：倒计时/存活时长
        _timeText = CreateText(_hudRoot, "TimeText", 36, Color.white);
        var timeRt = _timeText.rectTransform;
        timeRt.anchorMin = timeRt.anchorMax = new Vector2(0.5f, 1f);
        timeRt.sizeDelta = new Vector2(300f, 60f);
        timeRt.anchoredPosition = new Vector2(0f, -35f);

        // 右上：击杀数（微信小游戏：额外下移，避开胶囊按钮——SafeArea 只含系统安全区，不含胶囊）
        _killText = CreateText(_hudRoot, "KillText", 28, Color.white);
        var killRt = _killText.rectTransform;
        killRt.anchorMin = killRt.anchorMax = new Vector2(1f, 1f);
        killRt.pivot = new Vector2(1f, 1f); // pivot 对齐右上角，pos 即边距
        killRt.sizeDelta = new Vector2(300f, 40f);
        killRt.anchoredPosition = new Vector2(-30f, -40f - WeChatCapsuleTopOffset());
        _killText.alignment = TextAlignmentOptions.Right;
    }

    /// <summary>
    /// 微信小游戏：胶囊按钮底边超出安全区顶部的额外避让量（Unity 像素，含 8 逻辑像素边距）。
    /// SafeArea 只处理系统安全区，胶囊在部分机型上低于安全区顶线，击杀数需额外下移；其他平台返回 0
    /// </summary>
    private static float WeChatCapsuleTopOffset()
    {
#if UNITY_WEBGL && WEIXINMINIGAME
        var sys = WX.GetSystemInfoSync();
        var menu = WX.GetMenuButtonBoundingClientRect();
        if (sys?.safeArea == null || menu == null || sys.screenWidth <= 0 || Screen.width <= 0)
            return 0f;

        float scale = Screen.width / (float)sys.screenWidth;
        // 胶囊底边(+8 逻辑像素边距) 与 安全区顶线 的差值，负数说明胶囊在安全区内，无需避让
        return Mathf.Max(0f, (float)((menu.bottom + 8 - sys.safeArea.top) * scale));
#else
        return 0f;
#endif
    }

    /// <summary>创建进度条（底 + 前景）：前景锚点 x = 比例，改 anchorMax 即缩放，零 sprite 依赖</summary>
    private static RectTransform CreateBar(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
        Color bgColor, Color fillColor, out RectTransform fill)
    {
        var bgRt = CreateImage(parent, name, bgColor).rectTransform;
        bgRt.anchorMin = bgRt.anchorMax = anchor;
        bgRt.pivot = anchor; // 点锚定：pivot 对齐锚角，pos 即"距边距"，矩形完整落在屏内
        bgRt.sizeDelta = size;
        bgRt.anchoredPosition = pos;

        fill = CreateImage(bgRt, name + "Fill", fillColor).rectTransform;
        Stretch(fill);
        fill.anchorMax = new Vector2(1f, 1f);

        return bgRt;
    }

    private static Image CreateImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text CreateText(RectTransform parent, string name, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
