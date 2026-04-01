#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从选中的 Prefab 一键创建 TileBaseType 或 TilePlaceableType 蓝图。
/// </summary>
public static class BlueprintFromPrefab
{
    private const string BaseFolder = "Assets/Blueprint/Tiles/Base";
    private const string PlaceableFolder = "Assets/Blueprint/Tiles/Placeable";

    [MenuItem(OperationMarigoldPaths.AssetsCreateRoot + "/Blueprint from Prefab (Base)", false, 0)]
    public static void CreateBaseBlueprint()
    {
        CreateBlueprint(isBase: true);
    }

    [MenuItem(OperationMarigoldPaths.AssetsCreateRoot + "/Blueprint from Prefab (Placeable)", false, 1)]
    public static void CreatePlaceableBlueprint()
    {
        CreateBlueprint(isBase: false);
    }

    [MenuItem(OperationMarigoldPaths.AssetsCreateRoot + "/Blueprint from Prefab (Base)", true)]
    [MenuItem(OperationMarigoldPaths.AssetsCreateRoot + "/Blueprint from Prefab (Placeable)", true)]
    public static bool ValidateCreateBlueprint()
    {
        return GetSelectedPrefab() != null;
    }

    [MenuItem(OperationMarigoldPaths.GameObjectRoot + "/Create Blueprint (Base)", false, 0)]
    public static void CreateBaseBlueprintContext()
    {
        CreateBlueprint(isBase: true);
    }

    [MenuItem(OperationMarigoldPaths.GameObjectRoot + "/Create Blueprint (Placeable)", false, 1)]
    public static void CreatePlaceableBlueprintContext()
    {
        CreateBlueprint(isBase: false);
    }

    [MenuItem(OperationMarigoldPaths.GameObjectRoot + "/Create Blueprint (Base)", true)]
    [MenuItem(OperationMarigoldPaths.GameObjectRoot + "/Create Blueprint (Placeable)", true)]
    public static bool ValidateCreateBlueprintContext()
    {
        return GetSelectedPrefab() != null;
    }

    private static GameObject GetSelectedPrefab()
    {
        var obj = Selection.activeObject;
        if (obj == null) return null;
        var go = obj as GameObject;
        if (go == null) return null;
        if (PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.NotAPrefab)
            return go;
        var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
        return prefabAsset;
    }

    private static void CreateBlueprint(bool isBase)
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null)
            return;

        var size = DetectXZSizeFromPrefab(prefab);
        if (size <= 0)
            return;

        var folder = isBase ? BaseFolder : PlaceableFolder;
        if (!AssetDatabase.IsValidFolder("Assets/Blueprint")) AssetDatabase.CreateFolder("Assets", "Blueprint");
        if (!AssetDatabase.IsValidFolder("Assets/Blueprint/Tiles")) AssetDatabase.CreateFolder("Assets/Blueprint", "Tiles");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Blueprint/Tiles", isBase ? "Base" : "Placeable");
        var name = prefab.name;
        var path = $"{folder}/{name}.asset";
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        if (isBase)
        {
            var asset = ScriptableObject.CreateInstance<TileBaseType>();
            asset.id = name;
            asset.displayName = name;
            asset.prefab = prefab;
            asset.cellSize = size;
            asset.prefabNativeSize = size;

            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Base Blueprint");
        }
        else
        {
            var asset = ScriptableObject.CreateInstance<TilePlaceableType>();
            asset.id = name;
            asset.displayName = name;
            asset.prefab = prefab;
            asset.prefabNativeSize = size;

            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create Placeable Blueprint");
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    private static float DetectXZSizeFromPrefab(GameObject prefab)
    {
        if (prefab == null) return 0f;

        var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (temp == null) return 0f;

        var rends = temp.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0)
        {
            Object.DestroyImmediate(temp);
            return 0f;
        }

        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        Object.DestroyImmediate(temp);
        return Mathf.Max(b.size.x, b.size.z, 0.001f);
    }
}
#endif
