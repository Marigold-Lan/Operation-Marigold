#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 删除层级中名字后缀为 Snow 或 Desert 的物体。
/// </summary>
public static class DeleteSuffixObjects
{
    private static readonly string[] Suffixes = { "Snow", "Desert" };

    [MenuItem(OperationMarigoldPaths.ToolsCleanup + "/Delete Suffix Objects")]
    private static void DeleteAllMatching()
    {
        var candidates = FindCandidatesAcrossLoadedScenes(includeInactive: true);
        var toDelete = FilterOutChildrenWhenParentAlsoDeleted(candidates);

        if (toDelete.Count == 0)
        {
            EditorUtility.DisplayDialog("层级清理", "没有找到名字后缀为 Snow 或 Desert 的物体。", "OK");
            return;
        }

        var message =
            $"将删除 {toDelete.Count} 个物体（名字后缀为 Snow 或 Desert）。\n\n" +
            "此操作可通过 Undo 撤销。";

        if (!EditorUtility.DisplayDialog("确认删除", message, "删除", "取消"))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Delete *Snow/*Desert suffix objects");
        var group = Undo.GetCurrentGroup();

        foreach (var go in toDelete)
        {
            if (go == null) continue;
            Undo.DestroyObjectImmediate(go);
        }

        Undo.CollapseUndoOperations(group);
    }

    private static List<GameObject> FindCandidatesAcrossLoadedScenes(bool includeInactive)
    {
        var results = new List<GameObject>(256);

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                CollectMatchesDepthFirst(root.transform, includeInactive, results);
            }
        }

        return results;
    }

    private static void CollectMatchesDepthFirst(Transform t, bool includeInactive, List<GameObject> results)
    {
        if (t == null) return;

        var go = t.gameObject;
        if (go != null && (includeInactive || go.activeInHierarchy))
        {
            if (HasTargetSuffix(go.name))
                results.Add(go);
        }

        for (int i = 0; i < t.childCount; i++)
        {
            CollectMatchesDepthFirst(t.GetChild(i), includeInactive, results);
        }
    }

    private static bool HasTargetSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        for (int i = 0; i < Suffixes.Length; i++)
        {
            var suffix = Suffixes[i];
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static List<GameObject> FilterOutChildrenWhenParentAlsoDeleted(List<GameObject> candidates)
    {
        if (candidates == null || candidates.Count == 0) return new List<GameObject>();

        var set = new HashSet<Transform>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;
            set.Add(candidates[i].transform);
        }

        var filtered = new List<GameObject>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var go = candidates[i];
            if (go == null) continue;

            var t = go.transform;
            bool ancestorAlsoDeleted = false;

            var p = t.parent;
            while (p != null)
            {
                if (set.Contains(p))
                {
                    ancestorAlsoDeleted = true;
                    break;
                }
                p = p.parent;
            }

            if (!ancestorAlsoDeleted)
                filtered.Add(go);
        }

        return filtered;
    }
}
#endif
