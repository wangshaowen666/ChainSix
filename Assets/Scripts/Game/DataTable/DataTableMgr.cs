/*--------------------------------------------------------------
 * File: DataTableCtr.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/07 13:27:28 
 *--------------------------------------------------------------
 */

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using cfg;
using Luban;
using UnityEngine;

public class DataTableMgr : ManagerBase
{
    private const string TableLabel = "datatable";
    
    private readonly Dictionary<string, byte[]> _preloadedTableBytes = new Dictionary<string, byte[]>();
    private bool _isLoaded = false;
    /// <summary>
    /// 检查数据表是否已加载
    /// </summary>
    public bool IsLoaded => _isLoaded;
    
    // 导表工具自动补全下列属性
    private cfg.Tables _tables;
    public TbEntity TbEntity => _tables.TbEntity;
    public TbUIPanel TbUIPanel => _tables.TbUIPanel;

    public void LoadTable()
    {
        if (_isLoaded)
        {
            Log.Warning("数据表已经加载，请勿重复加载");
            return;
        }
        
        _tables = new cfg.Tables(LoadByteBuf);
        _isLoaded = true;
    }
    
    /// <summary>
    /// 重新加载数据表
    /// </summary>
    public void ReloadTable()
    {
        _tables = null;
        _isLoaded = false;
        LoadTable();
    }

    /// <summary>
    /// 异步预载所有配置表bytes（按标签统一加载，无需维护文件列表；键为 TextAsset.name，与 Luban loader 的文件名一致）。
    /// 必须在 LoadTable 之前完成：WebGL/小游戏无同步加载能力，未预载的表直接报错
    /// </summary>
    public async UniTask PreloadTableBytesAsync()
    {
        var assets = await CoreMgr.Res.PreloadWithLabel<TextAsset>(TableLabel);
        if (assets == null)
        {
            Log.Error($"配置表预载失败: {TableLabel}");
            return;
        }

        foreach (var ta in assets)
            _preloadedTableBytes[ta.name] = ta.bytes;
    }

    private ByteBuf LoadByteBuf(string file)
    {
        if (_preloadedTableBytes.TryGetValue(file, out var bytes))
            return new ByteBuf(bytes);

        throw new System.Exception($"配置表bytes未预载: {file}（检查资产是否打上 {TableLabel} 标签，且 PreloadTableBytesAsync 先于 LoadTable 执行）");
    }
}