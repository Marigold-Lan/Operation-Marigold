#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 批量将文件夹内 Prefab 转换为 UnitData 蓝图。
/// </summary>
public class UnitDataBatchBuilderWindow : EditorWindow
{
    private const string DefaultSourceFolder = "Assets/Prefabs";
    private const string DefaultTargetFolder = "Assets/Blueprint/Unit";
    private const string MarigoldTargetFolder = "Assets/Blueprint/Unit/Marigold";
    private const string LancelTargetFolder = "Assets/Blueprint/Unit/Lancel";

    private DefaultAsset _sourceFolder;
    private DefaultAsset _targetFolder;
    private bool _includeSubfolders = true;
    private bool _overwriteExisting = true;

    [MenuItem(OperationMarigoldPaths.ToolsData + "/Batch Create UnitData From Prefabs")]
    public static void Open()
    {
        var window = GetWindow<UnitDataBatchBuilderWindow>("UnitData 批量生成");
        window.minSize = new Vector2(520f, 240f);
    }

    private void OnEnable()
    {
        _sourceFolder = LoadFolderAsset(DefaultSourceFolder);
        _targetFolder = LoadFolderAsset(DefaultTargetFolder);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("将一个文件夹下的 Prefab 批量转为 UnitData", EditorStyles.boldLabel);
        EditorGUILayout.Space(6f);

        _sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab 源文件夹", _sourceFolder, typeof(DefaultAsset), false);
        _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("UnitData 输出文件夹", _targetFolder, typeof(DefaultAsset), false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("输出到 Marigold"))
            _targetFolder = LoadFolderAsset(MarigoldTargetFolder);
        if (GUILayout.Button("输出到 Lancel"))
            _targetFolder = LoadFolderAsset(LancelTargetFolder);
        EditorGUILayout.EndHorizontal();

        _includeSubfolders = EditorGUILayout.ToggleLeft("包含子文件夹", _includeSubfolders);
        _overwriteExisting = EditorGUILayout.ToggleLeft("同名 UnitData 已存在时覆盖", _overwriteExisting);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "规则：\n" +
            "1) 生成的 UnitData 文件名 = Prefab 名称\n" +
            "2) id = Prefab 名称\n" +
            "3) displayName = Prefab 名称\n" +
            "4) prefab = 对应 Prefab",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!CanBuild()))
        {
            if (GUILayout.Button("一键生成 UnitData", GUILayout.Height(32f)))
                Build();
        }
    }

    private bool CanBuild()
    {
        var sourcePath = GetFolderPath(_sourceFolder);
        var targetPath = GetFolderPath(_targetFolder);
        return IsAssetsFolder(sourcePath) && IsAssetsFolder(targetPath);
    }

    private void Build()
    {
        var sourcePath = GetFolderPath(_sourceFolder);
        var targetPath = GetFolderPath(_targetFolder);

        if (!IsAssetsFolder(sourcePath))
        {
            EditorUtility.DisplayDialog("无效路径", "请指定有效的 Prefab 源文件夹（位于 Assets 下）。", "确定");
            return;
        }

        if (!EnsureFolderExists(targetPath))
        {
            EditorUtility.DisplayDialog("无效路径", "请指定有效的 UnitData 输出文件夹（位于 Assets 下）。", "确定");
            return;
        }

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { sourcePath });
        var created = 0;
        var updated = 0;
        var skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                if (!_includeSubfolders && GetDirectoryPath(prefabPath) != sourcePath)
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                var unitDataPath = $"{targetPath}/{prefab.name}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<UnitData>(unitDataPath);

                if (existing != null)
                {
                    if (!_overwriteExisting)
                    {
                        skipped++;
                        continue;
                    }

                    existing.id = prefab.name;
                    existing.displayName = prefab.name;
                    existing.prefab = prefab;
                    EditorUtility.SetDirty(existing);
                    updated++;
                    continue;
                }

                var asset = ScriptableObject.CreateInstance<UnitData>();
                asset.id = prefab.name;
                asset.displayName = prefab.name;
                asset.prefab = prefab;

                AssetDatabase.CreateAsset(asset, unitDataPath);
                created++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "完成",
            $"UnitData 批量生成完成。\n\n新建: {created}\n覆盖: {updated}\n跳过: {skipped}",
            "确定");
    }

    private static string GetFolderPath(DefaultAsset folderAsset)
    {
        return folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : string.Empty;
    }

    private static bool IsAssetsFolder(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets") && AssetDatabase.IsValidFolder(path);
    }

    private static string GetDirectoryPath(string assetPath)
    {
        var dir = Path.GetDirectoryName(assetPath);
        return string.IsNullOrEmpty(dir) ? string.Empty : dir.Replace("\\", "/");
    }

    private static DefaultAsset LoadFolderAsset(string folderPath)
    {
        if (EnsureFolderExists(folderPath))
            return AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        return null;
    }

    private static bool EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !folderPath.StartsWith("Assets"))
            return false;
        if (AssetDatabase.IsValidFolder(folderPath))
            return true;

        var parts = folderPath.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets")
            return false;

        var current = "Assets";
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        return AssetDatabase.IsValidFolder(folderPath);
    }
}
#endif
