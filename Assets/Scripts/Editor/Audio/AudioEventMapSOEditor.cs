using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioEventMapSO))]
public class AudioEventMapSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var map = (AudioEventMapSO)target;

        EditorGUILayout.HelpBox(
            "一键补齐全部 GameAudioEvent 映射。\n" +
            "策略：若事件已配置有效 cueId，则跳过不改；仅补新增事件和 cueId=None 的占位项。",
            MessageType.Info);

        if (GUILayout.Button("一键补齐事件映射（有则跳过）"))
        {
            Undo.RecordObject(map, "Ensure Audio Event Mappings");
            map.EnsureAllEventMappings();
            EditorUtility.SetDirty(map);
        }

        EditorGUILayout.Space(8f);
        DrawDefaultInspector();
    }
}
