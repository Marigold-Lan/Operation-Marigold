using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理格子高光显示，订阅光标坐标变化将高光置于光标所在格子。
/// 订阅选中格子变化：选中单位时在单位位置播放特效（友方/敌方不同），并在可移动范围格子上放置阵营高光。
/// </summary>
public class HighlightManager : Singleton<HighlightManager>
{
    [Header("依赖")]
    public MapRoot mapRoot;
    [SerializeField] private GameStateFacade _gameStateFacade;

    [Header("光标高光")]
    [Tooltip("光标高光预制体")]
    public GameObject highlightPrefab;

    [Tooltip("高光相对格子中心的 Y 偏移")]
    public float heightOffset = 0.05f;

    [Header("选中单位特效")]
    [Tooltip("友方单位被选中时的粒子特效预制体")]
    public GameObject friendlySelectEffectPrefab;

    [Tooltip("敌方单位被选中时的粒子特效预制体")]
    public GameObject enemySelectEffectPrefab;

    [Tooltip("特效主要播放时长（秒），之后停止发射并进入淡出")]
    public float selectEffectDuration = 0.5f;

    [Tooltip("停止发射后等待粒子淡出的时长（秒），超时后销毁")]
    public float selectEffectFadeOutDuration = 1f;

    [Header("可移动范围高光")]
    [Tooltip("友方单位可移动范围的高光预制体")]
    public GameObject friendlyMoveHighlightPrefab;

    [Tooltip("敌方单位可移动范围的高光预制体")]
    public GameObject enemyMoveHighlightPrefab;

    [Header("攻击范围高光")]
    [Tooltip("攻击范围内含敌方单位的格子高光预制体。")]
    public GameObject attackRangeEnemyHighlightPrefab;

    [Tooltip("攻击范围内非敌方单位格（空格/友方）的高光预制体。")]
    public GameObject attackRangeOtherHighlightPrefab;

    [Header("补给目标高光")]
    [Tooltip("补给阶段专用高光预制体。")]
    public GameObject supplyTargetHighlightPrefab;
    [Header("装载目标高光")]
    [Tooltip("装载阶段专用高光预制体。")]
    public GameObject loadTargetHighlightPrefab;
    [Header("卸载目标高光")]
    [Tooltip("卸载阶段专用高光预制体。")]
    public GameObject dropTargetHighlightPrefab;

    private Transform _highlightInstance;
    private readonly List<GameObject> _moveHighlightInstances = new List<GameObject>();
    private readonly List<GameObject> _attackHighlightInstances = new List<GameObject>();
    private readonly List<GameObject> _supplyHighlightInstances = new List<GameObject>();
    private readonly List<GameObject> _loadHighlightInstances = new List<GameObject>();
    private readonly List<GameObject> _dropHighlightInstances = new List<GameObject>();
    public bool HasRangeHighlights =>
        _moveHighlightInstances.Count > 0 ||
        _attackHighlightInstances.Count > 0 ||
        _supplyHighlightInstances.Count > 0 ||
        _loadHighlightInstances.Count > 0 ||
        _dropHighlightInstances.Count > 0;
    public bool HasAttackRangeHighlights => _attackHighlightInstances.Count > 0;
    public bool HasMoveRangeHighlights => _moveHighlightInstances.Count > 0;
    public bool HasSupplyTargetHighlights => _supplyHighlightInstances.Count > 0;

    protected override void Awake()
    {
        base.Awake();
        if (_gameStateFacade == null)
        {
#if UNITY_2023_1_OR_NEWER
            _gameStateFacade = FindFirstObjectByType<GameStateFacade>(FindObjectsInactive.Include);
#else
            _gameStateFacade = FindObjectOfType<GameStateFacade>();
#endif
        }
    }

