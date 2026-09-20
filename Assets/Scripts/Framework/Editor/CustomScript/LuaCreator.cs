﻿﻿﻿/*--------------------------------------------------------------
 * File: LuaCreator.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/08/05 14:55:33 
 *--------------------------------------------------------------
 */

using System.IO;
using UnityEditor;
using UnityEngine;

public class LuaCreator 
{
    private const string LuaTemplateName = "CustomLuaScript";
    
    [MenuItem("Assets/Create/Custom Lua Script", false, 71)]
    private static void CreateCustomCSharpScript()
    {
        string templatePath = FindTemplatePath(LuaTemplateName);
        if (templatePath == null) return;
        Create(templatePath, "NewLuaScript.lua.txt");
    }

    private static string FindTemplatePath(string fileName)
    {
        string[] guids = AssetDatabase.FindAssets(fileName);
        if (guids.Length == 0)
        {
            Log.Error($"找不到Lua模板文件: {fileName}");
            return null;
        }
        return AssetDatabase.GUIDToAssetPath(guids[0]);
    }

    private static void Create(string path, string defaultName)
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("警告", "无法在游戏运行时或代码编译时创建脚本", "确定");
            return;
        }

        string folderPath = GetSelectionAssetDirPath();
        //Texture2D icon = EditorGUIUtility.IconContent("d_cs Script Icon").image as Texture2D;
       
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0,
            ScriptableObject.CreateInstance<CreateCustomScriptAction>(),
            folderPath + "/" + defaultName, null,
            path);
    }
    
    /// <summary>
    /// 获取选中文件的文件夹路径
    /// </summary>
    /// <returns></returns>
    private static string GetSelectionAssetDirPath()
    {
        string path = "Assets";
        foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            if (obj == null)
            {
                continue;
            }

            path = AssetDatabase.GetAssetPath(obj);
            if (Path.HasExtension(path))
            {
                path = Path.GetDirectoryName(path);
            }
            break;
        }

        return path;
    }
}