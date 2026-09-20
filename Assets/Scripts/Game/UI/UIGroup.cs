/*--------------------------------------------------------------
 * File: UIGroup.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/08 10:13:06 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using cfg;
using UnityEngine;

public class UIGroup
{
    private readonly LinkedList<UIPanelBase> _uiPanels;
    public Transform Trans { get; }

    public UIGroup(Transform transform)
    {
        _uiPanels = new LinkedList<UIPanelBase>();
        Trans = transform;
    }

    public void AddPanel(UIPanelBase panel, object userData = null)
    {
        panel.OnOpen(userData);
        _uiPanels.AddFirst(panel);
        Refresh();
    }

    public void RemovePanel(UIPanelBase panel)
    {
        _uiPanels.Remove(panel);
        Refresh();
        panel.OnRecycle();
    }

    public void RemoveAll()
    {
        var pool = CoreMgr.ObjectPool.GetPool<UIPanelBase>();
        var panelNode = _uiPanels.First;
        while (panelNode != null)
        {
            var panel = panelNode.Value;
            panel.OnRecycle();
            pool.PutObj(panel.PnlId.ToString(), panel);
            
            panelNode = panelNode.Next;
        }
        
        _uiPanels.Clear();
    }
    
    public void MoveToTop(UIPanelBase panel, object userData = null)
    {
        panel.OnOpen(userData);
        panel.transform.SetSiblingIndex(Trans.childCount - 1);
        _uiPanels.Remove(panel);
        _uiPanels.AddFirst(panel);
        Refresh();
    }
    
    public UIPanelBase HasPanel(DPnlId id)
    {
        var panel = _uiPanels.First;
        while (panel != null)
        {
            if (panel.Value.PnlId == id)
                return panel.Value;
            
            panel = panel.Next;
        }
        
        return null;
    }

    private void Refresh()
    {
        var current = _uiPanels.First;
        // 整组隐藏功能
        //bool hide = m_Hide;
        bool hide = false;
        bool cover = false;
        while (current != null && current.Value != null)
        {
            if (hide)
            {
                if (current.Value.State != PanelState.Hide)
                {
                    current.Value.OnHide();
                }
            }
            else
            {
                if (!cover)
                {
                    if (current.Value.State != PanelState.Active)
                    {
                        current.Value.OnResume();
                    }

                    if (current.Value.IsHideCovered)
                    {
                        hide = true;
                    }
                    else
                    {
                        cover = true;
                    }
                }
                else
                {
                    if (current.Value.State != PanelState.Cover)
                    {
                        current.Value.OnCover();
                    }
                }
            }

            current = current.Next;
        }
    }
}