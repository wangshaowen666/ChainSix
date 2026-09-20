/*--------------------------------------------------------------
 * File: ToolBox.Stats.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2026/01/19 10:26:20 
 *--------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

public partial class ToolBox
{
#if STATS_ON && UNITY_EDITOR
    private static readonly string s_exportDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "文档", "统计输出");
    
    [TitleGroup("统计工具")]
    [HorizontalGroup("统计工具/水平布局", Width = 130)]
    [Button("导出对象池统计数据", ButtonSizes.Large)]
    public void ExportObjectPoolInfo()
    {
        try
        {
            Directory.CreateDirectory(s_exportDir);
            string exportFileName = Path.Combine(s_exportDir, $"对象池统计数据 {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");

            var contents = CoreMgr.ObjectPool.DealPoolStats();
            AddLogInfo(ParseUtil.ToJson(contents));
            int index = 0;
            string[] data = new string[contents.Count + 1];
            data[index++] = "类名,变量名,总对象数,活跃对象数,非活跃对象数,峰值对象数,总获取次数,总放回次数,总释放次数,总加载次数,外部对象数";
            foreach (string str in contents)
            {
                data[index++] = str;
            }

            File.WriteAllLines(exportFileName, data, Encoding.UTF8);
            AddLogInfo($"导出对象池统计信息 '{exportFileName}' 成功.");
            OpenExportFile(exportFileName);
        }
        catch (Exception exception)
        {
            AddLogInfo($"导出对象池统计信息失败, 原因'{exception}'.");
        }
    }
    
    [HorizontalGroup("统计工具/水平布局", Width = 120, PaddingLeft = 0)]
    [Button("导出类池统计数据", ButtonSizes.Large)]
    public void ExportClassPoolInfo()
    {
        try
        {
            Directory.CreateDirectory(s_exportDir);
            string exportFileName = Path.Combine(s_exportDir, $"类池统计数据 {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");

            var contents = CoreMgr.ClassPool.DealPoolStats();
            AddLogInfo(ParseUtil.ToJson(contents));
            int index = 0;
            string[] data = new string[contents.Count + 1];
            data[index++] = "类名,容量,预热数量,创建数量,池中总数量,峰值数量,总获取次数,总放回次数,总释放数量";
            foreach (string str in contents)
            {
                data[index++] = str;
            }

            File.WriteAllLines(exportFileName, data, Encoding.UTF8);
            AddLogInfo($"导出类池统计信息 '{exportFileName}' 成功.");
            OpenExportFile(exportFileName);
        }
        catch (Exception exception)
        {
            AddLogInfo($"导出类池统计信息失败, 原因'{exception}'.");
        }
    }
    
    [HorizontalGroup("统计工具/水平布局", Width = 160)]
    [Button("导出AssetBundle统计数据", ButtonSizes.Large)]
    public void ExportAssetBundleInfo()
    {
        try
        {
            Directory.CreateDirectory(s_exportDir);
            string exportFileName = Path.Combine(s_exportDir, $"AssetBundle统计数据 {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");

            var contents = CoreMgr.Res.DealPoolStats();
            AddLogInfo(ParseUtil.ToJson(contents));
            int index = 0;
            string[] data = new string[contents.Count + 1];
            data[index++] = "资源名,当前引用数量,峰值引用数量,总获取次数,总释放次数";
            foreach (string str in contents)
            {
                data[index++] = str;
            }

            File.WriteAllLines(exportFileName, data, Encoding.UTF8);
            AddLogInfo($"导出AssetBundle统计信息 '{exportFileName}' 成功.");
            OpenExportFile(exportFileName);
        }
        catch (Exception exception)
        {
            AddLogInfo($"导出AssetBundle统计信息失败, 原因'{exception}'.");
        }
    }

    [HorizontalGroup("统计工具/水平布局", Width = 160)]
    [Button("导出Shader变体统计数据", ButtonSizes.Large)]
    public void ExportShaderVariantInfo()
    {
        try
        {
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Res/Common/Shader" });
            var data = new List<string> { "Shader名称,变体数量" };

            int shaderCount = 0;
            long totalVariants = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                {
                    continue;
                }

                long variantCount = GetShaderVariantCount(shader);
                shaderCount++;
                totalVariants += variantCount;
                data.Add($"{shader.name},{variantCount}");
                AddLogInfo($"Shader '{shader.name}' 变体数量: {variantCount}");
            }

            data.Add($"合计,{totalVariants}");
            Directory.CreateDirectory(s_exportDir);
            string exportFileName = Path.Combine(s_exportDir, $"Shader变体统计数据 {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");
            File.WriteAllLines(exportFileName, data, Encoding.UTF8);

            AddLogInfo($"共统计Shader {shaderCount} 个, 变体总数 {totalVariants}, 导出 '{exportFileName}' 成功.");
            OpenExportFile(exportFileName);
        }
        catch (Exception exception)
        {
            AddLogInfo($"导出Shader变体统计信息失败, 原因'{exception}'.");
        }
    }

    // 用系统默认程序(Excel/表格软件)打开导出的CSV文件
    private void OpenExportFile(string exportFileName)
    {
        try
        {
            EditorUtility.OpenWithDefaultApp(exportFileName);
            AddLogInfo($"已用默认程序打开 '{exportFileName}'.");
        }
        catch (Exception exception)
        {
            AddLogInfo($"打开文件 '{exportFileName}' 失败, 原因'{exception}'.");
        }
    }

    // 获取Shader实际变体数量: 优先反射Shader.variantCount属性, 不可用时反射ShaderUtil.GetVariantCount内部方法
    private static long GetShaderVariantCount(Shader shader)
    {
        PropertyInfo property = typeof(Shader).GetProperty("variantCount", BindingFlags.Public | BindingFlags.Instance);
        object result = property != null ? property.GetValue(shader) : null;
        if (result != null)
        {
            return Convert.ToInt64(result);
        }

        MethodInfo method = typeof(ShaderUtil).GetMethod("GetVariantCount", BindingFlags.Static | BindingFlags.NonPublic);
        result = method != null ? method.Invoke(null, new object[] { shader, false }) : null;
        return result != null ? Convert.ToInt64(result) : -1;
    }
#endif
}