    private void OnEnable()
    {
        GridCursor.OnCursorVisualCoordChanged += PlaceHighlight;
        GridCursor.OnCursorVisualMoveStarted += HideCursorHighlight;
        SelectionManager.OnSelectedCellChanged += HandleSelectedCellChanged;

        var cursor = GridCursor.Instance;
        if (cursor != null)
            PlaceHighlight(cursor.VisualCoord);
        if (mapRoot == null)
            mapRoot = MapRoot.Instance;
    }

    private void OnDisable()
    {
        GridCursor.OnCursorVisualCoordChanged -= PlaceHighlight;
        GridCursor.OnCursorVisualMoveStarted -= HideCursorHighlight;
        SelectionManager.OnSelectedCellChanged -= HandleSelectedCellChanged;
    }

    private void HandleSelectedCellChanged(Cell cell)
    {
        ClearMoveHighlights();
        ClearAttackHighlights();
        ClearSupplyHighlights();
        ClearLoadHighlights();
        ClearDropHighlights();

        if (cell == null || !cell.HasUnit) return;

        var unit = cell.UnitController;
        if (unit == null) return;

        var prefab = GetPrefabForUnit(unit, friendlySelectEffectPrefab, enemySelectEffectPrefab);
        if (prefab != null)
        {
            var go = Instantiate(prefab, unit.transform.position, Quaternion.identity, transform);
            StartCoroutine(PlayAndFadeOut(go));
        }

        ShowMoveRangeHighlights(unit);
        // 预留：攻击范围高亮接口已提供，但这里先不调用（按需求“先不要投入使用”）。
    }

    public void ShowMoveRangeHighlights(UnitController unit)
    {
        var pathfinding = PathfindingManager.Instance;
        if (pathfinding == null || mapRoot == null) return;

        var prefab = GetPrefabForUnit(unit, friendlyMoveHighlightPrefab, enemyMoveHighlightPrefab);
        if (prefab == null) return;

        var reachable = pathfinding.GetReachableCells(unit);
        var unitPos = unit.GridCoord;

        foreach (var coord in reachable)
        {
            if (coord == unitPos) continue;

            var go = PlaceHighlight(prefab, coord);
            if (go != null) _moveHighlightInstances.Add(go);
        }

        RefreshCursorHighlightAtCurrentCoord();
    }

    private GameObject GetPrefabForUnit(UnitController unit, GameObject friendlyPrefab, GameObject enemyPrefab)
    {
        var session = _gameStateFacade != null ? _gameStateFacade.Session : null;
        var currentFaction = session != null ? session.CurrentFaction : UnitFaction.None;
        var isFriendly = currentFaction != UnitFaction.None && unit.OwnerFaction == currentFaction;
        return isFriendly ? friendlyPrefab : enemyPrefab;
    }

    private Vector3 GetHighlightPosition(Vector2Int coord)
    {
        var pos = mapRoot != null ? mapRoot.GridToWorld(coord) : Vector3.zero;
        pos.y += heightOffset;
        return pos;
    }

    public void ClearMoveHighlights()
    {
        foreach (var go in _moveHighlightInstances)
        {
            if (go != null)
                Destroy(go);
        }
        _moveHighlightInstances.Clear();
        RefreshCursorHighlightAtCurrentCoord();
    }

