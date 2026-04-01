using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径预览管理器。当选中单位且光标在可达范围内时，用 LineRenderer 绘制从单位到光标的 A* 路径。
/// </summary>
public class PathPreviewManager : Singleton<PathPreviewManager>
{
    [Header("依赖")]
    public MapRoot mapRoot;

    [Header("LineRenderer")]
    [Tooltip("路径线条，留空则自动添加")]
    public LineRenderer lineRenderer;

    [Tooltip("路径材质（需支持顶点色，留空则自动创建）")]
    public Material pathMaterial;

    [Tooltip("路径线宽（首尾相同）")]
    public float lineWidth = 0.1f;

    [Tooltip("拐角平滑度（顶点数，0=锐角，越大越圆滑）")]
    [Range(0, 16)]
    public int cornerVertices = 8;

    [Tooltip("路径颜色")]
    public Color lineColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);

    [Tooltip("路径相对格子中心的 Y 偏移")]
    public float heightOffset = 0.05f;

    private UnitController _cachedUnit;
    private HashSet<Vector2Int> _cachedReachable;

    private void OnEnable()
    {
        SelectionManager.OnSelectedCellChanged += HandleSelectedCellChanged;
        GridCursor.OnCursorCoordChanged += HandleCursorCoordChanged;

        if (mapRoot == null)
            mapRoot = MapRoot.Instance;
        EnsureLineRenderer();
    }

    private void OnDisable()
    {
        SelectionManager.OnSelectedCellChanged -= HandleSelectedCellChanged;
        GridCursor.OnCursorCoordChanged -= HandleCursorCoordChanged;
    }

    private void HandleSelectedCellChanged(Cell cell)
    {
        ClearPathPreview();
        _cachedUnit = null;
        _cachedReachable = null;

        if (cell == null || !cell.HasUnit) return;

        var unit = cell.UnitController;
        if (unit == null) return;

        var pathfinding = PathfindingManager.Instance;
        if (pathfinding == null) return;

        _cachedUnit = unit;
        _cachedReachable = pathfinding.GetReachableCells(unit);
    }

    private void HandleCursorCoordChanged(Vector2Int coord)
    {
        ClearPathPreview();

        if (_cachedUnit == null || _cachedReachable == null) return;
        if (!_cachedReachable.Contains(coord)) return;
        if (coord == _cachedUnit.GridCoord) return;

        var pathfinding = PathfindingManager.Instance;
        if (pathfinding == null) return;

        var path = pathfinding.FindPath(_cachedUnit, coord);
        if (path == null || path.Count < 2) return;

        RenderPath(path);
    }

    private void RenderPath(List<Vector2Int> path)
    {
        if (mapRoot == null || lineRenderer == null || _cachedUnit == null) return;

        var points = new Vector3[path.Count];
        points[0] = GetUnitFootPosition();
        for (var i = 1; i < path.Count; i++)
            points[i] = GetPathPosition(path[i]);

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
        lineRenderer.enabled = true;
    }

    private Vector3 GetUnitFootPosition()
    {
        var pos = _cachedUnit.transform.position;
        pos.y += heightOffset;
        return pos;
    }

    private Vector3 GetPathPosition(Vector2Int coord)
    {
        var pos = mapRoot != null ? mapRoot.GridToWorld(coord) : Vector3.zero;
        pos.y += heightOffset;
        return pos;
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.numCornerVertices = cornerVertices;
        lineRenderer.numCapVertices = 4;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;

        var mat = pathMaterial;
        if (mat == null)
        {
            var shader = Shader.Find("PathPreview/LineGlow");
            if (shader != null)
            {
                mat = new Material(shader);
                mat.name = "PathPreviewLineGlow (Runtime)";
                mat.SetColor("_Color", lineColor);
            }
        }
        if (mat != null)
        {
            lineRenderer.material = mat;
            if (mat.HasProperty("_Color"))
                lineRenderer.material.SetColor("_Color", lineColor);
        }
    }

    private void ClearPathPreview()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }
    }
}
