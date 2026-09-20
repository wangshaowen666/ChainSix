/*--------------------------------------------------------------
 * File: SafeArea.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/07
 *--------------------------------------------------------------
 */

using UnityEngine;
#if UNITY_WEBGL && WEIXINMINIGAME
using WeChatWASM;
#endif

/// <summary>
/// 安全区适配：把所在节点的矩形收进安全区。
/// 原生平台（APK 等）用 Screen.safeArea；微信小游戏 WebGL 拿不到系统刘海/手势区信息
/// （Screen.safeArea 恒为全屏），改从微信桥 getSystemInfoSync().safeArea 换算。
/// 挂在面板 prefab 的功能层子节点上（背景层不要挂），子 UI 相对它做锚点布局。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeArea : MonoBehaviour
{
    private RectTransform _rt;
    private Rect _lastSafeArea;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 仅编辑器需要：拖窗口、Device Simulator 切机型时 safeArea 会变
        if (Screen.safeArea != _lastSafeArea)
        {
            Apply();
        }
    }
#endif

    private void Apply()
    {
        var sa = GetSafeArea();
        if (sa.width <= 0 || sa.height <= 0)
            return;

        _lastSafeArea = sa;

        var screenW = Screen.width;
        var screenH = Screen.height;

        _rt.anchorMin = new Vector2(sa.x / screenW, sa.y / screenH);
        _rt.anchorMax = new Vector2((sa.x + sa.width) / screenW, (sa.y + sa.height) / screenH);
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }

    /// <summary>取安全区（Unity 像素，左下原点）</summary>
    private static Rect GetSafeArea()
    {
#if UNITY_WEBGL && WEIXINMINIGAME
        // 微信小游戏：Screen.safeArea 恒为全屏，从微信桥读取安全区（wx 逻辑像素，左上原点）
        // 换算比例 = Unity 画布像素 / 微信逻辑像素
        var sys = WX.GetSystemInfoSync();
        if (sys?.safeArea != null && sys.screenWidth > 0 && Screen.width > 0)
        {
            float scale = Screen.width / (float)sys.screenWidth;
            return new Rect(
                (float)(sys.safeArea.left * scale),
                (float)((sys.screenHeight - sys.safeArea.bottom) * scale),
                (float)((sys.safeArea.right - sys.safeArea.left) * scale),
                (float)((sys.safeArea.bottom - sys.safeArea.top) * scale));
        }

        return new Rect(0, 0, Screen.width, Screen.height);
#else
        return Screen.safeArea;
#endif
    }
}
