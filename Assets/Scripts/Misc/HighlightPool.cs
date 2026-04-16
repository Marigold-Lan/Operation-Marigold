using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基于栈的 GameObject 对象池，专为 HighlightManager 高光系统设计。
/// </summary>
public class HighlightPool
{
    private readonly Stack<GameObject> _available = new Stack<GameObject>();
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly int _maxPoolSize;
    private readonly string _prefabTag;

    public HighlightPool(GameObject prefab, Transform parent, int initialSize = 0, int maxPoolSize = 200)
    {
        _prefab = prefab;
        _parent = parent;
        _maxPoolSize = maxPoolSize;
        _prefabTag = prefab != null ? prefab.name : "Null";

        if (prefab == null) return;
        for (var i = 0; i < initialSize; i++)
        {
            var go = CreateNew();
            go.SetActive(false);
        }
    }

    public GameObject Get(Vector3 position)
    {
        if (_prefab == null) return null;

        GameObject go;
        if (_available.Count > 0)
        {
            go = _available.Pop();
        }
        else
        {
            go = CreateNew();
        }

        go.transform.position = position;
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        if (_available.Count < _maxPoolSize)
            _available.Push(go);
        else
            Object.Destroy(go);
    }

    public void ReleaseAll(List<GameObject> list)
    {
        if (list == null) return;
        foreach (var go in list)
            Release(go);
        list.Clear();
    }

    /// <summary>
    /// 尝试归还到本池：仅当 GameObject 来自本池对应的 prefab 时才归还。
    /// 用于混合池场景（如攻击高光同时来自 enemy/other 两个池）。
    /// </summary>
    public void ReleaseIfFromPool(GameObject go)
    {
        if (go == null) return;
        if (go.name == _prefabTag || go.name == _prefabTag + "(Clone)")
            Release(go);
    }

    public void Clear()
    {
        foreach (var go in _available)
            Object.Destroy(go);
        _available.Clear();
    }

    private GameObject CreateNew()
    {
        return Object.Instantiate(_prefab, _parent);
    }
}
