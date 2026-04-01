#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器工具：为场景中的工厂建筑补齐 FactorySpawner（若缺失）。
/// </summary>
public static class EnsureFactorySpawnersInSceneTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/补齐工厂 FactorySpawner";

    [MenuItem(MenuPath)]
    private static void EnsureFactorySpawners()
    {
        var buildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scannedCount = 0;
        var factoryCount = 0;
        var addedCount = 0;
        var alreadyCount = 0;

        for (var i = 0; i < buildings.Length; i++)
        {
            var building = buildings[i];
            if (building == null || EditorUtility.IsPersistent(building))
                continue;

            scannedCount++;
            if (!IsFactoryBuilding(building))
                continue;

            factoryCount++;
            var go = building.gameObject;
            if (go.GetComponent<FactorySpawner>() != null)
            {
                alreadyCount++;
                continue;
            }

            Undo.AddComponent<FactorySpawner>(go);
            EditorUtility.SetDirty(go);
            if (go.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(go.scene);
            addedCount++;
        }

        var message =
            $"扫描建筑: {scannedCount}\n" +
            $"识别为工厂: {factoryCount}\n" +
            $"新增 FactorySpawner: {addedCount}\n" +
            $"已存在 FactorySpawner: {alreadyCount}";

        EditorUtility.DisplayDialog("补齐完成", message, "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateEnsureFactorySpawners()
    {
        var buildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < buildings.Length; i++)
        {
            if (buildings[i] != null && !EditorUtility.IsPersistent(buildings[i]))
                return true;
        }

        return false;
    }

    private static bool IsFactoryBuilding(BuildingController building)
    {
        if (building == null)
            return false;

        if (building.GetComponent<FactorySpawner>() != null)
            return true;

        var data = building.Data;
        if (data != null)
        {
            var id = data.id != null ? data.id.ToLowerInvariant() : string.Empty;
            var display = data.displayName != null ? data.displayName.ToLowerInvariant() : string.Empty;
            if (id.Contains("factory") || display.Contains("factory"))
                return true;
        }

        var lowerName = building.name.ToLowerInvariant();
        if (lowerName.Contains("factory"))
            return true;

        return false;
    }
}
#endif
