#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将选中文件夹下的 PNG 纹理批量转换为 Sprite 导入类型。
/// </summary>
public static class BatchConvertPngToSpriteTool
{
    private const string MenuPath = OperationMarigoldPaths.ToolsTexture + "/Batch Convert PNG To Sprite";

    [MenuItem(MenuPath)]
    private static void ConvertSelectedFolderPngToSprite()
    {
        var folderPath = GetSelectedFolderPath();
        if (!IsAssetsFolder(folderPath))
        {
            EditorUtility.DisplayDialog("无效选择", "请先在 Project 面板中选中一个 Assets 下的文件夹。", "确定");
            return;
        }

        var pngPaths = CollectPngPaths(folderPath);
        if (pngPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("未找到 PNG", $"在目录 `{folderPath}` 下未找到 PNG 资源。", "确定");
            return;
        }

        var convertedCount = 0;
        var alreadySpriteCount = 0;
        var invalidImporterCount = 0;

        try
        {
            for (var i = 0; i < pngPaths.Count; i++)
            {
                var path = pngPaths[i];
                EditorUtility.DisplayProgressBar("批量转换 PNG 为 Sprite", $"处理中: {path}", (i + 1f) / pngPaths.Count);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    invalidImporterCount++;
                    continue;
                }

                var changed = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    changed = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }

                if (importer.alphaIsTransparency != true)
                {
                    importer.alphaIsTransparency = true;
                    changed = true;
                }

                if (!changed)
                {
                    alreadySpriteCount++;
                    continue;
                }

                AssetDatabase.WriteImportSettingsIfDirty(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                convertedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();

        var message =
            $"目标目录: {folderPath}\n" +
            $"PNG 总数: {pngPaths.Count}\n" +
            $"成功转换: {convertedCount}\n" +
            $"原本已是 Sprite: {alreadySpriteCount}\n" +
            $"无效导入器: {invalidImporterCount}";

        EditorUtility.DisplayDialog("完成", message, "确定");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateConvertSelectedFolderPngToSprite()
    {
        return IsAssetsFolder(GetSelectedFolderPath());
    }

    private static List<string> CollectPngPaths(string folderPath)
    {
        var result = new List<string>();
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(path);
        }

        return result;
    }

    private static string GetSelectedFolderPath()
    {
        var selected = Selection.activeObject;
        if (selected == null)
            return string.Empty;

        var path = AssetDatabase.GetAssetPath(selected);
        if (!IsAssetsFolder(path))
            return string.Empty;

        return path;
    }

    private static bool IsAssetsFolder(string path)
    {
        return !string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(path);
    }
}
#endif
