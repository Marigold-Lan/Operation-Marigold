#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器工具：为场景中挂有 BuildingController 的建筑补齐 BuildingView（若缺失）。
/// </summary>
public static class EnsureBuildingViewsInSceneTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/补齐建筑 BuildingView";

    [MenuItem(MenuPath)]
    private static void EnsureBuildingViews()
    {
        var buildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scannedCount = 0;
        var addedCount = 0;

        for (var i = 0; i < buildings.Length; i++)
        {
            var controller = buildings[i];
            if (controller == null || EditorUtility.IsPersistent(controller))
                continue;

            scannedCount++;
            if (controller.GetComponent<BuildingView>() != null)
                continue;

            Undo.AddComponent<BuildingView>(controller.gameObject);
            EditorUtility.SetDirty(controller.gameObject);
            if (controller.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            addedCount++;
        }

        var message = $"扫描建筑: {scannedCount}\n新增 BuildingView: {addedCount}";
        EditorUtility.DisplayDialog("补齐完成", message, "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateEnsureBuildingViews()
    {
        var buildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] != null && !EditorUtility.IsPersistent(buildings[i]))
                return true;
        }

        return false;
    }
}
#endif
