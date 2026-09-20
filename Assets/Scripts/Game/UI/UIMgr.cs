/*--------------------------------------------------------------
 * File: PanelCtr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/18 14:42:43 
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using cfg;
using UnityEngine;
using Object = UnityEngine.Object;

public class UIMgr : ManagerBase
{
    private const string UILayerName = "UI";
    
    private readonly Dictionary<DUIGroup, UIGroup> _layerGroupMap = new();
    // 正在加载中的界面，如果还没加载完就被关闭了，value会被置为0
    private readonly Dictionary<uint, DPnlId> _loadingPanelIdMap = new();
    private readonly int _uiLayerId = LayerMask.NameToLayer(UILayerName);
    
    private Transform _uiRoot;
    private ObjectPool<UIPanelBase> _uiPanelPool = CoreMgr.ObjectPool.RegisterPool<UIPanelBase>(5);

    public override void OnInit()
    {
        base.OnInit();
        _uiRoot = GameObject.Find(LaunchConfig.UIRoot).transform;
    }

    public void PanelOn(DPnlId id, object userData = null)
    {
        var cfg = GameMgr.DataTable.TbUIPanel[id];
        if (cfg == null)
        {
            Log.Error("不存在的界面：" + id);
            return;
        }

        var g = cfg.Group;
        if (!_layerGroupMap.TryGetValue(g, out var group))
        {
            group = CreateUIGroup(g);
        }
        
        if (!cfg.AllowMult)
        {
            if (_loadingPanelIdMap.ContainsValue(cfg.Id))
                return;

            var pnl = HasPanel(cfg.Id);
            if (pnl != null && pnl.State != PanelState.Active)
            {
                group.MoveToTop(pnl, userData);
                return;
            }
        }

        uint loadingId = AutoID.GetID();
        UIPanelBase panel = _uiPanelPool.GetObj(id.ToString());
        if (panel == null)
        {
            _loadingPanelIdMap.Add(loadingId, id);
            LoadPanelAsync(loadingId, cfg, group, userData).Forget();
        }
        else
        {
            group.AddPanel(panel, userData);
        }
    }
    
    // 幂等关闭，允许多次调用
    public void PanelOff(DPnlId id)
    {
        var panel = HasPanel(id);
        if (panel != null)
        {
            PanelOff(panel);
            return;
        }

        foreach (var kv in _loadingPanelIdMap)
        {
            if (kv.Value == id)
            {
                _loadingPanelIdMap[kv.Key] = 0;
                return;
            }
        }

        Log.Info("正在关闭不存在的界面：", id);
    }

    public void PanelOff(UIPanelBase panel)
    {
        var cfg = GameMgr.DataTable.TbUIPanel[panel.PnlId];
        _uiPanelPool.PutObj(panel.PnlId.ToString(), panel);
        
        var group = _layerGroupMap[cfg.Group];
        group.RemovePanel(panel);
    }

    public void PanelOffAll()
    {
        foreach (var kv in _loadingPanelIdMap)
        {
            _loadingPanelIdMap[kv.Key] = 0;
        }

        foreach (var kv in _layerGroupMap)
        {
            kv.Value.RemoveAll();
        }
    }

    public UIPanelBase HasPanel(DPnlId id)
    {
        var cfg = GameMgr.DataTable.TbUIPanel[id];
        // 容错还未创建的Group，直接拿id判断存在了
        if (_layerGroupMap.TryGetValue(cfg.Group, out var group))
            return group.HasPanel(id);
        
        return null;
    }
    
    private async UniTaskVoid LoadPanelAsync(uint loadingId, DUIPanel cfg, UIGroup group, object userData)
    {
        var obj = await CoreMgr.Res.LoadAsync<GameObject>(cfg.Name);
        _loadingPanelIdMap.Remove(loadingId, out var pnlId);
        // 加载的过程中被关闭了
        if (pnlId == 0)
        {
            // todo 这里加载到内存，但没有实例化，卸载的时候要注意
            return;
        }

        var prefab = Object.Instantiate(obj, group.Trans);
        var panel = prefab.GetComponent<UIPanelBase>();
        if (panel == null)
            throw new System.Exception("预制体上缺少UIPanelBase脚本");

        panel.OnInit(cfg);
        group.AddPanel(panel, userData);
    }

    private UIGroup CreateUIGroup(DUIGroup groupId)
    {
        var obj = new GameObject
        {
            name = groupId.ToString(),
            layer = _uiLayerId
        };

        var tr = obj.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        tr.SetParent(_uiRoot, false);
        tr.SetSiblingIndex((int)groupId - 1);
        
        var group = new UIGroup(tr);
        _layerGroupMap.Add(groupId, group);
        return group;
    }
}