using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用选中管理器。以 Cell（格子）为统一选中目标，支持单位、建筑或纯地形。
/// 选中变化后，消费者可订阅 OnSelectedCellChanged 响应（null 表示清除选中）。
/// </summary>
public class SelectionManager : Singleton<SelectionManager>
{
    public static event Action<Cell> OnSelectedCellChanged;
    public static event Action<Vector2Int> OnAttackTargetInvalid;

    [Header("依赖")]
    public MapRoot mapRoot;
    [SerializeField] private GameStateFacade _gameStateFacade;

    private Cell _selectedCell;
    private PlayerTurnController _turnController;
    private readonly RangeLockSession _moveRangeLockSession = new RangeLockSession();

    /// <summary>
    /// 当前选中的格子，null 表示空闲（无选中）。
    /// </summary>
    public Cell SelectedCell => _selectedCell;

    /// <summary>
    /// 是否有格子被选中。
    /// </summary>
    public bool HasSelection => _selectedCell != null;

    /// <summary>
    /// 当前选中格子有单位时返回 UnitController，否则为 null。
    /// </summary>
    public UnitController SelectedUnit => _selectedCell != null ? _selectedCell.UnitController : null;

    /// <summary>
    /// 当前选中格子有建筑时返回 BuildingController，否则为 null。
    /// </summary>
    public BuildingController SelectedBuilding => _selectedCell != null ? _selectedCell.Building : null;

    /// <summary>
    /// 当前是否处于攻击选目标状态（攻击范围显示/光标锁定等）。
    /// </summary>
    public bool IsAttackTargeting => _turnController != null && _turnController.IsAttackTargeting;

    /// <summary>
    /// 当前是否处于补给选目标状态（四邻格高亮/光标锁定等）。
    /// </summary>
    public bool IsSupplyTargeting => _turnController != null && _turnController.IsSupplyTargeting;
    public bool IsLoadTargeting => _turnController != null && _turnController.IsLoadTargeting;
    public bool IsDropTargeting => _turnController != null && _turnController.IsDropTargeting;

    /// <summary>
    /// 攻击选目标状态下的攻击方单位（否则为 null）。
    /// </summary>
    public UnitController AttackTargetingSourceUnit => _turnController != null ? _turnController.AttackTargetingSession.SourceUnit : null;

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
        var session = _gameStateFacade != null ? _gameStateFacade.Session : null;
        _turnController = new PlayerTurnController(this, mapRoot, session);
    }

    private void OnEnable()
    {
        InputManager.RegisterConfirmHandler(HandleConfirm, priority: 0);
        InputManager.RegisterCancelHandler(HandleCancel, priority: 0);
    }

    private void OnDisable()
    {
        InputManager.UnregisterConfirmHandler(HandleConfirm);
        InputManager.UnregisterCancelHandler(HandleCancel);
    }

    private bool HandleCancel()
    {
        if (FieldMenuController.IsAnyOpen)
        {
            FieldMenuController.Instance?.Close();
            return true;
        }

        if (_turnController != null &&
            (_turnController.IsAttackTargeting ||
             _turnController.IsSupplyTargeting ||
             _turnController.IsLoadTargeting ||
             _turnController.IsDropTargeting))
        {
            _turnController.HandleCancel();
            return true;
        }

        if (CommandPanelController.IsAnyOpen || FactoryPanelManager.IsAnyOpen)
            return false;

        if (IsInFieldMenuIdleState())
        {
            FieldMenuController.Instance?.Open();
            return true;
        }

        var selectedUnit = SelectedUnit;
        if (selectedUnit != null)
            GridCursor.Instance?.SetPosition(selectedUnit.GridCoord);

        ClearSelection();
        return selectedUnit != null || HasSelection == false;
    }

    private void Start()
    {
        if (mapRoot == null)
            mapRoot = MapRoot.Instance;
        _turnController?.SetMapRoot(mapRoot);
    }

    private bool HandleConfirm(Vector2Int coord)
    {
        if (FieldMenuController.IsAnyOpen)
            return false;

        _turnController?.SetMapRoot(mapRoot);
        _turnController?.HandleConfirm(coord);
        return true;
    }

    private bool IsInFieldMenuIdleState()
    {
        if (_selectedCell != null)
            return false;
        if (_turnController != null &&
            (_turnController.IsAttackTargeting ||
             _turnController.IsSupplyTargeting ||
             _turnController.IsLoadTargeting ||
             _turnController.IsDropTargeting))
            return false;
        if (CommandPanelController.IsAnyOpen || FactoryPanelManager.IsAnyOpen)
            return false;

        var highlightManager = HighlightManager.Instance;
        return highlightManager == null || !highlightManager.HasRangeHighlights;
    }

    public void EnterAttackTargeting(UnitController unit)
    {
        _turnController?.EnterAttackTargeting(unit);
    }

    internal void NotifyAttackTargetInvalid(Vector2Int coord) => OnAttackTargetInvalid?.Invoke(coord);

    /// <summary>
    /// 选中指定格子。单位、建筑、纯地形格子均可选中。
    /// </summary>
    public void SelectCell(Cell cell)
    {
        if (cell == null) return;

        _selectedCell = cell;
        OnSelectedCellChanged?.Invoke(cell);
        ApplyMoveRangeCursorRestriction();
    }

    /// <summary>
    /// 清除当前选中。
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedCell == null) return;

        _selectedCell = null;
        OnSelectedCellChanged?.Invoke(null);
        ClearMoveRangeCursorRestriction();
    }

    private void ApplyMoveRangeCursorRestriction()
    {
        if (_turnController != null &&
            (_turnController.IsAttackTargeting ||
             _turnController.IsSupplyTargeting ||
             _turnController.IsLoadTargeting ||
             _turnController.IsDropTargeting))
            return;

        var unit = SelectedUnit;
        if (unit == null)
        {
            _moveRangeLockSession.Exit();
            return;
        }

        var session = _gameStateFacade != null ? _gameStateFacade.Session : null;
        var currentFaction = session != null ? session.CurrentFaction : UnitFaction.None;
        var isEnemyUnit = currentFaction != UnitFaction.None && unit.OwnerFaction != currentFaction;
        if (isEnemyUnit)
        {
            // 选中敌方单位：保留其移动范围高亮用于观察，但光标锁在当前格，不允许移动。
            _moveRangeLockSession.TryEnter(new List<Vector2Int> { unit.GridCoord }, snapToNearestAllowed: true);
            return;
        }

        var pathfinding = PathfindingManager.Instance;
        if (pathfinding == null)
        {
            _moveRangeLockSession.Exit();
            return;
        }

        var reachable = pathfinding.GetReachableCells(unit);
        if (reachable == null || reachable.Count == 0)
        {
            _moveRangeLockSession.Exit();
            return;
        }

        _moveRangeLockSession.TryEnter(reachable, snapToNearestAllowed: true);
    }

    private void ClearMoveRangeCursorRestriction()
    {
        if (_turnController != null &&
            (_turnController.IsAttackTargeting ||
             _turnController.IsSupplyTargeting ||
             _turnController.IsLoadTargeting ||
             _turnController.IsDropTargeting))
            return;

        _moveRangeLockSession.Exit();
    }
}
