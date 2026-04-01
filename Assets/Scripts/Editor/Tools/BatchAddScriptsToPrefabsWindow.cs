#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量给一组 Prefab 添加相同的多个脚本组件。
/// </summary>
public class BatchAddScriptsToPrefabsWindow : EditorWindow
{
    private enum PrefabSourceMode
    {
        Folder,
        ManualList
    }

    private PrefabSourceMode _sourceMode = PrefabSourceMode.Folder;
    private DefaultAsset _prefabFolder;
    private bool _includeSubfolders = true;
    private bool _allowDuplicateComponent = false;

    private readonly List<GameObject> _manualPrefabs = new();
    private readonly List<MonoScript> _scriptsToAdd = new();

    [MenuItem(OperationMarigoldPaths.ToolsPrefab + "/Batch Add Scripts To Prefabs")]
    private static void ShowWindow()
    {
        var window = GetWindow<BatchAddScriptsToPrefabsWindow>("批量添加脚本");
        window.minSize = new Vector2(560f, 380f);
    }

    private void OnEnable()
    {
        if (_scriptsToAdd.Count == 0)
            _scriptsToAdd.Add(null);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("给一组 Prefab 批量添加相同脚本", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        _sourceMode = (PrefabSourceMode)EditorGUILayout.EnumPopup("Prefab 来源", _sourceMode);
        _allowDuplicateComponent = EditorGUILayout.ToggleLeft("允许重复添加同类型组件", _allowDuplicateComponent);

        DrawPrefabSourceSection();

        EditorGUILayout.Space(6f);
        DrawScriptListSection();

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(!CanExecute()))
        {
            if (GUILayout.Button("一键批量添加", GUILayout.Height(32f)))
                ExecuteBatchAdd();
        }
    }

    private void DrawPrefabSourceSection()
    {
        EditorGUILayout.LabelField("Prefab 目标集", EditorStyles.boldLabel);

        if (_sourceMode == PrefabSourceMode.Folder)
        {
            _prefabFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab 文件夹", _prefabFolder, typeof(DefaultAsset), false);
            _includeSubfolders = EditorGUILayout.ToggleLeft("包含子文件夹", _includeSubfolders);
            EditorGUILayout.HelpBox("会遍历该文件夹下所有 Prefab 资源。", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("拖入需要处理的 Prefab（仅接受 Prefab 资源）。", MessageType.Info);
        var removeIndex = -1;
        for (var i = 0; i < _manualPrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _manualPrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"Prefab {i + 1}", _manualPrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("移除", GUILayout.Width(60f)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            _manualPrefabs.RemoveAt(removeIndex);

        if (GUILayout.Button("添加 Prefab 项"))
            _manualPrefabs.Add(null);
    }

    private void DrawScriptListSection()
    {
        EditorGUILayout.LabelField("要添加的脚本（多个）", EditorStyles.boldLabel);
        var removeIndex = -1;

        for (var i = 0; i < _scriptsToAdd.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _scriptsToAdd[i] = (MonoScript)EditorGUILayout.ObjectField($"脚本 {i + 1}", _scriptsToAdd[i], typeof(MonoScript), false);
            if (GUILayout.Button("移除", GUILayout.Width(60f)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            _scriptsToAdd.RemoveAt(removeIndex);

        if (GUILayout.Button("添加脚本项"))
            _scriptsToAdd.Add(null);
    }

    private bool CanExecute()
    {
        if (!HasValidScripts())
            return false;

        if (_sourceMode == PrefabSourceMode.Folder)
        {
            var folderPath = GetFolderPath(_prefabFolder);
            return IsAssetsFolder(folderPath);
        }

        return GetValidManualPrefabPaths().Count > 0;
    }

    private void ExecuteBatchAdd()
    {
        var scriptTypes = GetValidScriptTypes();
        if (scriptTypes.Count == 0)
        {
            EditorUtility.DisplayDialog("无有效脚本", "请选择至少一个继承 MonoBehaviour 的脚本。", "确定");
            return;
        }

        var prefabPaths = _sourceMode == PrefabSourceMode.Folder
            ? CollectPrefabPathsFromFolder()
            : GetValidManualPrefabPaths();

        if (prefabPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("未找到 Prefab", "没有可处理的 Prefab，请检查输入。", "确定");
            return;
        }

        var changedPrefabCount = 0;
        var addedComponentCount = 0;
        var skippedExistingCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (var i = 0; i < prefabPaths.Count; i++)
            {
                var path = prefabPaths[i];
                var root = PrefabUtility.LoadPrefabContents(path);
                var changed = false;

                try
                {
                    foreach (var type in scriptTypes)
                    {
                        if (!_allowDuplicateComponent && root.GetComponent(type) != null)
                        {
                            skippedExistingCount++;
                            continue;
                        }

                        root.AddComponent(type);
                        changed = true;
                        addedComponentCount++;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedPrefabCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var message =
            $"处理 Prefab 数: {prefabPaths.Count}\n" +
            $"发生修改的 Prefab 数: {changedPrefabCount}\n" +
            $"新增组件数: {addedComponentCount}\n" +
            $"已存在而跳过: {skippedExistingCount}";

        EditorUtility.DisplayDialog("完成", message, "确定");
    }

    private List<string> CollectPrefabPathsFromFolder()
    {
        var result = new List<string>();
        var folderPath = GetFolderPath(_prefabFolder);
        if (!IsAssetsFolder(folderPath))
            return result;

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            if (!_includeSubfolders && GetParentFolder(path) != folderPath)
                continue;

            result.Add(path);
        }

        return result;
    }

    private List<string> GetValidManualPrefabPaths()
    {
        var result = new List<string>();
        var dedup = new HashSet<string>(StringComparer.Ordinal);

        foreach (var prefab in _manualPrefabs)
        {
            if (prefab == null)
                continue;
            if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
                continue;

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path))
                continue;
            if (!dedup.Add(path))
                continue;
            result.Add(path);
        }

        return result;
    }

    private bool HasValidScripts()
    {
        for (var i = 0; i < _scriptsToAdd.Count; i++)
        {
            var script = _scriptsToAdd[i];
            if (script == null)
                continue;
            var type = script.GetClass();
            if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
                return true;
        }

        return false;
    }

    private List<Type> GetValidScriptTypes()
    {
        var result = new List<Type>();
        var dedup = new HashSet<Type>();

        for (var i = 0; i < _scriptsToAdd.Count; i++)
        {
            var script = _scriptsToAdd[i];
            if (script == null)
                continue;

            var type = script.GetClass();
            if (type == null)
                continue;
            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                continue;
            if (!dedup.Add(type))
                continue;

            result.Add(type);
        }

        return result;
    }

    private static string GetFolderPath(DefaultAsset folderAsset)
    {
        return folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : string.Empty;
    }

    private static bool IsAssetsFolder(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets") && AssetDatabase.IsValidFolder(path);
    }

    private static string GetParentFolder(string assetPath)
    {
        var slash = assetPath.LastIndexOf('/');
        return slash > 0 ? assetPath.Substring(0, slash) : string.Empty;
    }
}
#endif
