#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器工具：为场景中的总部建筑补齐 HqGameCondition（若缺失）。
/// </summary>
public static class EnsureHqGameConditionsInSceneTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/补齐总部 HqGameCondition";

    [MenuItem(MenuPath)]
    private static void EnsureHqGameConditions()
    {
        var buildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scannedCount = 0;
        var hqCount = 0;
        var addedCount = 0;
        var alreadyCount = 0;
        var missingDataCount = 0;

        for (var i = 0; i < buildings.Length; i++)
        {
            var building = buildings[i];
            if (building == null || EditorUtility.IsPersistent(building))
                continue;

            scannedCount++;

            var data = building.Data;
            if (data == null)
            {
                missingDataCount++;
                continue;
            }

            if (!data.isHq)
                continue;

            hqCount++;
            var go = building.gameObject;
            if (go.GetComponent<HqGameCondition>() != null)
            {
                alreadyCount++;
                continue;
            }

            Undo.AddComponent<HqGameCondition>(go);
            EditorUtility.SetDirty(go);
            if (go.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(go.scene);
            addedCount++;
        }

        var message =
            $"扫描建筑: {scannedCount}\n" +
            $"识别为总部: {hqCount}\n" +
            $"新增 HqGameCondition: {addedCount}\n" +
            $"已存在 HqGameCondition: {alreadyCount}\n" +
            $"缺少 BuildingData: {missingDataCount}";

        EditorUtility.DisplayDialog("补齐完成", message, "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateEnsureHqGameConditions()
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
