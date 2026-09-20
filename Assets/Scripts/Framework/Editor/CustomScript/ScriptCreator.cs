/*--------------------------------------------------------------
 * File: NewCSharpScript.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2024/01/17 18:27:54
 * Copyright: Copyright © 2024 wangshaowen. All rights reserved.
 *--------------------------------------------------------------
 */

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using Object = UnityEngine.Object;

public class ScriptCreator
{
    private const string ScriptTemplateName = "CustomC#Script";

    [MenuItem("Assets/Create/Custom C# Script", false, 70)]
    private static void CreateCustomCSharpScript()
    {
        string templatePath = FindTemplatePath(ScriptTemplateName);
        if (templatePath == null) return;
        Create(templatePath, "NewCSharpScript.cs");
    }

    private static string FindTemplatePath(string fileName)
    {
        string[] guids = AssetDatabase.FindAssets(fileName);
        if (guids.Length == 0)
        {
            Log.Error($"找不到脚本模板文件: {fileName}");
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
        Texture2D icon = EditorGUIUtility.IconContent("d_cs Script Icon").image as Texture2D;
       
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0,
            ScriptableObject.CreateInstance<CreateCustomScriptAction>(),
            folderPath + "/" + defaultName, icon,
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

public class CreateCustomScriptAction : EndNameEditAction
{
    public override void Action(int instanceId, string pathName, string resourceFile)
    {
        // 读入模板内容
        StreamReader sr = new StreamReader(resourceFile);
        string content = sr.ReadToEnd();
        sr.Close();

        // 修改内容中的部分内容
        content = content.Replace("#SCRIPTNAME#", Path.GetFileNameWithoutExtension(pathName));
        content = content.Replace("#Author#", "Wsw");
        content = content.Replace("#Feedback#", "614270423@qq.com");
        content = content.Replace("#TIME#", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
        //content = content.Replace("#Copyright#", "Copyright © 2024 wangshaowen. All rights reserved.");
        
        // 将修改后的内容写入新创建的文件
        StreamWriter sw = new StreamWriter(Path.GetFullPath(pathName), false, new UTF8Encoding(false, false));
        sw.Write(content);
        sw.Close();
        
        // 导入资源
        AssetDatabase.ImportAsset(pathName);
        Object obj = AssetDatabase.LoadAssetAtPath(pathName, typeof(UnityEngine.Object));
        
        ProjectWindowUtil.ShowCreatedAsset(obj);
    }
}
