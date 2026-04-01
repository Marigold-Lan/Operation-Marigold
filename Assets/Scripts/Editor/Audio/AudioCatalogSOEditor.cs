using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioCatalogSO))]
public class AudioCatalogSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var catalog = (AudioCatalogSO)target;

        EditorGUILayout.HelpBox(
            "简化配置流程：\n" +
            "1) 先点「一键补齐所有 Cue 条目」\n" +
            "2) 再点「一键套用默认参数」\n" +
            "3) 最后只需要给每个 Cue 填 clips（其余参数可按需微调）",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("一键补齐所有 Cue 条目"))
            {
                Undo.RecordObject(catalog, "Ensure Audio Cue Entries");
                catalog.EnsureAllCueIdEntries();
                EditorUtility.SetDirty(catalog);
            }

            if (GUILayout.Button("一键套用默认参数（有则跳过）"))
            {
                Undo.RecordObject(catalog, "Apply Audio Defaults");
                catalog.ApplyDefaultParamsToEntries(onlyIfUnconfigured: true);
                EditorUtility.SetDirty(catalog);
            }
        }

        if (GUILayout.Button("一键补齐并套用默认参数（有则跳过）"))
        {
            Undo.RecordObject(catalog, "Initialize Audio Catalog");
            catalog.EnsureAndApplyDefaults();
            EditorUtility.SetDirty(catalog);
        }

        EditorGUILayout.Space(8f);
        DrawDefaultInspector();
    }
}
