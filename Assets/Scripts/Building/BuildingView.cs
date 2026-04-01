using UnityEngine;

/// <summary>
/// 建筑表现层：监听 BuildingController 的阵营事件并切换 Mesh。
/// </summary>
public class BuildingView : MonoBehaviour
{
    [SerializeField] private BuildingController _controller;
    [SerializeField] private MeshFilter _meshFilter;

    private bool _isSubscribed;
    private Mesh _originalMesh;
    private bool _cachedOriginalMesh;

    public void Bind(BuildingController controller)
    {
        if (_controller == controller) return;
        Unsubscribe();
        _controller = controller;
        Subscribe();
        RefreshNow();
    }

    private void Awake()
    {
        if (_meshFilter == null)
            _meshFilter = GetComponentInChildren<MeshFilter>();
        CacheOriginalMeshIfNeeded();
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_isSubscribed || _controller == null) return;
        _controller.OnOwnerFactionSet += HandleOwnerFactionSet;
        _controller.OnOwnerFactionChanged += HandleOwnerFactionChanged;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _controller == null) return;
        _controller.OnOwnerFactionSet -= HandleOwnerFactionSet;
        _controller.OnOwnerFactionChanged -= HandleOwnerFactionChanged;
        _isSubscribed = false;
    }

    private void HandleOwnerFactionSet(UnitFaction faction)
    {
        ApplyMeshForFaction(faction);
    }

    private void HandleOwnerFactionChanged(UnitFaction oldFaction, UnitFaction newFaction)
    {
        ApplyMeshForFaction(newFaction);
    }

    private void RefreshNow()
    {
        if (_controller == null) return;
        ApplyMeshForFaction(_controller.OwnerFaction);
    }

    private void CacheOriginalMeshIfNeeded()
    {
        if (_cachedOriginalMesh) return;
        if (_meshFilter != null)
            _originalMesh = _meshFilter.sharedMesh;
        _cachedOriginalMesh = true;
    }

    private void ApplyMeshForFaction(UnitFaction faction)
    {
        if (_controller == null || _meshFilter == null) return;
        CacheOriginalMeshIfNeeded();

        var data = _controller.Data;
        if (data != null && data.TryGetMesh(faction, out var mappedMesh))
        {
            _meshFilter.sharedMesh = mappedMesh;
            return;
        }

        if (faction == UnitFaction.None && data != null && data.defaultMesh != null)
        {
            _meshFilter.sharedMesh = data.defaultMesh;
            return;
        }

        _meshFilter.sharedMesh = _originalMesh;
    }
}
