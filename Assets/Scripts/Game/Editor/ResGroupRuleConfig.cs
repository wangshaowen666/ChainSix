/*--------------------------------------------------------------
 * File: ResGroupRuleConfig.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/20
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 资源分组规则表：声明 Assets/Res 下资源应落入的 Addressables 组、地址与 Label。
/// 规则表是分组结果的唯一事实来源，由 ResGroupSyncer 幂等同步到 Addressables 配置。
///
/// 运行时协议依赖（勿随意变更，改动会导致已发布内容失效）：
/// - 组名 Remote_ 前缀：远程 bundle 标识，被 AddressableHelper 缓存清理、ToolBox 构建清理依赖
/// - 地址：固定为「文件名去扩展名」，与运行时按名加载约定一致（如 Game.dll.bytes -> Game.dll）
/// - Label：DataTableMgr 按 datatable 预载配置表；AddressableHelper 按 common/lua 交集做登录预下载
/// </summary>
public class ResGroupRuleConfig : ScriptableObject
{
    /// <summary>目录规则：目录(含子目录)下所有资源按规则入组；开启子文件夹即组时，每个直接子文件夹为独立组</summary>
    [Serializable]
    public class DirRule
    {
        [LabelText("资源目录")]
        [FolderPath(RequireExistingPath = true)]
        public string dir;
        [LabelText("子文件夹即组名")] public bool subfolderAsGroup;
        [HideIf("subfolderAsGroup")]
        [LabelText("目标组名")] public string group;
        [ShowIf("subfolderAsGroup")]
        [LabelText("组名前缀(空=直接用子文件夹名)")] public string groupNamePrefix;
        [LabelText("仅限扩展名(空=全部)")] public string extensions;
        [LabelText("Label(逗号分隔)")] public string labels;
    }

    /// <summary>例外规则：对单个资源覆盖目录规则，或管理规则目录外的资源</summary>
    [Serializable]
    public class ExceptionRule
    {
        [LabelText("资源")] public UnityEngine.Object asset;
        [LabelText("移出管理(不设为可寻址)")] public bool unmanage;
        [LabelText("覆盖组(空=按目录规则)")] public string groupOverride;
        [LabelText("覆盖地址(空=文件名)")] public string addressOverride;
        [LabelText("覆盖Label(逗号分隔)")] public string labelsOverride;
    }

    [LabelText("目录规则")]
    [ListDrawerSettings(DraggableItems = true, ShowItemCount = true)]
    public List<DirRule> dirRules = new List<DirRule>();

    [LabelText("例外规则")]
    [TableList(AlwaysExpanded = true)]
    public List<ExceptionRule> exceptions = new List<ExceptionRule>();

    [LabelText("忽略路径前缀(不参与同步与覆盖检查)")]
    public List<string> ignorePaths = new List<string>();

    [LabelText("不警告多余条目的组(其中的条目仍会被同步归位)")]
    public List<string> unmanagedGroups = new List<string> { BuiltInGroupName, ContentUpdateGroupPrefix };

    [LabelText("同步后检查重复打包依赖")]
    public bool checkDuplicateDeps = true;

    public const string AssetPath = "Assets/Scripts/Game/Editor/ResGroupRuleConfig.asset";
    public const string BuiltInGroupName = "Built In Data";
    /// <summary>热更构建自动生成的临时组名前缀，须与 ToolBox.BuildContentUpdate 保持一致</summary>
    public const string ContentUpdateGroupPrefix = "Remote_ContentUpdate";

    /// <summary>加载规则表：优先按类型查找（资产位置可移动），不存在时在默认路径创建并写入初始规则</summary>
    public static ResGroupRuleConfig LoadOrCreate()
    {
        string[] guids = AssetDatabase.FindAssets("t:ResGroupRuleConfig");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<ResGroupRuleConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

        var config = CreateInstance<ResGroupRuleConfig>();
        config.InitDefaultRules();
        AssetDatabase.CreateAsset(config, AssetPath);
        AssetDatabase.SaveAssets();
        return config;
    }

    /// <summary>
    /// ChainSix 初始规则（与现有运行时加载约定一致）
    /// </summary>
    private void InitDefaultRules()
    {
        dirRules.Add(new DirRule
        {
            dir = "Assets/Res/Dll",
            group = "Remote_GameDll",
            labels = "",   // 按需加载：ProcedureLoadDll 按地址拉取，登录预下载暂不覆盖
        });
        dirRules.Add(new DirRule
        {
            dir = "Assets/Res/LubanData/Bin",
            group = "Remote_DataTable",
            labels = "datatable",   // 预载：DataTableMgr.PreloadTableBytesAsync 按 label 加载
        });

        // 开发用 JSON 不进 bundle
        ignorePaths.Add("Assets/Res/LubanData/Json");
        // 纯编辑器配置
        ignorePaths.Add("Assets/Res/Config/EditorPathConfig.asset");
    }
}
