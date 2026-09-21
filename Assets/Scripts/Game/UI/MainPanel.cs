/*--------------------------------------------------------------
 * File: MainPanel.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/31 18:43:16
 *--------------------------------------------------------------
 */

using cfg;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主界面（灰盒）：prefab 只保留根节点 + 脚本，UI 全部代码构建（无美术资源依赖）。
/// 开始按钮 -> ProcedureExitMain 事件 -> ProcedureMain 切 Battle 场景（事件解耦，面板不感知流程）
/// </summary>
public class MainPanel : UIPanelBase
{
    private Button _btnStart;

    public override void OnInit(DUIPanel cfg)
    {
        base.OnInit(cfg);
        BuildUi(); // OnInit 仅在实例化后调用一次（面板池化常驻），UI 只构建一次
        _btnStart.onClick.AddListener(OnClickStart);
    }

    /// <summary>代码构建 UI：标题（顶部居中）+ 开始按钮（底部居中）</summary>
    private void BuildUi()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var title = CreateText(transform, "Title", "六连珠", 90, Color.white);
        var titleRt = (RectTransform)title.transform;
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(800f, 140f);
        titleRt.anchoredPosition = new Vector2(0f, -320f);

        var btnGo = new GameObject("BtnStart", typeof(RectTransform), typeof(Image), typeof(Button));
        var btnRt = (RectTransform)btnGo.transform;
        btnRt.SetParent(transform, false);
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.sizeDelta = new Vector2(640f, 180f);
        btnRt.anchoredPosition = new Vector2(0f, 420f);
        btnGo.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.95f, 1f);

        var btnText = CreateText(btnRt, "Text", "进入对战", 64, Color.white);
        Stretch((RectTransform)btnText.transform);

        _btnStart = btnGo.GetComponent<Button>();
    }

    private Text CreateText(Transform parent, string name, string content, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = (int)fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
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

    private void OnClickStart()
    {
        GameMgr.Event.Send(GameEvent.ProcedureExitMain);
        GameMgr.UI.PanelOff(this);
    }
}
