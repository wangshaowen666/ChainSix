/*--------------------------------------------------------------
 * File: ResGroupSyncer.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/09/20
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// 资源分组同步器：依据 ResGroupRuleConfig 把 Addressables 配置对齐到规则表（幂等，重复执行无副作用）。
/// 职责：
/// - 一次性初始化 Addressables 全局设置（远程 Catalog 等）
/// - 建组 / 入组 / 改地址 / 改 Label / 清死条目 / 组 schema 对齐统一模板
/// - 归位热更构建遗留在 Remote_ContentUpdate 组中的条目（兼容保留：仅在组开启 Prevent Updates 时
///   热更流程才会把变更条目移入该组；不归位会导致这些条目永远无法热更）
/// - 校验：地址重名（阻断）、Res 下未被规则覆盖的资源（警告）、组内多余条目（警告）
/// - 重复打包依赖检查（Analyze 规则）：同一非条目资源被重复打进多个 bundle 时列出清单（警告）
/// </summary>
public static class ResGroupSyncer
{
    private const string ResRoot = "Assets/Res";
    private const string BuiltInGroupName = "Built In Data";
    /// <summary>与 ResGroupRuleConfig.ContentUpdateGroupPrefix / ToolBox.BuildContentUpdate 保持一致</summary>
    private const string ContentUpdateGroupPrefix = "Remote_ContentUpdate";

    public class SyncReport
    {
        public readonly List<string> infos = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> errors = new List<string>();
        public string summary = "";

        public bool success => errors.Count == 0;
    }

    private class DesiredEntry
    {
        public string guid;
        public string assetPath;
        public string group;
        public string address;
        public List<string> labels;
    }

    /// <summary>
    /// 执行同步并逐行输出报告，返回是否成功（无阻断性错误）
    /// </summary>
    public static bool SyncAndReport(Action<string> log)
    {
        SyncReport report = Sync();
        foreach (string line in report.infos) log(line);
        foreach (string line in report.warnings) log("警告: " + line);
        foreach (string line in report.errors) log("错误: " + line);
        log(report.summary);
        return report.success;
    }

    /// <summary>
    /// 执行同步。规则表本身有错（缺组名/重名地址等）时不做任何修改直接返回
    /// </summary>
    public static SyncReport Sync()
    {
        var report = new SyncReport();
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            report.errors.Add("未找到 AddressableAssetSettings");
            return report;
        }

        ResGroupRuleConfig config = ResGroupRuleConfig.LoadOrCreate();

        bool settingsInitialized = EnsureGlobalSettings(settings, report);

        // 1. 计算期望状态（规则有错则中止，不做修改）
        var desiredByGuid = new Dictionary<string, DesiredEntry>();
        CollectDesiredState(config, desiredByGuid, report);
        if (report.errors.Count > 0)
        {
            report.summary = "同步中止: 规则表存在错误，未做任何修改";
            return report;
        }

        // 2. 地址重名校验（地址即运行时加载协议，冲突必须报错）
        CheckDuplicateAddresses(desiredByGuid, report);
        if (report.errors.Count > 0)
        {
            report.summary = "同步中止: 存在重名地址，未做任何修改";
            return report;
        }

        // 3. 注册 Label、确保组存在
        var groupCache = new Dictionary<string, AddressableAssetGroup>();
        foreach (string label in desiredByGuid.Values.SelectMany(d => d.labels).Distinct().ToList())
            settings.AddLabel(label, false);

        int groupsTouched = 0;
        foreach (string groupName in desiredByGuid.Values.Select(d => d.group).Distinct().ToList())
        {
            if (EnsureGroup(settings, groupName, report, groupCache))
                groupsTouched++;
        }

        int added = 0, moved = 0, addressChanged = 0, labelChanged = 0, deadRemoved = 0, groupRemoved = 0;

        // 4. 应用期望状态
        foreach (DesiredEntry desired in desiredByGuid.Values)
        {
            AddressableAssetEntry entry = settings.FindAssetEntry(desired.guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(desired.guid, groupCache[desired.group], false, false);
                added++;
            }
            else if (entry.parentGroup == null || entry.parentGroup.Name != desired.group)
            {
                entry = settings.CreateOrMoveEntry(desired.guid, groupCache[desired.group], false, false);
                moved++;
            }

            if (entry.address != desired.address)
            {
                entry.SetAddress(desired.address, false);
                addressChanged++;
            }

            if (SyncLabels(entry, desired.labels))
                labelChanged++;
        }

        // 5. 清死条目、多余条目检查
        var emptyContentUpdateGroups = new List<AddressableAssetGroup>();
        foreach (AddressableAssetGroup group in settings.groups.ToList())
        {
            if (group == null || group.Name == BuiltInGroupName)
                continue;

            bool isContentUpdateGroup = IsContentUpdateGroup(group.Name);

            foreach (AddressableAssetEntry entry in group.entries.ToList())
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    settings.RemoveAssetEntry(entry.guid, false);
                    deadRemoved++;
                    report.infos.Add($"清理死条目: {entry.address} ({group.Name})");
                    continue;
                }

                // 热更临时组里的条目已在上面归位；此处兜底：剩余条目不警告（临时组是构建自动产物）
                if (!desiredByGuid.ContainsKey(entry.guid) && !isContentUpdateGroup
                    && !config.unmanagedGroups.Contains(group.Name))
                {
                    report.warnings.Add($"组内多余条目(未命中规则，可登记例外表): {entry.address} -> {assetPath} ({group.Name})");
                }
            }

            // 清空的热更临时组直接删掉，避免 FindUniqueGroupName 产生 (1)(2) 副本
            if (isContentUpdateGroup && group.entries.Count == 0)
                emptyContentUpdateGroups.Add(group);
        }
        foreach (AddressableAssetGroup group in emptyContentUpdateGroups)
        {
            settings.RemoveGroup(group);
            groupRemoved++;
        }

        // 6. Res 下未被规则覆盖的资源检查
        foreach (string guid in AssetDatabase.FindAssets("", new[] { ResRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) || IsIgnored(config, path))
                continue;
            if (!desiredByGuid.ContainsKey(guid))
                report.warnings.Add($"资源未被规则覆盖: {path}");
        }

        // 7. 重复打包依赖检查（Analyze 规则，内部跑一次只计算不落盘的构建Pass，大工程耗时明显）
        if (config.checkDuplicateDeps && desiredByGuid.Count > 0)
            CheckDuplicateBundleDependencies(settings, report);

        bool changed = added + moved + addressChanged + labelChanged + deadRemoved + groupRemoved + groupsTouched > 0
            || settingsInitialized;
        if (changed)
        {
            // 广播一次批量修改事件，让 Addressables Groups 窗口立即重绘（同步内部走免事件模式，窗口不会自行刷新）
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, false);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        report.summary = $"同步完成: 新增{added} 移组{moved} 改地址{addressChanged} 改Label{labelChanged} " +
                         $"清死条目{deadRemoved} 删组{groupRemoved} | 错误{report.errors.Count} 警告{report.warnings.Count}";
        return report;
    }

    /// <summary>
    /// 一次性初始化 Addressables 全局设置（幂等）：
    /// 远程 Catalog 构建是热更链路(BuildContentUpdate)的硬性要求，路径从已有的 Remote 变量派生
    /// </summary>
    private static bool EnsureGlobalSettings(AddressableAssetSettings settings, SyncReport report)
    {
        if (settings.BuildRemoteCatalog)
            return false;

        // 变量已存在时 CreateValue 原样返回，重复调用安全
        settings.profileSettings.CreateValue("CatalogRemoteBuildPath", "{Remote.BuildPath}/catalog");
        settings.profileSettings.CreateValue("CatalogRemoteLoadPath", "{Remote.LoadPath}/catalog");
        settings.RemoteCatalogBuildPath.SetVariableByName(settings, "CatalogRemoteBuildPath");
        settings.RemoteCatalogLoadPath.SetVariableByName(settings, "CatalogRemoteLoadPath");
        settings.BuildRemoteCatalog = true;
        // 客户端由 AddressableHelper 手动检查/更新 catalog，关掉启动时自动检查
        settings.DisableCatalogUpdateOnStartup = true;
        settings.CatalogRequestsTimeout = 5;
        report.infos.Add("初始化 Addressables 设置: 远程Catalog路径({Remote.BuildPath}/{Remote.LoadPath}/catalog)、关闭启动自动更新");
        return true;
    }

    /// <summary>
    /// 依据规则表与例外表计算期望状态
    /// </summary>
    private static void CollectDesiredState(ResGroupRuleConfig config, Dictionary<string, DesiredEntry> result, SyncReport report)
    {
        // 解析例外表（未命中目录规则的例外资源后面单独处理）
        var exceptionByGuid = new Dictionary<string, ResGroupRuleConfig.ExceptionRule>();
        foreach (ResGroupRuleConfig.ExceptionRule ex in config.exceptions)
        {
            if (ex.asset == null)
            {
                report.errors.Add("例外规则存在空资源引用");
                continue;
            }

            string path = NormalizePath(AssetDatabase.GetAssetPath(ex.asset));
            if (string.IsNullOrEmpty(path) || !path.StartsWith(ResRoot + "/", StringComparison.Ordinal))
            {
                report.errors.Add($"例外资源必须在 {ResRoot} 下: {ex.asset.name}");
                continue;
            }

            exceptionByGuid[AssetDatabase.AssetPathToGUID(path)] = ex;
        }

        foreach (ResGroupRuleConfig.DirRule rule in config.dirRules)
        {
            string dir = NormalizePath(rule.dir);
            if (string.IsNullOrEmpty(dir) || !dir.StartsWith(ResRoot + "/", StringComparison.Ordinal))
            {
                report.errors.Add($"目录规则必须在 {ResRoot} 下: {rule.dir}");
                continue;
            }

            if (!rule.subfolderAsGroup && string.IsNullOrEmpty(rule.group))
            {
                report.errors.Add($"目录规则未填目标组: {dir}");
                continue;
            }

            if (!AssetDatabase.IsValidFolder(dir))
            {
                report.warnings.Add($"目录规则路径不存在: {dir}");
                continue;
            }

            HashSet<string> exts = ParseCsv(rule.extensions).Select(e => NormalizeExtension(e)).ToHashSet();
            if (rule.subfolderAsGroup)
            {
                // 模式规则：每个直接子文件夹一个独立组（组名 = 前缀 + 子文件夹名），新增子文件夹零配置自动成组
                foreach (string subDir in AssetDatabase.GetSubFolders(dir))
                {
                    string subName = Path.GetFileName(subDir.Replace('\\', '/').TrimEnd('/'));
                    CollectFolderAssets(subDir, rule.groupNamePrefix + subName, rule, exts, config, exceptionByGuid, result, report);
                }
            }
            else
            {
                CollectFolderAssets(dir, rule.group, rule, exts, config, exceptionByGuid, result, report);
            }
        }

        // 未命中目录规则的例外资源独立生效
        foreach (ResGroupRuleConfig.ExceptionRule ex in exceptionByGuid.Values)
        {
            if (ex.unmanage)
                continue;

            string path = NormalizePath(AssetDatabase.GetAssetPath(ex.asset));
            if (IsIgnored(config, path))
                continue;

            if (string.IsNullOrEmpty(ex.groupOverride))
            {
                report.errors.Add($"例外资源未命中目录规则且未指定组: {path}");
                continue;
            }

            string address = string.IsNullOrEmpty(ex.addressOverride) ? DefaultAddress(path) : ex.addressOverride.Trim();
            AddDesired(result, AssetDatabase.AssetPathToGUID(path), path, ex.groupOverride, address,
                ParseCsv(ex.labelsOverride), report);
        }
    }

    /// <summary>
    /// 收集单个目录（含子目录）下所有资源到期望状态，例外规则在此逐资源生效
    /// </summary>
    private static void CollectFolderAssets(string folder, string group, ResGroupRuleConfig.DirRule rule,
        HashSet<string> exts, ResGroupRuleConfig config,
        Dictionary<string, ResGroupRuleConfig.ExceptionRule> exceptionByGuid,
        Dictionary<string, DesiredEntry> result, SyncReport report)
    {
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string path = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
            if (AssetDatabase.IsValidFolder(path) || IsIgnored(config, path))
                continue;

            if (exts.Count > 0 && !exts.Contains(Path.GetExtension(path).ToLowerInvariant()))
                continue;

            string address = DefaultAddress(path);
            List<string> labels = ParseCsv(rule.labels);
            string targetGroup = group;

            if (exceptionByGuid.TryGetValue(guid, out ResGroupRuleConfig.ExceptionRule ex))
            {
                exceptionByGuid.Remove(guid);
                if (ex.unmanage)
                    continue;
                if (!string.IsNullOrEmpty(ex.groupOverride))
                    targetGroup = ex.groupOverride;
                if (!string.IsNullOrEmpty(ex.addressOverride))
                    address = ex.addressOverride.Trim();
                if (!string.IsNullOrEmpty(ex.labelsOverride))
                    labels = ParseCsv(ex.labelsOverride);
            }

            AddDesired(result, guid, path, targetGroup, address, labels, report);
        }
    }

    private static void AddDesired(Dictionary<string, DesiredEntry> result, string guid, string path,
        string group, string address, List<string> labels, SyncReport report)
    {
        if (result.ContainsKey(guid))
        {
            report.warnings.Add($"资源命中多条规则，采用先命中者: {path}");
            return;
        }

        result[guid] = new DesiredEntry { guid = guid, assetPath = path, group = group, address = address, labels = labels };
    }

    private static void CheckDuplicateAddresses(Dictionary<string, DesiredEntry> desiredByGuid, SyncReport report)
    {
        var byAddress = new Dictionary<string, List<DesiredEntry>>();
        foreach (DesiredEntry d in desiredByGuid.Values)
        {
            if (!byAddress.TryGetValue(d.address, out List<DesiredEntry> list))
                byAddress[d.address] = list = new List<DesiredEntry>();
            list.Add(d);
        }

        foreach (KeyValuePair<string, List<DesiredEntry>> kv in byAddress)
        {
            if (kv.Value.Count > 1)
                report.errors.Add($"地址重名: \"{kv.Key}\" -> " + string.Join(" | ", kv.Value.Select(d => d.assetPath)));
        }
    }

    /// <summary>
    /// 确保组存在且 schema 对齐统一模板（幂等）。新组：Remote_ 前缀 -> 远程路径，否则本地路径。
    /// 返回是否有实际变更（建组或 schema 标准化）
    /// </summary>
    private static bool EnsureGroup(AddressableAssetSettings settings, string groupName, SyncReport report,
        Dictionary<string, AddressableAssetGroup> groupCache)
    {
        if (groupCache.TryGetValue(groupName, out AddressableAssetGroup cached) && cached != null)
            return false;

        bool changed = false;
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(groupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            BundledAssetGroupSchema bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            bool isRemote = groupName.StartsWith("Remote", StringComparison.OrdinalIgnoreCase);
            bool okBuild = bundleSchema.BuildPath.SetVariableByName(settings, isRemote ? "Remote.BuildPath" : "Local.BuildPath");
            bool okLoad = bundleSchema.LoadPath.SetVariableByName(settings, isRemote ? "Remote.LoadPath" : "Local.LoadPath");

            if (!okBuild || !okLoad)
                report.errors.Add($"创建组 {groupName} 时 Profile 路径变量设置失败（检查 Profile 是否存在 Local/Remote 的 BuildPath/LoadPath）");
            else
                report.infos.Add($"创建组: {groupName} ({(isRemote ? "远程" : "本地")})");

            EditorUtility.SetDirty(bundleSchema);
            changed = true;
        }

        if (NormalizeGroupSchema(group, report))
            changed = true;

        groupCache[groupName] = group;
        return changed;
    }

    /// <summary>
    /// 把组 schema 对齐到统一模板（与 wgame 远程组配置一致），值相同则无操作。
    /// 热更策略为整包级：Prevent Updates(StaticContent) 关闭，组内任一资源变更时整包重新下发。
    /// BundleNaming 必须为 AppendHash：bundle 文件名 = 组名_hash.bundle，运行时依赖 remote_ 前缀识别
    /// </summary>
    private static bool NormalizeGroupSchema(AddressableAssetGroup group, SyncReport report)
    {
        BundledAssetGroupSchema s = group.GetSchema<BundledAssetGroupSchema>();
        if (s == null)
            return false;

        bool changed = s.Compression != BundledAssetGroupSchema.BundleCompressionMode.LZ4
            || !s.IncludeInBuild
            || s.ForceUniqueProvider
            || !s.UseAssetBundleCache
            || !s.UseAssetBundleCrc
            || !s.UseAssetBundleCrcForCachedBundles
            || s.UseUnityWebRequestForLocalBundles
            || s.Timeout != 10
            || s.ChunkedTransfer
            || s.RedirectLimit != -1
            || s.RetryCount != 0
            || !s.IncludeAddressInCatalog
            || s.IncludeGUIDInCatalog
            || !s.IncludeLabelsInCatalog
            || s.InternalIdNamingMode != BundledAssetGroupSchema.AssetNamingMode.Filename
            || s.InternalBundleIdMode != BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash
            || s.AssetBundledCacheClearBehavior != BundledAssetGroupSchema.CacheClearBehavior.ClearWhenSpaceIsNeededInCache
            || s.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel
            || s.BundleNaming != BundledAssetGroupSchema.BundleNamingStyle.AppendHash
            || s.AssetLoadMode != AssetLoadMode.RequestedAssetAndDependencies;

        s.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        s.IncludeInBuild = true;
        s.ForceUniqueProvider = false;
        s.UseAssetBundleCache = true;
        s.UseAssetBundleCrc = true;
        s.UseAssetBundleCrcForCachedBundles = true;
        s.UseUnityWebRequestForLocalBundles = false;
        s.Timeout = 10;
        s.ChunkedTransfer = false;
        s.RedirectLimit = -1;
        s.RetryCount = 0;
        s.IncludeAddressInCatalog = true;
        s.IncludeGUIDInCatalog = false;
        s.IncludeLabelsInCatalog = true;
        // 该属性 setter 无判重，手动守卫避免每次同步都误标脏
        if (s.InternalIdNamingMode != BundledAssetGroupSchema.AssetNamingMode.Filename)
            s.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.Filename;
        s.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash;
        s.AssetBundledCacheClearBehavior = BundledAssetGroupSchema.CacheClearBehavior.ClearWhenSpaceIsNeededInCache;
        s.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel;
        // 组名_hash.bundle 命名，与运行时 remote_ 前缀的 bundle 识别逻辑(AddressableHelper/ToolBox)一致
        s.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
        s.AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;

        ContentUpdateGroupSchema updateSchema = group.GetSchema<ContentUpdateGroupSchema>();
        if (updateSchema != null && updateSchema.StaticContent)
        {
            updateSchema.StaticContent = false;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(s);
            report.infos.Add($"组标准化: {group.Name} schema 对齐模板");
        }

        return changed;
    }

    /// <summary>
    /// 运行 Addressables 自带的「Check Duplicate Bundle Dependencies」分析：
    /// 检测同一份非条目资源被重复打进多个 bundle 的情况。
    /// 治理方式：将共享资源显式设为可寻址并入公共组（打包一次，其余组以依赖引用）
    /// </summary>
    private static void CheckDuplicateBundleDependencies(AddressableAssetSettings settings, SyncReport report)
    {
        var rule = new CheckBundleDupeDependencies();
        List<AnalyzeRule.AnalyzeResult> results = rule.RefreshAnalysis(settings);
        if (results == null || results.Count == 0)
            return;

        var issues = new List<string>();
        foreach (AnalyzeRule.AnalyzeResult result in results)
        {
            if (result.resultName.Contains("No issues found"))
                continue;
            // 失败类信息（如存在未保存场景）带规则名前缀；资源类条目格式为 "组名:bundle名:资源路径"
            issues.Add(result.resultName.StartsWith(rule.ruleName)
                ? result.resultName.Substring(rule.ruleName.Length)
                : result.resultName);
        }

        if (issues.Count == 0)
        {
            report.infos.Add("重复依赖检查: 无重复打包");
            return;
        }

        const int maxLines = 30;
        report.warnings.Add($"发现重复打包的共享依赖 {issues.Count} 项（建议将共享资源设为可寻址并入公共组）:");
        foreach (string line in issues.Take(maxLines))
            report.warnings.Add("    " + line.TrimStart());
        if (issues.Count > maxLines)
            report.warnings.Add($"    ...其余 {issues.Count - maxLines} 项请在 Addressables 窗口 Tools > Analyze 查看");
    }

    /// <summary>
    /// 把条目 Label 对齐到期望集合，返回是否有变更
    /// </summary>
    private static bool SyncLabels(AddressableAssetEntry entry, List<string> desired)
    {
        bool changed = false;
        var want = new HashSet<string>(desired ?? new List<string>());
        foreach (string label in entry.labels.ToList())
        {
            if (!want.Remove(label))
            {
                entry.SetLabel(label, false, true, false);
                changed = true;
            }
        }

        foreach (string label in want)
        {
            entry.SetLabel(label, true, true, false);
            changed = true;
        }

        return changed;
    }

    private static bool IsContentUpdateGroup(string groupName)
    {
        return groupName.StartsWith(ContentUpdateGroupPrefix, StringComparison.Ordinal);
    }

    private static bool IsIgnored(ResGroupRuleConfig config, string path)
    {
        path = NormalizePath(path);
        foreach (string ignore in config.ignorePaths)
        {
            string p = NormalizePath(ignore);
            if (string.IsNullOrEmpty(p))
                continue;
            if (path == p || path.StartsWith(p + "/", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/').TrimEnd('/');
    }

    private static List<string> ParseCsv(string csv)
    {
        if (string.IsNullOrEmpty(csv))
            return new List<string>();
        return csv.Split(new[] { ',', ';', '，', '；' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }

    private static string NormalizeExtension(string ext)
    {
        ext = ext?.Trim().ToLowerInvariant() ?? "";
        if (ext.Length == 0)
            return ext;
        return ext.StartsWith(".") ? ext : "." + ext;
    }

    /// <summary>地址规则：文件名去扩展名（如 Game.dll.bytes -> Game.dll）</summary>
    private static string DefaultAddress(string assetPath)
    {
        return Path.GetFileNameWithoutExtension(assetPath);
    }
}