    /// <summary>
    /// 高亮单位攻击范围（曼哈顿距离环带）。
    /// 当前为预留接口，尚未在选中流程中启用。
    /// </summary>
    public void ShowAttackRangeHighlights(UnitController unit, bool clearExisting = true)
    {
        if (clearExisting)
            ClearAttackHighlights();

        if (unit == null || unit.Data == null || mapRoot == null) return;
        if (!unit.Data.HasAnyWeapon) return;

        var maxRange = unit.Data.GetAvailableAttackRangeMax(unit.CurrentAmmo, unit.HasMovedThisTurn);
        if (maxRange < 1)
            return;

        var origin = unit.GridCoord;

        var coords = GetCoordsInRange(origin, maxRange);
        foreach (var coord in coords)
        {
            var distance = Mathf.Abs(coord.x - origin.x) + Mathf.Abs(coord.y - origin.y);
            if (!unit.Data.CanAttackAtDistance(distance, unit.CurrentAmmo, unit.HasMovedThisTurn))
                continue;

            var prefab = GetAttackRangePrefabForCoord(unit, coord);
            if (prefab == null) continue;

            var go = PlaceHighlight(prefab, coord);
            if (go != null) _attackHighlightInstances.Add(go);
        }

        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ClearAttackHighlights()
    {
        foreach (var go in _attackHighlightInstances)
        {
            if (go != null)
                Destroy(go);
        }
        _attackHighlightInstances.Clear();
        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ShowSupplyTargetHighlights(UnitController sourceUnit, bool clearExisting = true)
    {
        if (clearExisting)
            ClearSupplyHighlights();

        if (sourceUnit == null || mapRoot == null)
            return;

        if (supplyTargetHighlightPrefab == null)
            return;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(sourceUnit.GridCoord))
        {
            if (!mapRoot.IsInBounds(coord))
                continue;

            var go = PlaceHighlight(supplyTargetHighlightPrefab, coord);
            if (go != null)
                _supplyHighlightInstances.Add(go);
        }

        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ClearSupplyHighlights()
    {
        foreach (var go in _supplyHighlightInstances)
        {
            if (go != null)
                Destroy(go);
        }

        _supplyHighlightInstances.Clear();
        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ShowLoadTargetHighlights(UnitController sourceUnit, bool clearExisting = true)
    {
        if (clearExisting)
            ClearLoadHighlights();

        if (sourceUnit == null || mapRoot == null || loadTargetHighlightPrefab == null)
            return;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(sourceUnit.GridCoord))
        {
            if (!LoadCommand.TryGetLoadTargetTransporterAtCoord(sourceUnit, mapRoot, coord, out _))
                continue;

            var go = PlaceHighlight(loadTargetHighlightPrefab, coord);
            if (go != null)
                _loadHighlightInstances.Add(go);
        }

        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ClearLoadHighlights()
    {
        foreach (var go in _loadHighlightInstances)
        {
            if (go != null)
                Destroy(go);
        }

        _loadHighlightInstances.Clear();
        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ShowDropTargetHighlights(UnitController sourceUnit, bool clearExisting = true)
    {
        if (clearExisting)
            ClearDropHighlights();

        if (sourceUnit == null || mapRoot == null || dropTargetHighlightPrefab == null)
            return;
        var transporter = sourceUnit.GetComponent<ITransporter>();
        if (transporter == null || transporter.LoadedCount <= 0 || transporter.LoadedUnits == null || transporter.LoadedUnits.Count == 0)
            return;

        var cargo = transporter.LoadedUnits[0];
        if (cargo == null)
            return;

        foreach (var coord in CommandGridUtils.EnumerateCardinalNeighbors(sourceUnit.GridCoord))
        {
            if (!DropCommand.IsValidDropCoord(sourceUnit, mapRoot, coord, cargo))
                continue;

            var go = PlaceHighlight(dropTargetHighlightPrefab, coord);
            if (go != null)
                _dropHighlightInstances.Add(go);
        }

        RefreshCursorHighlightAtCurrentCoord();
    }

    public void ClearDropHighlights()
    {
        foreach (var go in _dropHighlightInstances)
        {
            if (go != null)
                Destroy(go);
        }

        _dropHighlightInstances.Clear();
        RefreshCursorHighlightAtCurrentCoord();
    }

    private List<Vector2Int> GetCoordsInRange(Vector2Int center, int maxRange)
    {
        var result = new List<Vector2Int>();
        var dedup = new HashSet<Vector2Int>();

        for (var dx = -maxRange; dx <= maxRange; dx++)
        {
            for (var dy = -maxRange; dy <= maxRange; dy++)
            {
                var distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (distance < 1 || distance > maxRange) continue;

                var coord = new Vector2Int(center.x + dx, center.y + dy);
                if (!mapRoot.IsInBounds(coord)) continue;
                if (dedup.Add(coord))
                    result.Add(coord);
            }
        }

        return result;
    }

    private GameObject GetAttackRangePrefabForCoord(UnitController sourceUnit, Vector2Int coord)
    {
        var cell = mapRoot != null ? mapRoot.GetCellAt(coord) : null;
        var target = cell != null ? cell.UnitController : null;
        var hasEnemyUnit = target != null && sourceUnit != null && target.OwnerFaction != sourceUnit.OwnerFaction;
        var canAttackEnemy = hasEnemyUnit &&
                             sourceUnit.Data != null &&
                             target.Data != null &&
                             sourceUnit.Data.CanAttackTarget(target.Data, sourceUnit.CurrentAmmo, sourceUnit.HasMovedThisTurn);

        if (canAttackEnemy)
            return attackRangeEnemyHighlightPrefab != null ? attackRangeEnemyHighlightPrefab : attackRangeOtherHighlightPrefab;

        return attackRangeOtherHighlightPrefab != null ? attackRangeOtherHighlightPrefab : attackRangeEnemyHighlightPrefab;
    }

    private IEnumerator PlayAndFadeOut(GameObject go)
    {
        yield return new WaitForSeconds(selectEffectDuration);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        yield return new WaitForSeconds(selectEffectFadeOutDuration);
        Destroy(go);
    }

    /// <summary>
    /// 将光标高光置于指定格子（ reposition 单例）。
    /// </summary>
    private void PlaceHighlight(Vector2Int coord)
    {
        if (mapRoot == null || !mapRoot.IsInBounds(coord)) return;
        if (highlightPrefab == null) return;

        var cursor = GridCursor.Instance;
        if (cursor != null && cursor.IsVisualMoving)
        {
            HideCursorHighlight();
            return;
        }

        EnsureHighlightInstance();
        if (_highlightInstance == null) return;

        if (HasOverlayHighlightAt(coord))
        {
            _highlightInstance.gameObject.SetActive(false);
            return;
        }

        _highlightInstance.position = GetHighlightPosition(coord);
        _highlightInstance.gameObject.SetActive(true);
    }

    private void HideCursorHighlight()
    {
        if (_highlightInstance != null)
            _highlightInstance.gameObject.SetActive(false);
    }

    /// <summary>
    /// 在指定格子实例化高光，返回实例。用于移动范围等高光。
    /// </summary>
    private GameObject PlaceHighlight(GameObject prefab, Vector2Int coord)
    {
        if (prefab == null || mapRoot == null || !mapRoot.IsInBounds(coord)) return null;
        return Instantiate(prefab, GetHighlightPosition(coord), Quaternion.identity, transform);
    }

    private void EnsureHighlightInstance()
    {
        if (_highlightInstance != null) return;

        var go = Instantiate(highlightPrefab, transform);
        go.name = "CursorHighlight";
        _highlightInstance = go.transform;
    }

    private bool HasOverlayHighlightAt(Vector2Int coord)
    {
        return HasHighlightAt(_moveHighlightInstances, coord) ||
               HasHighlightAt(_attackHighlightInstances, coord) ||
               HasHighlightAt(_supplyHighlightInstances, coord) ||
               HasHighlightAt(_loadHighlightInstances, coord) ||
               HasHighlightAt(_dropHighlightInstances, coord);
    }

    private bool HasHighlightAt(List<GameObject> highlights, Vector2Int coord)
    {
        for (var i = 0; i < highlights.Count; i++)
        {
            var go = highlights[i];
            if (go == null) continue;
            if (mapRoot.WorldToGrid(go.transform.position) == coord)
                return true;
        }

        return false;
    }

    private void RefreshCursorHighlightAtCurrentCoord()
    {
        var cursor = GridCursor.Instance;
        if (cursor == null) return;
        PlaceHighlight(cursor.VisualCoord);
    }
}
