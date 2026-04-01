#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 编辑器工具：为场景中 MapRoot 下的建筑放置物补齐 BuildingController（若缺失）。
/// </summary>
public static class EnsureBuildingControllersInMapRootsTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsScene + "/补齐地图建筑 BuildingController";
    private const string UndoLabel = "Ensure BuildingController On Buildings";
    private static readonly string[] BuildingHintTokens = { "building", "city", "factory", "hq", "airport", "port", "lab", "commtower", "silo" };

    [MenuItem(MenuPath)]
    private static void EnsureBuildingControllers()
    {
        var lookup = BuildBuildingDataLookup();

        var mapRoots = UnityEngine.Object.FindObjectsByType<MapRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scannedMapRootCount = 0;
        var scannedCellCount = 0;
        var buildingCandidateCount = 0;
        var addedCount = 0;
        var autoFilledDataCount = 0;
        var ownerSetToNoneCount = 0;
        var noMatchedDataCount = 0;
        var skippedNoPlaceableInstance = 0;

        for (var i = 0; i < mapRoots.Length; i++)
        {
            var mapRoot = mapRoots[i];
            if (mapRoot == null || EditorUtility.IsPersistent(mapRoot))
                continue;

            scannedMapRootCount++;
            var cells = mapRoot.GetComponentsInChildren<Cell>(true);

            for (var j = 0; j < cells.Length; j++)
            {
                var cell = cells[j];
                if (cell == null)
                    continue;

                scannedCellCount++;
                if (cell.Building != null)
                {
                    // 已有 BuildingController 也纳入后续自动填充/默认阵营修复流程。
                }

                var placeableRoot = FindPlaceableRoot(cell);
                if (placeableRoot == null)
                {
                    skippedNoPlaceableInstance++;
                    continue;
                }

                if (!IsBuildingCandidate(cell, placeableRoot))
                    continue;

                buildingCandidateCount++;

                var building = cell.Building;
                if (building == null)
                {
                    building = Undo.AddComponent<BuildingController>(placeableRoot.gameObject);
                    addedCount++;
                }

                if (building == null)
                    continue;

                var changed = false;
                if (TryAutoFillBuildingData(building, placeableRoot, cell, lookup))
                {
                    autoFilledDataCount++;
                    changed = true;
                }
                else if (GetSerializedBuildingData(building) == null)
                {
                    noMatchedDataCount++;
                }

                if (TrySetInitialOwnerNone(building))
                {
                    ownerSetToNoneCount++;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(building);
                    EditorUtility.SetDirty(placeableRoot.gameObject);
                    if (placeableRoot.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(placeableRoot.gameObject.scene);
                }
            }
        }

        var message =
            $"扫描 MapRoot: {scannedMapRootCount}\n" +
            $"扫描 Cell: {scannedCellCount}\n" +
            $"建筑候选格: {buildingCandidateCount}\n" +
            $"新增 BuildingController: {addedCount}\n" +
            $"自动填充 BuildingData: {autoFilledDataCount}\n" +
            $"默认阵营设为 None: {ownerSetToNoneCount}\n" +
            $"未匹配到 BuildingData: {noMatchedDataCount}\n" +
            $"跳过（未找到建筑实例）: {skippedNoPlaceableInstance}";

        EditorUtility.DisplayDialog("补齐完成", message, "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateEnsureBuildingControllers()
    {
        var mapRoots = UnityEngine.Object.FindObjectsByType<MapRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < mapRoots.Length; i++)
        {
            if (mapRoots[i] != null && !EditorUtility.IsPersistent(mapRoots[i]))
                return true;
        }

        return false;
    }

    private static Transform FindPlaceableRoot(Cell cell)
    {
        if (cell == null || cell.PlaceableType == null || cell.PlaceableType.prefab == null)
            return null;

        var expectedPrefabName = cell.PlaceableType.prefab.name;
        Transform fallback = null;

        for (var i = 0; i < cell.transform.childCount; i++)
        {
            var child = cell.transform.GetChild(i);
            if (child == null)
                continue;

            if (fallback == null)
                fallback = child;

            var strippedName = child.name.Replace("(Clone)", string.Empty).Trim();
            if (strippedName == expectedPrefabName)
                return child;
        }

        return fallback;
    }

    private static bool IsBuildingCandidate(Cell cell, Transform placeableRoot)
    {
        var keys = BuildLookupKeys(placeableRoot, cell);
        for (var i = 0; i < keys.Count; i++)
        {
            var n = Normalize(keys[i]);
            if (string.IsNullOrEmpty(n))
                continue;
            for (var t = 0; t < BuildingHintTokens.Length; t++)
            {
                if (n.Contains(BuildingHintTokens[t], StringComparison.Ordinal))
                    return true;
            }
        }

        // 显式配置了 buildingData 的也视为建筑。
        return cell != null && cell.PlaceableType != null && cell.PlaceableType.buildingData != null;
    }

    private static bool TryAutoFillBuildingData(
        BuildingController building,
        Transform placeableRoot,
        Cell cell,
        BuildingDataLookup lookup)
    {
        if (building == null || lookup == null)
            return false;
        if (GetSerializedBuildingData(building) != null)
            return false;

        var best = lookup.FindBestMatch(BuildLookupKeys(placeableRoot, cell));
        if (best == null)
            return false;

        Undo.RecordObject(building, UndoLabel);
        var so = new SerializedObject(building);
        var dataProp = so.FindProperty("_data");
        if (dataProp == null)
            return false;

        dataProp.objectReferenceValue = best;
        so.ApplyModifiedProperties();
        return true;
    }

    private static BuildingData GetSerializedBuildingData(BuildingController building)
    {
        if (building == null)
            return null;
        var so = new SerializedObject(building);
        var dataProp = so.FindProperty("_data");
        return dataProp != null ? dataProp.objectReferenceValue as BuildingData : null;
    }

    private static bool TrySetInitialOwnerNone(BuildingController building)
    {
        if (building == null)
            return false;

        var so = new SerializedObject(building);
        var ownerProp = so.FindProperty("_initialOwnerFaction");
        if (ownerProp == null)
            return false;

        if (ownerProp.intValue == (int)UnitFaction.None)
            return false;

        Undo.RecordObject(building, UndoLabel);
        ownerProp.intValue = (int)UnitFaction.None;
        so.ApplyModifiedProperties();
        return true;
    }

    private static List<string> BuildLookupKeys(Transform placeableRoot, Cell cell)
    {
        var keys = new List<string>();
        if (placeableRoot != null)
            keys.Add(placeableRoot.name);
        if (cell != null)
        {
            keys.Add(cell.name);
            if (cell.PlaceableType != null)
            {
                keys.Add(cell.PlaceableType.name);
                keys.Add(cell.PlaceableType.id);
                keys.Add(cell.PlaceableType.displayName);
                if (cell.PlaceableType.prefab != null)
                    keys.Add(cell.PlaceableType.prefab.name);
            }
        }
        return keys;
    }

    private static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var stripped = raw.Trim().ToLowerInvariant();
        stripped = stripped.Replace("(clone)", string.Empty);

        var chars = new List<char>(stripped.Length);
        for (var i = 0; i < stripped.Length; i++)
        {
            var c = stripped[i];
            if (char.IsLetterOrDigit(c))
                chars.Add(c);
            else
                chars.Add('_');
        }

        var normalized = new string(chars.ToArray());
        while (normalized.IndexOf("__", StringComparison.Ordinal) >= 0)
            normalized = normalized.Replace("__", "_");
        return normalized.Trim('_');
    }

    private static string ExtractCoreToken(string raw)
    {
        var n = Normalize(raw);
        if (string.IsNullOrEmpty(n))
            return string.Empty;

        if (n.StartsWith("building_", StringComparison.Ordinal))
            n = n.Substring("building_".Length);

        var parts = n.Split('_');
        return parts.Length == 0 ? n : parts[parts.Length - 1];
    }

    private static BuildingDataLookup BuildBuildingDataLookup()
    {
        var guids = AssetDatabase.FindAssets("t:BuildingData");
        var all = new List<BuildingData>();
        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data != null)
                all.Add(data);
        }

        return new BuildingDataLookup(all);
    }

    private sealed class BuildingDataLookup
    {
        private readonly List<BuildingData> _all;
        private readonly Dictionary<string, BuildingData> _exactMap;
        private readonly Dictionary<string, BuildingData> _tokenMap;

        public BuildingDataLookup(List<BuildingData> all)
        {
            _all = all ?? new List<BuildingData>();
            _exactMap = new Dictionary<string, BuildingData>(StringComparer.OrdinalIgnoreCase);
            _tokenMap = new Dictionary<string, BuildingData>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < _all.Count; i++)
            {
                var data = _all[i];
                if (data == null)
                    continue;

                AddExact(data.name, data);
                AddExact(data.id, data);
                AddExact(data.displayName, data);

                AddToken(data.name, data);
                AddToken(data.id, data);
                AddToken(data.displayName, data);
            }
        }

        public BuildingData FindBestMatch(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
                return null;

            // 1) 强匹配：完全规范化一致（例如 Building.City）
            for (var i = 0; i < keys.Count; i++)
            {
                var n = Normalize(keys[i]);
                if (string.IsNullOrEmpty(n))
                    continue;
                if (_exactMap.TryGetValue(n, out var exact))
                    return exact;
            }

            // 2) 规则匹配：核心 token（例如 city/factory/hq）
            for (var i = 0; i < keys.Count; i++)
            {
                var token = ExtractCoreToken(keys[i]);
                if (string.IsNullOrEmpty(token))
                    continue;
                if (_tokenMap.TryGetValue(token, out var tokenMatch))
                    return tokenMatch;
            }

            // 3) 兜底：包含关系最短优先，避免无结果
            BuildingData best = null;
            var bestLen = int.MaxValue;
            for (var i = 0; i < keys.Count; i++)
            {
                var n = Normalize(keys[i]);
                if (string.IsNullOrEmpty(n))
                    continue;
                for (var j = 0; j < _all.Count; j++)
                {
                    var data = _all[j];
                    if (data == null)
                        continue;
                    var candidates = new[] { Normalize(data.name), Normalize(data.id), Normalize(data.displayName) };
                    for (var k = 0; k < candidates.Length; k++)
                    {
                        var c = candidates[k];
                        if (string.IsNullOrEmpty(c))
                            continue;
                        if (c.Contains(n, StringComparison.Ordinal) || n.Contains(c, StringComparison.Ordinal))
                        {
                            if (c.Length < bestLen)
                            {
                                best = data;
                                bestLen = c.Length;
                            }
                        }
                    }
                }
            }

            return best;
        }

        private void AddExact(string key, BuildingData data)
        {
            var n = Normalize(key);
            if (string.IsNullOrEmpty(n))
                return;
            if (!_exactMap.ContainsKey(n))
                _exactMap.Add(n, data);
        }

        private void AddToken(string key, BuildingData data)
        {
            var token = ExtractCoreToken(key);
            if (string.IsNullOrEmpty(token))
                return;
            if (!_tokenMap.ContainsKey(token))
                _tokenMap.Add(token, data);
        }
    }
}
#endif
