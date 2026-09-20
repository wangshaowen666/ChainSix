/*--------------------------------------------------------------
 * File: AssetBundleTool.cs
 * Author: Wsw
 * Feedback: 614270423@qq.com
 * Time: 2025/12/26 10:51:41 
 *--------------------------------------------------------------
 */
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

public partial class ToolBox : OdinEditorWindow
{
    [Title("日志信息")]
    [HideLabel]
    [CustomValueDrawer("DrawLogFieldWithButtons")]
    public string logInfoTextField = "";
    
    private string DrawLogFieldWithButtons(string value, GUIContent label)
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth - 100));
        value = EditorGUILayout.TextArea(
            value, 
            GUILayout.Height(EditorGUIUtility.singleLineHeight * 6),
            GUILayout.ExpandWidth(true)
        );
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(80));
        
        GUI.color = new Color(0f, 1f, 0f);
        if (GUILayout.Button("清除", GUILayout.Height(25)))
        {
            value = "";
        }
        GUI.color = Color.white;
        
        if (GUILayout.Button("测试", GUILayout.Height(25)))
        {
            value = Test();
        }
        
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        
        return value;
    }
    
    [InlineEditor]
    [Title("路径配置")]
    [HideLabel]
    public EditorPathConfig editorPathConfig;
    
    [MenuItem("Tools/Game/工具箱", false, 10)]
    private static void OpenWindow()
    {
        var window = GetWindow<ToolBox>();
        window.position = GUIHelper.GetEditorWindowRect().AlignCenter(700, 700);
    }
    protected override void OnEnable()
    {
        if (editorPathConfig ==null)
        {
            string[] guids = AssetDatabase.FindAssets("t:EditorPathConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                editorPathConfig = AssetDatabase.LoadAssetAtPath<EditorPathConfig>(path);
            }
            else
            {
                // 创建默认配置文件
                editorPathConfig = CreateInstance<EditorPathConfig>();
                AssetDatabase.CreateAsset(editorPathConfig, "Assets/Res/Config/EditorPathConfig.asset");
                AssetDatabase.SaveAssets();
            }
        }
    }
    
    //[OnInspectorGUI] private void Space1() { GUILayout.Space(20); }
    
    private string Test()
    {
        return "a.ToString();";
    }

    private void AddLogInfo(string str)
    {
        logInfoTextField = (str + "\n") + logInfoTextField;
    }
}