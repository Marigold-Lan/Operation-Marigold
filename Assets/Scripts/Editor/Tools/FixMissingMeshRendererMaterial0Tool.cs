#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FixMissingMeshRendererMaterial0Tool
{
    private const string MaterialName = "MasterMat";
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/修复 MeshRenderer Missing 材质 (Element0→MasterMat)";

    [MenuItem(MenuPath)]
    private static void Fix()
    {
        var masterMat = FindMasterMaterial(out var chosenPath, out var candidateCount);
        if (masterMat == null)
        {
            EditorUtility.DisplayDialog(
                "未找到材质",
                $"找不到名为 {MaterialName} 的材质。\n请确认工程内存在 {MaterialName}.mat",
                "确定");
            return;
        }

        var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scannedCount = 0;
        var fixedCount = 0;

        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || EditorUtility.IsPersistent(r))
                continue;

            scannedCount++;

            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0)
                continue;

            if (mats[0] != null)
                continue;

            Undo.RecordObject(r, $"Fix Missing Material0 -> {MaterialName}");
            mats[0] = masterMat;
            r.sharedMaterials = mats;

            EditorUtility.SetDirty(r);
            if (r.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(r.gameObject.scene);

            fixedCount++;
        }

        var extraHint = candidateCount > 1
            ? $"\n注意：找到 {candidateCount} 个同名材质，本次使用：\n{chosenPath}"
            : string.Empty;

        EditorUtility.DisplayDialog(
            "修复完成",
            $"扫描 MeshRenderer: {scannedCount}\n修复 element0 Missing: {fixedCount}{extraHint}",
            "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateFix()
    {
        var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || EditorUtility.IsPersistent(r))
                continue;

            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0 && mats[0] == null)
                return true;
        }

        return false;
    }

    private static Material FindMasterMaterial(out string chosenPath, out int candidateCount)
    {
        chosenPath = null;
        candidateCount = 0;

        var guids = AssetDatabase.FindAssets($"{MaterialName} t:Material");
        if (guids == null || guids.Length == 0)
            return null;

        var candidates = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => (path: p, mat: AssetDatabase.LoadAssetAtPath<Material>(p)))
            .Where(x => x.mat != null && string.Equals(x.mat.name, MaterialName, StringComparison.Ordinal))
            .OrderBy(x => x.path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        candidateCount = candidates.Length;
        if (candidateCount == 0)
            return null;

        chosenPath = candidates[0].path;
        return candidates[0].mat;
    }
}
#endif

