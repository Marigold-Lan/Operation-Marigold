#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器工具：一键补满当前场景内所有已布置单位的生命、燃料和弹药。
/// </summary>
public static class RefillUnitsInSceneTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/补满场景单位状态";
    private const string UndoLabel = "Refill Units In Scene";

    [MenuItem(MenuPath)]
    private static void RefillAllUnitsInScene()
    {
        var units = Object.FindObjectsByType<UnitController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var total = 0;
        var updated = 0;
        var skippedNoData = 0;

        for (var i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            if (unit == null)
                continue;
            if (EditorUtility.IsPersistent(unit))
                continue;

            total++;

            var data = unit.Data;
            if (data == null)
            {
                skippedNoData++;
                continue;
            }

            Undo.RecordObject(unit, UndoLabel);
            unit.CurrentFuel = data.maxFuel;
            unit.CurrentAmmo = data.MaxPrimaryAmmo;
            unit.HasActed = false;
            EditorUtility.SetDirty(unit);

            var health = unit.Health != null ? unit.Health : unit.GetComponent<UnitHealth>();
            if (health != null)
            {
                Undo.RecordObject(health, UndoLabel);
                health.SetHp(data.maxHp);
                EditorUtility.SetDirty(health);
            }

            if (unit.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(unit.gameObject.scene);

            updated++;
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateRefillAllUnitsInScene()
    {
        var units = Object.FindObjectsByType<UnitController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return units != null && units.Length > 0;
    }
}
#endif
