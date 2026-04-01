#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器工具：为场景中的 Cell 补齐 CellView（若缺失）。
/// </summary>
public static class EnsureCellViewsInSceneTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/补齐 CellView";

    [MenuItem(MenuPath)]
    private static void EnsureCellViews()
    {
        var cells = Object.FindObjectsByType<Cell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scannedCount = 0;
        var addedCount = 0;

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell == null || EditorUtility.IsPersistent(cell))
                continue;

            scannedCount++;
            if (cell.GetComponent<CellView>() != null)
                continue;

            Undo.AddComponent<CellView>(cell.gameObject);
            EditorUtility.SetDirty(cell.gameObject);
            if (cell.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(cell.gameObject.scene);
            addedCount++;
        }

        var message = $"扫描 Cell: {scannedCount}\n新增 CellView: {addedCount}";
        EditorUtility.DisplayDialog("补齐完成", message, "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateEnsureCellViews()
    {
        var cells = Object.FindObjectsByType<Cell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] != null && !EditorUtility.IsPersistent(cells[i]))
                return true;
        }

        return false;
    }
}
#endif
