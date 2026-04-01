#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnitController))]
[CanEditMultipleObjects]
public class UnitControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("配置工具", EditorStyles.boldLabel);

        if (GUILayout.Button("按物体名一键匹配 UnitData"))
            AutoAssignUnitDataForTargets();
        if (GUILayout.Button("按名称前缀一键匹配阵营"))
            AutoAssignFactionForTargets();
        if (GUILayout.Button("一键配置（UnitData + 阵营）"))
            AutoAssignAllForTargets();
    }

    private void AutoAssignUnitDataForTargets()
    {
        var success = 0;
        var failed = 0;

        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not UnitController controller)
                continue;

            Undo.RecordObject(controller, "Auto Assign UnitData By Name");
            if (controller.TryAutoAssignUnitDataByName())
                success++;
            else
                failed++;
        }

        serializedObject.Update();
        serializedObject.ApplyModifiedProperties();
    }

    private void AutoAssignFactionForTargets()
    {
        var success = 0;
        var failed = 0;

        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not UnitController controller)
                continue;

            Undo.RecordObject(controller, "Auto Assign Faction By Name Prefix");
            if (controller.TryAutoAssignFactionByNamePrefix())
                success++;
            else
                failed++;
        }

        serializedObject.Update();
        serializedObject.ApplyModifiedProperties();
    }

    private void AutoAssignAllForTargets()
    {
        var unitDataSuccess = 0;
        var factionSuccess = 0;

        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] is not UnitController controller)
                continue;

            Undo.RecordObject(controller, "Auto Assign UnitData And Faction");
            if (controller.TryAutoAssignUnitDataByName())
                unitDataSuccess++;
            if (controller.TryAutoAssignFactionByNamePrefix())
                factionSuccess++;
        }

        serializedObject.Update();
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
