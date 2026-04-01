#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 扫描单位 Prefab 的能力配置一致性：
/// 1) 缺少 UnitCapture
/// 2) APC 命名不规范
/// 3) 缺少 UnitSupply
/// </summary>
public class UnitCapabilityConfigCheckerWindow : EditorWindow
{
    private const string DefaultUnitFolder = "Assets/Prefabs/Unit";
    private const string ApcNamePattern = "^(Marigold|Lancel)_APC$";

    private DefaultAsset _unitPrefabFolder;
    private bool _includeSubfolders = true;
    private Vector2 _scroll;

    private readonly List<ResultItem> _missingCapture = new();
    private readonly List<ResultItem> _apcNamingInvalid = new();
    private readonly List<ResultItem> _missingSupply = new();

    private int _scannedUnitPrefabCount;
    private int _allPrefabCount;

    [MenuItem(OperationMarigoldPaths.ToolsPrefab + "/能力配置检查器")]
    private static void ShowWindow()
    {
        var window = GetWindow<UnitCapabilityConfigCheckerWindow>("能力配置检查器");
        window.minSize = new Vector2(680f, 460f);
    }

    private void OnEnable()
    {
        if (_unitPrefabFolder == null)
        {
            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultUnitFolder);
            if (folderAsset != null)
                _unitPrefabFolder = folderAsset;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("单位能力配置检查器", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        _unitPrefabFolder = (DefaultAsset)EditorGUILayout.ObjectField("单位 Prefab 文件夹", _unitPrefabFolder, typeof(DefaultAsset), false);
        _includeSubfolders = EditorGUILayout.ToggleLeft("包含子文件夹", _includeSubfolders);
        EditorGUILayout.HelpBox(
            "扫描规则：\n" +
            "1) 仅检查包含 UnitController 的 Prefab\n" +
            "2) 检查是否缺少 UnitCapture\n" +
            "3) 若识别为 APC（名称或 UnitData 包含 APC），检查命名是否符合 ^(Marigold|Lancel)_APC$\n" +
            "4) 检查是否缺少 UnitSupply",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!CanScan()))
        {
            if (GUILayout.Button("一键扫描所有单位 Prefab", GUILayout.Height(34f)))
                ScanAllUnitPrefabs();
        }

        EditorGUILayout.Space(8f);
        DrawSummary();
        EditorGUILayout.Space(6f);
        DrawResultLists();
    }

    private bool CanScan()
    {
        var folderPath = GetFolderPath(_unitPrefabFolder);
        return IsAssetsFolder(folderPath);
    }

    private void ScanAllUnitPrefabs()
    {
        _missingCapture.Clear();
        _apcNamingInvalid.Clear();
        _missingSupply.Clear();
        _scannedUnitPrefabCount = 0;
        _allPrefabCount = 0;

        var folderPath = GetFolderPath(_unitPrefabFolder);
        if (!IsAssetsFolder(folderPath))
        {
            EditorUtility.DisplayDialog("路径无效", "请选择 Assets 下的有效文件夹。", "确定");
            return;
        }

        var prefabPaths = CollectPrefabPathsFromFolder(folderPath);
        _allPrefabCount = prefabPaths.Count;

        for (var i = 0; i < prefabPaths.Count; i++)
        {
            var path = prefabPaths[i];
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var unit = root.GetComponentInChildren<UnitController>(true);
                if (unit == null)
                    continue;

                _scannedUnitPrefabCount++;
                var unitGo = unit.gameObject;
                var prefabName = root.name;

                if (unitGo.GetComponent<UnitCapture>() == null)
                    _missingCapture.Add(new ResultItem(path, prefabName, "缺少 UnitCapture"));

                if (unitGo.GetComponent<UnitSupply>() == null)
                    _missingSupply.Add(new ResultItem(path, prefabName, "缺少 UnitSupply"));

                if (IsApcUnit(unit, prefabName) && !IsApcNameStandard(prefabName))
                    _apcNamingInvalid.Add(new ResultItem(path, prefabName, "APC 命名不规范"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        var msg =
            $"扫描完成。\n" +
            $"总 Prefab: {_allPrefabCount}\n" +
            $"单位 Prefab: {_scannedUnitPrefabCount}\n\n" +
            $"缺 UnitCapture: {_missingCapture.Count}\n" +
            $"APC 命名不规范: {_apcNamingInvalid.Count}\n" +
            $"缺 UnitSupply: {_missingSupply.Count}";

        EditorUtility.DisplayDialog("能力配置检查完成", msg, "确定");
        Repaint();
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("结果统计", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"已扫描单位 Prefab: {_scannedUnitPrefabCount} / 总 Prefab: {_allPrefabCount}");
        EditorGUILayout.LabelField($"缺 UnitCapture: {_missingCapture.Count}");
        EditorGUILayout.LabelField($"APC 命名不规范: {_apcNamingInvalid.Count}");
        EditorGUILayout.LabelField($"缺 UnitSupply: {_missingSupply.Count}");
    }

    private void DrawResultLists()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawSection("缺少 UnitCapture", _missingCapture);
        EditorGUILayout.Space(8f);
        DrawSection("APC 命名不规范", _apcNamingInvalid);
        EditorGUILayout.Space(8f);
        DrawSection("缺少 UnitSupply", _missingSupply);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawSection(string title, List<ResultItem> items)
    {
        EditorGUILayout.LabelField($"{title}（{items.Count}）", EditorStyles.boldLabel);
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField("无");
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel($"{item.PrefabName}    [{item.AssetPath}]", GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("定位", GUILayout.Width(56f)))
                    PingAsset(item.AssetPath);
            }
        }
    }

    private static bool IsApcUnit(UnitController unit, string prefabName)
    {
        if (ContainsApc(prefabName))
            return true;

        var data = unit != null ? unit.Data : null;
        if (data == null)
            return false;

        return ContainsApc(data.name) || ContainsApc(data.id) || ContainsApc(data.displayName);
    }

    private static bool ContainsApc(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf("APC", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsApcNameStandard(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(prefabName, ApcNamePattern);
    }

    private static void PingAsset(string assetPath)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (asset == null)
            return;
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

    private List<string> CollectPrefabPathsFromFolder(string folderPath)
    {
        var result = new List<string>();
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            if (!_includeSubfolders && GetParentFolder(path) != folderPath)
                continue;
            result.Add(path);
        }

        return result;
    }

    private static string GetFolderPath(DefaultAsset folderAsset)
    {
        return folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : string.Empty;
    }

    private static bool IsAssetsFolder(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(path);
    }

    private static string GetParentFolder(string assetPath)
    {
        var slash = assetPath.LastIndexOf('/');
        return slash > 0 ? assetPath.Substring(0, slash) : string.Empty;
    }

    private readonly struct ResultItem
    {
        public ResultItem(string assetPath, string prefabName, string issue)
        {
            AssetPath = assetPath;
            PrefabName = prefabName;
            Issue = issue;
        }

        public string AssetPath { get; }
        public string PrefabName { get; }
        public string Issue { get; }
    }
}
#endif
