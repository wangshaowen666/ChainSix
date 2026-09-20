/*--------------------------------------------------------------
 * File: GameJoystick.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/08/31 11:23:55 
 *--------------------------------------------------------------
 */

using UnityEngine;
using UnityEngine.EventSystems;

public class GameJoystick : Joystick
{
    private Vector3 _initPos;
    protected override void Start()
    {
        base.Start();
        _initPos = background.anchoredPosition;
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        // 首次按下时 cam 尚未在 OnDrag 中赋值，需要先更新，否则坐标换算会出错导致背景飞出屏幕
        UpdateCamera();
        background.anchoredPosition = ScreenPointToAnchoredPosition(eventData.position);
        base.OnPointerDown(eventData);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        background.anchoredPosition = _initPos;
    }
}