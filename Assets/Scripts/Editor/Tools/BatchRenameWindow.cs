#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量重命名工具，可为选中对象添加或移除前缀。
/// </summary>
public class BatchRenameWindow : EditorWindow
{
    private string prefix = "OrangeStar_";
    private bool addPrefix = true;

    [MenuItem(OperationMarigoldPaths.ToolsUtility + "/Batch Rename")]
    static void ShowWindow()
    {
        GetWindow<BatchRenameWindow>("批量重命名");
    }

    void OnGUI()
    {
        prefix = EditorGUILayout.TextField("前缀", prefix);
        addPrefix = EditorGUILayout.Toggle("添加前缀", addPrefix);

        if (GUILayout.Button("对选中对象添加前缀"))
        {
            RenameSelected();
        }
    }

    void RenameSelected()
    {
        foreach (var obj in Selection.gameObjects)
        {
            string newName = addPrefix ? prefix + obj.name : obj.name.Replace(prefix, "");
            obj.name = newName;
            EditorUtility.SetDirty(obj);
        }
        AssetDatabase.Refresh();
    }
}
#endif
