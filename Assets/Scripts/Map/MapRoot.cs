using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图根物体，持有网格设置，所有 Cell 应作为其子物体。
/// 内部维护 Vector2Int → Cell 字典缓存，GetCellAt 为 O(1)。
/// </summary>
public class MapRoot : Singleton<MapRoot>, IGridReadView
{
    [Header("单位容器（可选）")]
    [SerializeField] private Transform _marigoldUnitContainer;
    [SerializeField] private Transform _lancelUnitContainer;

    [Header("网格设置")]
    [Tooltip("网格宽度（格子数量）")]
    public int gridWidth = 10;

    [Tooltip("网格高度（格子数量）")]
    public int gridHeight = 10;

    [Tooltip("单个格子边长（Unity 单位）")]
    public float cellSize = 1f;

    public Transform MarigoldUnitContainer => _marigoldUnitContainer;
    public Transform LancelUnitContainer => _lancelUnitContainer;
    public int Width => gridWidth;
    public int Height => gridHeight;

    private Dictionary<Vector2Int, Cell> _cellCache;

    /// <summary>
    /// 网格左下角世界坐标。MapRoot 位于世界原点，同时是整张地图的中心。
    /// </summary>
    public Vector3 GridOrigin => transform.position + new Vector3(
        -gridWidth * cellSize / 2f,
        0f,
        -gridHeight * cellSize / 2f);

    protected override void Awake()
    {
        base.Awake();
        RebuildCellCache();
    }

    /// <summary>
    /// 遍历所有子 Cell 建立坐标索引。场景加载时自动调用，也可手动触发。
    /// </summary>
    public void RebuildCellCache()
    {
        int capacity = gridWidth * gridHeight;
        _cellCache = new Dictionary<Vector2Int, Cell>(capacity);
        for (var i = 0; i < transform.childCount; i++)
        {
            var cell = transform.GetChild(i).GetComponent<Cell>();
            if (cell != null)
                _cellCache[cell.gridCoord] = cell;
        }
    }

    /// <summary>
    /// 由 Cell 在运行时动态注册自身到缓存（如工厂生成新格子时）。
    /// </summary>
    public void RegisterCell(Cell cell)
    {
        if (cell == null) return;
        if (_cellCache == null)
            _cellCache = new Dictionary<Vector2Int, Cell>(gridWidth * gridHeight);
        _cellCache[cell.gridCoord] = cell;
    }

    /// <summary>
    /// 由 Cell 在销毁时从缓存注销。
    /// </summary>
    public void UnregisterCell(Cell cell)
    {
        if (cell == null || _cellCache == null) return;
        if (_cellCache.TryGetValue(cell.gridCoord, out var cached) && cached == cell)
            _cellCache.Remove(cell.gridCoord);
    }

    /// <summary>
    /// 将世界坐标转换为网格坐标。
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        var local = worldPos - GridOrigin;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, z);
    }

    /// <summary>
    /// 将网格坐标转换为世界坐标（格子中心点）。
    /// </summary>
    public Vector3 GridToWorld(int gridX, int gridZ)
    {
        return GridOrigin + new Vector3(
            (gridX + 0.5f) * cellSize,
            0f,
            (gridZ + 0.5f) * cellSize);
    }

    public Vector3 GridToWorld(Vector2Int gridCoord)
    {
        return GridToWorld(gridCoord.x, gridCoord.y);
    }

    /// <summary>
    /// 检查网格坐标是否在有效范围内。
    /// </summary>
    public bool IsInBounds(int gridX, int gridZ)
    {
        return gridX >= 0 && gridX < gridWidth && gridZ >= 0 && gridZ < gridHeight;
    }

    public bool IsInBounds(Vector2Int gridCoord)
    {
        return IsInBounds(gridCoord.x, gridCoord.y);
    }

    /// <summary>
    /// 根据网格坐标获取对应的 Cell。O(1) 字典查找。
    /// </summary>
    public Cell GetCellAt(Vector2Int gridCoord)
    {
        if (_cellCache != null && _cellCache.TryGetValue(gridCoord, out var cell))
            return cell;
        return null;
    }

    public bool TryGetCell(Vector2Int coord, out ICellReadView cell)
    {
        cell = GetCellAt(coord);
        return cell != null;
    }
}
