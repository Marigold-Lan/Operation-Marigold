#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 创建空父节点时保持子节点世界变换不变（子节点本地坐标变为 0,0,0）。
/// 解决默认 Make Empty Parent 将新父节点放在世界原点导致子节点坐标变化的问题。
/// </summary>
public static class MakeEmptyParentPreserveTransform
{
    private static bool _pending;

    [MenuItem(OperationMarigoldPaths.GameObjectRoot + "/Make Empty Parent (Preserve Transform)", false, 0)]
    public static void Execute()
    {
        if (_pending) return;
        _pending = true;
        var selected = Selection.gameObjects;
        EditorApplication.delayCall += () =>
        {
            _pending = false;
            ExecuteInternal(selected);
        };
    }

    private static void ExecuteInternal(GameObject[] selected)
    {
        if (selected == null || selected.Length == 0)
            return;

        var first = selected[0];
        var parentForNew = first.transform.parent;

        var newParent = new GameObject(first.name);
        Undo.RegisterCreatedObjectUndo(newParent, "Make Empty Parent (Preserve Transform)");

        newParent.transform.SetParent(parentForNew, false);
        newParent.transform.localPosition = first.transform.localPosition;
        newParent.transform.localRotation = first.transform.localRotation;
        newParent.transform.localScale = first.transform.localScale;

        for (int i = 0; i < selected.Length; i++)
        {
            var go = selected[i];
            if (go != null && go != newParent)
                Undo.SetTransformParent(go.transform, newParent.transform, "Make Empty Parent (Preserve Transform)");
        }

        if (selected.Length == 1)
        {
            var singleChild = selected[0];
            if (singleChild != null && singleChild.transform.parent == newParent.transform &&
                singleChild.GetComponent<MeshRenderer>() != null)
            {
                var childName = singleChild.name;
                Undo.RecordObject(newParent, "Make Empty Parent (Preserve Transform)");
                Undo.RecordObject(singleChild, "Make Empty Parent (Preserve Transform)");
                newParent.name = childName;
                singleChild.name = "mesh";
            }
        }

        Selection.activeGameObject = newParent;
        EditorGUIUtility.PingObject(newParent);
    }

    [MenuItem(OperationMarigoldPaths.GameObjectRoot + "/Make Empty Parent (Preserve Transform)", true)]
    public static bool Validate()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
#endif
