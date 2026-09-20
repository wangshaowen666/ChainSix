/*--------------------------------------------------------------
 * File: UIPanelBase.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/31 18:48:58 
 *--------------------------------------------------------------
 */

using cfg;
using UnityEngine;
using DG.Tweening;

public enum PanelState
{
    None,
    Active,
    Hide,
    Cover,
    Recycle,
}

[RequireComponent(typeof(CanvasGroup))]
public class UIPanelBase : MonoBehaviour
{
    public PanelState State { get; private set; }
    public DPnlId PnlId => _cfg.Id;
    public bool IsHideCovered => _cfg.HideCovered;

    protected object UserData;
    
    private const float FadeTime = 0.3f;
    
    private CanvasGroup _canvasGroup;
    private DUIPanel _cfg;

    public virtual void OnInit(DUIPanel cfg)
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _cfg = cfg;
    }

    public virtual void OnOpen(object userData = null)
    {
        _canvasGroup.alpha = 0;
        _canvasGroup.DOFade(1, FadeTime);
        _canvasGroup.blocksRaycasts = true;
        
        State = PanelState.Active;
        UserData = userData;
    }

    public virtual void OnHide()
    {
        _canvasGroup.alpha = 0;
        _canvasGroup.blocksRaycasts = false;
        State = PanelState.Hide;
    }

    public virtual void OnCover()
    {
        _canvasGroup.blocksRaycasts = false;
        State = PanelState.Cover;
    }
    
    public virtual void OnResume()
    {
        switch (State)
        {
            case PanelState.Hide:
                _canvasGroup.DOFade(1, FadeTime);
                break;
        }
        
        _canvasGroup.blocksRaycasts = true;
        State = PanelState.Active;
    }

    public virtual void OnRecycle()
    {
        State = PanelState.Recycle;
        _canvasGroup.DOFade(0, FadeTime);
        _canvasGroup.blocksRaycasts = false;

        UserData = null;
        State = PanelState.None;
    }

    public virtual void OnClose()
    {
        
    }
}