using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Text;
using System.IO;

public class PrefabHierarchyExporter
{
    [MenuItem("Assets/Tool/Delete Hierarchy Files")]
    public static void DeleteHierarchyFiles()
    {
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeInstanceID);
        if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
        {
            EditorUtility.DisplayDialog("Delete Hierarchy Files",
                "请在 Project 窗口中选择一个文件夹", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("_hierarchy t:TextAsset", new[] { assetPath });
        if (guids.Length == 0)
        {
            // 兜底: 按文件名搜索
            guids = AssetDatabase.FindAssets("*_hierarchy*", new[] { assetPath });
        }

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Delete Hierarchy Files",
                "文件夹下未找到 _hierarchy 文件", "OK");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog("Delete Hierarchy Files",
            $"将删除 {guids.Length} 个 _hierarchy 文件\n目录: {assetPath}", "删除", "取消");
        if (!confirm) return;

        int count = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string filePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (filePath.EndsWith("_hierarchy.txt"))
            {
                bool deleted = AssetDatabase.DeleteAsset(filePath);
                if (deleted) count++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"已删除 {count} 个 _hierarchy 文件");
    }

    [MenuItem("Assets/Tool/Export Prefab Hierarchy")]
    public static void ExportSelectedPrefab()
    {
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeInstanceID);

        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("Export Prefab Hierarchy",
                "请在 Project 窗口中选择一个 Prefab 或文件夹", "OK");
            return;
        }

        // 选中文件夹: 遍历所有 prefab
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            ExportAllPrefabsInFolder(assetPath);
            return;
        }

        // 选中单个 prefab
        if (assetPath.EndsWith(".prefab"))
        {
            ExportPrefabAtPath(assetPath);
            return;
        }

        EditorUtility.DisplayDialog("Export Prefab Hierarchy",
            "请在 Project 窗口中选择一个 Prefab 或文件夹", "OK");
    }

    static void ExportAllPrefabsInFolder(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("Export Prefab Hierarchy",
                "文件夹下未找到 Prefab: " + folderPath, "OK");
            return;
        }

        int count = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            EditorUtility.DisplayProgressBar(
                "Exporting Prefab Hierarchy",
                $"({i + 1}/{guids.Length}) {Path.GetFileName(prefabPath)}",
                (float)(i + 1) / guids.Length);

            ExportPrefabAtPath(prefabPath, false);
            count++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();
        Debug.Log($"已完成 {count} 个 Prefab 层级导出，目录: {folderPath}");
    }

    static void ExportPrefabAtPath(string prefabPath, bool revealInFinder = true)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (root == null)
        {
            Debug.LogError("无法加载 Prefab: " + prefabPath);
            return;
        }

        // 输出到 prefab 同级目录
        string outputPath = prefabPath.Replace(".prefab", "_hierarchy.txt");
        string content = BuildHierarchyText(root);
        File.WriteAllText(outputPath, content);
        AssetDatabase.Refresh();
        Debug.Log("Prefab 层级已导出: " + outputPath);

        if (revealInFinder)
        {
            EditorUtility.RevealInFinder(outputPath);
        }
    }

    static string BuildHierarchyText(GameObject root)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Prefab Hierarchy: " + root.name + " ===");
        sb.AppendLine();
        BuildNodeRecursive(root.transform, 0, sb);
        return sb.ToString();
    }

    static void BuildNodeRecursive(Transform transform, int depth, StringBuilder sb)
    {
        string indent = new string(' ', depth * 2);
        GameObject go = transform.gameObject;

        string activeStr = go.activeSelf ? "" : " [INACTIVE]";
        sb.Append(indent + go.name + activeStr);

        // 组件列表
        var components = go.GetComponents<Component>();
        var compNames = new System.Collections.Generic.List<string>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (typeName == "RectTransform") continue;
            compNames.Add(typeName);
        }
        if (compNames.Count > 0)
        {
            sb.Append("  <" + string.Join(", ", compNames.ToArray()) + ">");
        }

        // Animator 控制器 + 动画状态名
        var animator = go.GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            string ctrlPath = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            string ctrlName = Path.GetFileNameWithoutExtension(ctrlPath);
            string states = GetAnimatorStateNames(ctrlPath);
            if (!string.IsNullOrEmpty(states))
            {
                sb.Append("  [Controller: " + ctrlName + " | States: " + states + "]");
            }
            else
            {
                sb.Append("  [Controller: " + ctrlName + "]");
            }
        }

        // Image sprite
        var image = go.GetComponent<Image>();
        if (image != null && image.sprite != null)
        {
            string spritePath = AssetDatabase.GetAssetPath(image.sprite);
            sb.Append("  [Sprite: " + Path.GetFileNameWithoutExtension(spritePath) + "]");
        }

        // Image fillAmount (进度条)
        if (image != null && image.type == Image.Type.Filled)
        {
            sb.Append("  [fillAmount: " + image.fillAmount + "]");
        }

        // Text 内容
        var text = go.GetComponent<Text>();
        if (text != null && !string.IsNullOrEmpty(text.text))
        {
            sb.Append("  [Text: \"" + text.text + "\"]");
        }

        // TextMeshProUGUI 内容
        var tmpText = go.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
        {
            sb.Append("  [TMP: \"" + tmpText.text + "\"]");
        }

        // Toggle isOn
        var toggle = go.GetComponent<Toggle>();
        if (toggle != null)
        {
            sb.Append("  [isOn: " + toggle.isOn + "]");
        }

        // Slider value
        var slider = go.GetComponent<Slider>();
        if (slider != null)
        {
            sb.Append("  [value: " + slider.value + "]");
        }

        sb.AppendLine();

        for (int i = 0; i < transform.childCount; i++)
        {
            BuildNodeRecursive(transform.GetChild(i), depth + 1, sb);
        }
    }

    static string GetAnimatorStateNames(string controllerPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        if (controller == null || controller.layers.Length == 0)
            return "";

        var stateNames = new System.Collections.Generic.List<string>();
        CollectStatesFromStateMachine(controller.layers[0].stateMachine, stateNames);
        return string.Join(", ", stateNames.ToArray());
    }

    static void CollectStatesFromStateMachine(UnityEditor.Animations.AnimatorStateMachine sm, System.Collections.Generic.List<string> names)
    {
        foreach (var childState in sm.states)
        {
            names.Add(childState.state.name);
        }
        // 递归子状态机
        foreach (var childSm in sm.stateMachines)
        {
            CollectStatesFromStateMachine(childSm.stateMachine, names);
        }
    }
}
