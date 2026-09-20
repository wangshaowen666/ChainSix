/*--------------------------------------------------------------
 * File: ToolBox.Excel.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/19 11:26:43 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public partial class ToolBox
{
    [TitleGroup("导表工具")]
    [HorizontalGroup("导表工具/水平布局", Width = 80)] 
    [Button("导表", ButtonSizes.Large)]
    public void ImportExcel()
    {
        var ret = ShellUtil.Run(editorPathConfig.excelShellPath);
        if (!ret.TrimEnd().EndsWith("bye~"))
        {
            AddLogInfo("导表出错：" + ret);
            return;
        }
        AddLogInfo("导表完成");
        AssetDatabase.Refresh();
        EditorApplication.delayCall += AutoCompleteDataTableCtrProperties;
    }
    
    [HorizontalGroup("导表工具/水平布局", Width = 80)] 
    [Button("导Proto", ButtonSizes.Large)]
    public void ImportProto()
    {
        var ret = ShellUtil.Run(editorPathConfig.protoShellPath);
        if (!ret.Contains("[PROTO_OK]"))
        {
            AddLogInfo("导proto出错：" + ret);
            return;
        }
        AddLogInfo("导proto完成");
        AssetDatabase.Refresh();
        EditorApplication.delayCall += AutoCompleteApiRegistry;
    }
    
    /// <summary>
    /// 自动为DataTableCtr补全所有可获取的表的属性
    /// </summary>
    private void AutoCompleteDataTableCtrProperties()
    {
        try
        {
            AddLogInfo("开始自动补全DataTableCtr表属性...");
            var tablePath = GetScriptPathByType(typeof(cfg.Tables));
            if (!File.Exists(tablePath))
            {
                AddLogInfo($"错误：Tables.cs文件不存在");
                return;
            }
            
            var dataTableMgrPath = GetScriptPathByType(typeof(DataTableMgr));
            if (!File.Exists(dataTableMgrPath))
            {
                AddLogInfo($"错误：DataTableCtr.cs文件不存在");
                return;
            }
            
            string tablesContent = File.ReadAllText(tablePath);
            Regex tablePropertyRegex = new Regex(@"^\s*public\s+(\w+)\s+(\w+)\s+\{get;\s+\}\s*$", RegexOptions.Multiline);
            List<Tuple<string, string>> tableProperties = new List<Tuple<string, string>>();
            HashSet<string> validPropertyNames = new HashSet<string>();
            
            foreach (Match match in tablePropertyRegex.Matches(tablesContent))
            {
                string typeName = match.Groups[1].Value;
                string propertyName = match.Groups[2].Value;
                tableProperties.Add(new Tuple<string, string>(typeName, propertyName));
                validPropertyNames.Add(propertyName);
            }
            
            string dataTableCtrContent = File.ReadAllText(dataTableMgrPath);
            Regex dataTablePropertyRegex = new Regex(@"^\s*public\s+(\w+)\s+(\w+)\s+=>\s+_tables\.(\w+);\s*$", RegexOptions.Multiline);
            
            int removedCount = 0;
            dataTableCtrContent = dataTablePropertyRegex.Replace(dataTableCtrContent, match =>
            {
                string propertyName = match.Groups[2].Value;
                if (!validPropertyNames.Contains(propertyName))
                {
                    removedCount++;
                    return string.Empty;
                }
                return match.Value;
            });
            
            HashSet<string> existingProperties = new HashSet<string>();
            foreach (Match match in dataTablePropertyRegex.Matches(dataTableCtrContent))
            {
                string propertyName = match.Groups[3].Value;
                existingProperties.Add(propertyName);
            }
            
            int insertIndex = dataTableCtrContent.IndexOf("private cfg.Tables _tables;");
            if (insertIndex == -1)
            {
                AddLogInfo("错误：在DataTableCtr.cs中未找到private cfg.Tables _tables;声明");
                return;
            }
            
            insertIndex += "private cfg.Tables _tables;".Length;
            
            StringBuilder newPropertiesBuilder = new StringBuilder();
            int newPropertyCount = 0;
            
            foreach (var tableProperty in tableProperties)
            {
                string typeName = tableProperty.Item1;
                string propertyName = tableProperty.Item2;
                
                if (!existingProperties.Contains(propertyName))
                {
                    newPropertiesBuilder.AppendLine($"    public {typeName} {propertyName} => _tables.{propertyName};");
                    newPropertyCount++;
                }
            }
            
            if (removedCount > 0 || newPropertyCount > 0)
            {
                if (newPropertyCount > 0)
                {
                    if (insertIndex < dataTableCtrContent.Length && dataTableCtrContent[insertIndex] != '\n')
                    {
                        newPropertiesBuilder.Insert(0, "\n");
                    }
                    
                    if (insertIndex > 0 && dataTableCtrContent[insertIndex - 1] != '\n')
                    {
                        newPropertiesBuilder.Insert(0, "\n");
                    }
                    dataTableCtrContent = dataTableCtrContent.Insert(insertIndex, newPropertiesBuilder.ToString());
                }
                
                File.WriteAllText(dataTableMgrPath, dataTableCtrContent);
                AssetDatabase.Refresh();
                
                var logParts = new List<string>();
                if (removedCount > 0) logParts.Add($"移除{removedCount}个无效属性");
                if (newPropertyCount > 0) logParts.Add($"新增{newPropertyCount}个属性");
                AddLogInfo($"已成功更新DataTableCtr表属性：" + string.Join("，", logParts));
            }
            else
            {
                AddLogInfo("DataTableCtr已有所有表属性，无需更新");
            }
        }
        catch (Exception ex)
        {
            AddLogInfo($"自动补全DataTableCtr表属性失败：{ex.Message}");
            Debug.LogError($"自动补全DataTableCtr表属性失败：{ex}");
        }
    }
    
    /// <summary>
    /// 自动为 ApiRegistry 补全 RegisterAll 方法（由 NetApi.cs 驱动）
    /// 解析 NetApi.cs 每个字段：字段名 X + 注释 // POST → RegisterPost<NetMsg.XReq>
    ///                                  字段名 X + 注释 // GET  → RegisterGet<NetMsg.XResp>
    /// </summary>
    private void AutoCompleteApiRegistry()
    {
        try
        {
            AddLogInfo("开始自动补全 ApiRegistry.RegisterAll...");
            var netApiPath = GetScriptPathByType(typeof(NetApi));
            if (!File.Exists(netApiPath))
            {
                AddLogInfo("错误：NetApi.cs 文件不存在");
                return;
            }

            var apiRegistryPath = GetScriptPathByType(typeof(ApiRegistry));
            if (!File.Exists(apiRegistryPath))
            {
                AddLogInfo("错误：ApiRegistry.cs 文件不存在");
                return;
            }

            string netApiContent = File.ReadAllText(netApiPath);

            // 解析 NetApi.cs 的字段与分组注释（按行顺序扫描）：
            //   // ===== 分组名 =====  → 当前分组标题（输出一次）
            //   public const string X = "url";   // POST：说明 / // GET：说明
            // 注释里的方法标识不区分大小写（post/get/POST/GET 均可）
            Regex apiRegex = new Regex(
                @"public\s+const\s+string\s+(\w+)\s*=\s*""[^""]*"";\s*//\s*(POST|GET)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Regex groupRegex = new Regex(@"//\s*=====\s*(.+?)\s*=====", RegexOptions.Multiline);

            var apiMatches = apiRegex.Matches(netApiContent);
            if (apiMatches.Count == 0)
            {
                AddLogInfo("错误：NetApi.cs 中没有找到带 POST/GET 注释的字段");
                return;
            }

            // 按行顺序扫描，遇到分组记录当前分组，遇到接口时输出所属分组（仅一次）
            StringBuilder newBody = new StringBuilder();
            string currentGroup = null;
            foreach (string line in netApiContent.Split('\n'))
            {
                var groupMatch = groupRegex.Match(line);
                if (groupMatch.Success)
                {
                    currentGroup = groupMatch.Groups[1].Value;
                    continue;
                }

                var apiMatch = apiRegex.Match(line);
                if (!apiMatch.Success) continue;

                if (currentGroup != null)
                {
                    newBody.AppendLine($"        // ===== {currentGroup} =====");
                    currentGroup = null; // 已输出，避免重复
                }

                string fieldName = apiMatch.Groups[1].Value;
                string method = apiMatch.Groups[2].Value;
                if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
                    newBody.AppendLine($"        RegisterPost<NetMsg.{fieldName}Req>(NetApi.{fieldName});");
                else
                    newBody.AppendLine($"        RegisterGet<NetMsg.{fieldName}Resp>(NetApi.{fieldName});");
            }

            // 替换 ApiRegistry.cs 中的 RegisterAll 方法体
            // 注意：模板缩进必须幂等（方法名/大括号固定 4 空格、方法体固定 8 空格），
            //      否则每次执行缩进逐层加深（原模板曾因缩进被正则 \s* 吸收而累积）
            string apiRegistryContent = File.ReadAllText(apiRegistryPath);
            Regex registerAllRegex = new Regex(
                @"public\s+static\s+void\s+RegisterAll\(\)\s*\{[\s\S]*?\n\s*\}", RegexOptions.Multiline);

            if (!registerAllRegex.IsMatch(apiRegistryContent))
            {
                AddLogInfo("错误：ApiRegistry.cs 中未找到 RegisterAll 方法");
                return;
            }

            // 固定模板：方法名与 { } 各 4 空格缩进，方法体行由 newBody 自带 8 空格缩进
            string newRegisterAll = "    public static void RegisterAll()\n    {\n"
                                    + newBody
                                    + "    }";
            apiRegistryContent = registerAllRegex.Replace(apiRegistryContent, newRegisterAll);

            File.WriteAllText(apiRegistryPath, apiRegistryContent);
            AssetDatabase.Refresh();

            AddLogInfo($"已自动补全 ApiRegistry.RegisterAll（共 {apiMatches.Count} 个接口）");
        }
        catch (Exception ex)
        {
            AddLogInfo($"自动补全 ApiRegistry.RegisterAll 失败：{ex.Message}");
            Debug.LogError($"自动补全 ApiRegistry.RegisterAll 失败：{ex}");
        }
    }

    private string GetScriptPathByType(Type type)
    {
        string[] guids = AssetDatabase.FindAssets($"t:Script {type.Name}");

        if (guids.Length == 0)
            return null;

        return AssetDatabase.GUIDToAssetPath(guids[0]);
    }
   
}