#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器工具：PathPreview 设置与材质创建。
/// </summary>
public static class PathPreviewSetup
{
    private const string PathMaterialPath = "Assets/Materials/PathPreviewLine.mat";

    [MenuItem(OperationMarigoldPaths.ToolsPathPreview + "/Setup")]
    public static void Setup()
    {
        EnsurePathPreviewManager();
    }

    [MenuItem(OperationMarigoldPaths.ToolsPathPreview + "/Create Material")]
    public static void CreatePathMaterial()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var shader = Shader.Find("PathPreview/LineGlow");
        if (shader == null)
            return;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.name = "PathPreviewLine";
            mat.SetColor("_Color", new Color(0.2f, 0.9f, 0.3f, 0.9f));
            mat.SetFloat("_SparkleSpeed", 2f);
            mat.SetFloat("_SparkleIntensity", 0.5f);
            AssetDatabase.CreateAsset(mat, PathMaterialPath);
            AssetDatabase.SaveAssets();
        }

        var mgr = Object.FindFirstObjectByType<PathPreviewManager>();
        if (mgr != null)
        {
            var so = new SerializedObject(mgr);
            so.FindProperty("pathMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsurePathPreviewManager()
    {
        var existing = Object.FindFirstObjectByType<PathPreviewManager>();
        if (existing != null) return;

        var go = new GameObject("PathPreviewManager");
        var comp = go.AddComponent<PathPreviewManager>();

        var mapRoot = Object.FindFirstObjectByType<MapRoot>();
        if (mapRoot != null)
        {
            var so = new SerializedObject(comp);
            so.FindProperty("mapRoot").objectReferenceValue = mapRoot;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PathMaterialPath);
            if (mat != null)
                so.FindProperty("pathMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.RegisterCreatedObjectUndo(go, "Add PathPreviewManager");
    }
}
#endif
