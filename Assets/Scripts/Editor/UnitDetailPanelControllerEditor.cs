#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitDetailPanelController))]
[CanEditMultipleObjects]
public class UnitDetailPanelControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("快捷工具", EditorStyles.boldLabel);

        if (GUILayout.Button("一键自动抓取层级引用"))
        {
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not UnitDetailPanelController controller)
                    continue;

                Undo.RecordObject(controller, "Auto Bind UnitDetailPanel References");
                controller.AutoBindHierarchyReferences();
                EditorUtility.SetDirty(controller);
            }
        }
    }
}
#endif
