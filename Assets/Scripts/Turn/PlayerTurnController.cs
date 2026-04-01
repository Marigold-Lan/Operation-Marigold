using UnityEngine;

public class PlayerTurnController
{
    private const string FactoryOpenDebugTag = "[FactoryOpenDebug]";
    private const bool EnableFactoryOpenDebugLog = true;

    private readonly SelectionManager _selectionManager;
    private readonly AttackTargetingSession _attackTargetingSession = new AttackTargetingSession();
    private readonly SupplyTargetingSession _supplyTargetingSession = new SupplyTargetingSession();
    private readonly LoadTargetingSession _loadTargetingSession = new LoadTargetingSession();
    private readonly DropTargetingSession _dropTargetingSession = new DropTargetingSession();
    private UnitController _pendingCommandUnit;
    private MapRoot _mapRoot;
    private readonly GameSessionState _sessionState;
    private bool _attackTargetingConsumeActionOnCancel;

    public PlayerTurnController(SelectionManager selectionManager, MapRoot mapRoot, GameSessionState sessionState)
    {
        _selectionManager = selectionManager;
        _mapRoot = mapRoot;
        _sessionState = sessionState;
    }

    public bool IsAttackTargeting => _attackTargetingSession.IsActive;
    public bool IsSupplyTargeting => _supplyTargetingSession.IsActive;
    public bool IsLoadTargeting => _loadTargetingSession.IsActive;
    public bool IsDropTargeting => _dropTargetingSession.IsActive;
    public AttackTargetingSession AttackTargetingSession => _attackTargetingSession;
    public SupplyTargetingSession SupplyTargetingSession => _supplyTargetingSession;

    public void SetMapRoot(MapRoot mapRoot)
    {
        _mapRoot = mapRoot;
    }

    public void HandleCancel()
    {
        if (FactoryPanelManager.IsAnyOpen)
        {
            FactoryPanelManager.Instance?.Close();
            return;
        }

        if (_supplyTargetingSession.IsActive)
        {
            var supplySource = _supplyTargetingSession.SourceUnit;
            if (supplySource != null)
                supplySource.HasActed = true;

            ExitSupplyTargeting(clearHighlights: true);
            HighlightManager.Instance?.ClearMoveHighlights();
            HighlightManager.Instance?.ClearAttackHighlights();
            _selectionManager?.ClearSelection();
            return;
        }

        if (_loadTargetingSession.IsActive)
        {
            ExitLoadTargeting(clearHighlights: true);
            return;
        }

        if (_dropTargetingSession.IsActive)
        {
            ExitDropTargeting(clearHighlights: true);
            return;
        }

        if (!_attackTargetingSession.IsActive)
            return;

        var attackSource = _attackTargetingSession.SourceUnit;
        var attackSourceCoord = attackSource != null ? (Vector2Int?)attackSource.GridCoord : null;
        var consumeActionOnCancel = _attackTargetingConsumeActionOnCancel;

        ExitAttackTargeting(clearHighlights: true);

        if (attackSourceCoord.HasValue)
            GridCursor.Instance?.SetPosition(attackSourceCoord.Value);
        if (attackSource != null)
            OpenCommandPanelForUnit(attackSource, consumeActionOnCancel);
    }

    public void HandleConfirm(Vector2Int coord)
    {
        if (FactoryPanelManager.IsBlockingGameplayConfirm)
        {
            LogFactoryOpen("HandleConfirm ignored: FactoryPanel is open or closed this frame.");
            return;
        }

        if (_attackTargetingSession.IsActive)
        {
            LogFactoryOpen("HandleConfirm routed to attack targeting.");
            HandleAttackConfirm(coord);
            return;
        }

        if (_supplyTargetingSession.IsActive)
        {
            HandleSupplyConfirm();
            return;
        }

        if (_loadTargetingSession.IsActive)
        {
            HandleLoadConfirm(coord);
            return;
        }

        if (_dropTargetingSession.IsActive)
        {
            HandleDropConfirm(coord);
            return;
        }

        if (CommandPanelController.IsAnyOpen || _mapRoot == null)
        {
            LogFactoryOpen($"HandleConfirm aborted: commandPanelOpen={CommandPanelController.IsAnyOpen}, mapRootNull={_mapRoot == null}.");
            return;
        }

        var cell = _mapRoot.GetCellAt(coord);
        if (cell == null)
        {
            LogFactoryOpen($"HandleConfirm aborted: cell not found at {FormatCoord(coord)}.");
            return;
        }

        var hoveredUnit = cell.UnitController;
        if (_selectionManager.SelectedUnit == null && hoveredUnit != null)
        {
            if (hoveredUnit.HasActed)
                return;

            // 单位与工厂同格时，优先选中单位（己方/敌方都一致）。
            _selectionManager.SelectCell(cell);
            return;
        }

        if (_selectionManager.SelectedUnit == null && IsFactorySelectionBlocked(cell))
            return;

        if (TryOpenFactoryPanelFromIdle(cell))
            return;

        var unit = _selectionManager.SelectedUnit;
        var canOperateSelectedUnit = CanOperateUnit(unit);
        if (unit != null && coord == unit.GridCoord)
        {
            if (!canOperateSelectedUnit)
                return;
            _selectionManager.ClearSelection();
            OpenCommandPanelForUnit(unit);
            return;
        }

        if (canOperateSelectedUnit && UnitActionValidator.TryGetMovePath(unit, coord, _sessionState, out var path))
        {
            _pendingCommandUnit = unit;
            unit.Movement.MoveAlongPath(path, OnMoveCompleted);
            _selectionManager.ClearSelection();
            return;
        }

        // 进入单位移动范围预览后，非可移动目标不会切换选中，只能通过 ESC 退出。
        if (unit != null)
            return;

        if (IsCellSelectionBlocked(cell))
            return;

        _selectionManager.SelectCell(cell);
    }

    private bool TryOpenFactoryPanelFromIdle(Cell cell)
    {
        if (cell == null || _selectionManager == null)
        {
            LogFactoryOpen($"TryOpenFactoryPanelFromIdle=false: cellNull={cell == null}, selectionManagerNull={_selectionManager == null}.");
            return false;
        }
        // “闲逛”按无范围高亮定义。若当前选中的是单位，仍走单位流程，不进入工厂菜单。
        if (_selectionManager.SelectedUnit != null)
        {
            LogFactoryOpen($"TryOpenFactoryPanelFromIdle=false: selected unit exists ({_selectionManager.SelectedUnit.name}).");
            return false;
        }

        var highlightManager = HighlightManager.Instance;
        if (highlightManager != null && highlightManager.HasRangeHighlights)
        {
            LogFactoryOpen("TryOpenFactoryPanelFromIdle=false: has range highlights.");
            return false;
        }

        var building = cell.Building;
        if (building == null)
        {
            LogFactoryOpen("TryOpenFactoryPanelFromIdle=false: current cell has no building.");
            return false;
        }
        if (building.OwnerFaction == UnitFaction.None)
        {
            LogFactoryOpen("TryOpenFactoryPanelFromIdle=false: building owner faction is None.");
            return false;
        }

        var currentFaction = _sessionState != null ? _sessionState.CurrentFaction : UnitFaction.None;
        if (currentFaction != UnitFaction.None &&
            building.OwnerFaction != currentFaction)
        {
            LogFactoryOpen(
                $"TryOpenFactoryPanelFromIdle=false: faction mismatch. building={building.OwnerFaction}, currentTurn={currentFaction}.");
            return false;
        }

        var spawner = building.GetComponent<FactorySpawner>();
        if (spawner == null)
        {
            LogFactoryOpen("TryOpenFactoryPanelFromIdle=false: building has no FactorySpawner.");
            return false;
        }
        if (!spawner.CanSpawn(building.OwnerFaction))
        {
            LogFactoryOpen("TryOpenFactoryPanelFromIdle=false: spawner cannot spawn this turn.");
            return false;
        }

        var panel = FactoryPanelManager.Instance;
        if (panel == null)
            return false;

        var opened = panel.Open(spawner, building.OwnerFaction);
        LogFactoryOpen($"TryOpenFactoryPanelFromIdle result: opened={opened}.");
        return opened;
    }

    public void EnterAttackTargeting(UnitController unit, bool consumeActionOnCancel = false)
    {
        _attackTargetingConsumeActionOnCancel = consumeActionOnCancel;
        _attackTargetingSession.TryEnter(unit, _mapRoot);
    }

    public void ExitAttackTargeting(bool clearHighlights)
    {
        _attackTargetingSession.Exit(clearHighlights);
        _attackTargetingConsumeActionOnCancel = false;
    }

    public void EnterSupplyTargeting(UnitController unit)
    {
        _supplyTargetingSession.TryEnter(unit, _mapRoot);
    }

    public void ExitSupplyTargeting(bool clearHighlights)
    {
        _supplyTargetingSession.Exit(clearHighlights);
    }

    public void EnterLoadTargeting(UnitController unit)
    {
        _loadTargetingSession.TryEnter(unit, _mapRoot);
    }

    public void ExitLoadTargeting(bool clearHighlights)
    {
        _loadTargetingSession.Exit(clearHighlights);
    }

    public void EnterDropTargeting(UnitController unit)
    {
        _dropTargetingSession.TryEnter(unit, _mapRoot);
    }

    public void ExitDropTargeting(bool clearHighlights)
    {
        _dropTargetingSession.Exit(clearHighlights);
    }

    private void OnMoveCompleted()
    {
        if (_pendingCommandUnit == null)
            return;

        var unit = _pendingCommandUnit;
        _pendingCommandUnit = null;
        OpenCommandPanelForUnit(unit, consumeActionOnCancel: true);
    }

    private void OpenCommandPanelForUnit(UnitController unit, bool consumeActionOnCancel = false)
    {
        if (unit == null || unit.Data == null)
            return;
        if (!CanOperateUnit(unit))
            return;

        var context = BuildCommandContext(unit, consumeActionOnCancel);
        var options = CommandProvider.Build(context);
        if (options == null || options.Count == 0)
            return;

        var panel = CommandPanelController.Instance;
        if (panel == null)
            return;

        var screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, unit.transform.position);
        panel.Open(context, options, screenPos);
    }

    private void HandleAttackConfirm(Vector2Int coord)
    {
        var source = _attackTargetingSession.SourceUnit;
        if (!_attackTargetingSession.IsActive || source == null)
            return;

        if (!_attackTargetingSession.Contains(coord))
        {
            _selectionManager.NotifyAttackTargetInvalid(coord);
            return;
        }

        var root = _mapRoot != null ? _mapRoot : source.MapRoot;
        var cell = root != null ? root.GetCellAt(coord) : null;
        var target = cell != null ? cell.UnitController : null;
        if (target == null || target.OwnerFaction == source.OwnerFaction)
        {
            _selectionManager.NotifyAttackTargetInvalid(coord);
            return;
        }

        var context = BuildCommandContext(source);
        var command = new AttackCommand(source, target, coord);
        CommandExecutor.Execute(command, context);
    }

    private void HandleSupplyConfirm()
    {
        var source = _supplyTargetingSession.SourceUnit;
        if (!_supplyTargetingSession.IsActive || source == null)
            return;

        var root = _mapRoot != null ? _mapRoot : source.MapRoot;
        if (root == null)
            return;

        var context = BuildImmediateCommandContext(source);
        if (!CommandExecutor.Execute(new SupplyCommand(), context))
            return;

        ExitSupplyTargeting(clearHighlights: true);
        HighlightManager.Instance?.ClearMoveHighlights();
        HighlightManager.Instance?.ClearAttackHighlights();
        _selectionManager?.ClearSelection();
    }

    private void HandleLoadConfirm(Vector2Int coord)
    {
        var source = _loadTargetingSession.SourceUnit;
        if (!_loadTargetingSession.IsActive || source == null)
            return;
        if (!_loadTargetingSession.Contains(coord))
            return;

        var root = _mapRoot != null ? _mapRoot : source.MapRoot;
        var targetUnit = root != null ? root.GetCellAt(coord)?.UnitController : null;
        if (!LoadCommand.TryGetLoadTargetTransporterAtCoord(source, root, coord, out var transporter))
            return;
        if (targetUnit == null || targetUnit.GetComponent<ITransporter>() != transporter)
            return;

        var context = BuildImmediateCommandContext(source, targetUnit: targetUnit);
        if (!CommandExecutor.Execute(new LoadCommand(), context))
            return;

        ExitLoadTargeting(clearHighlights: true);
        HighlightManager.Instance?.ClearMoveHighlights();
        HighlightManager.Instance?.ClearAttackHighlights();
        _selectionManager?.ClearSelection();
    }

    private void HandleDropConfirm(Vector2Int coord)
    {
        var source = _dropTargetingSession.SourceUnit;
        if (!_dropTargetingSession.IsActive || source == null)
            return;
        if (!_dropTargetingSession.Contains(coord))
            return;

        var transporter = source.GetComponent<ITransporter>();
        if (transporter == null || transporter.LoadedCount <= 0)
            return;

        var cargo = transporter.LoadedUnits != null && transporter.LoadedUnits.Count > 0
            ? transporter.LoadedUnits[0]
            : null;
        if (cargo == null)
            return;

        var root = _mapRoot != null ? _mapRoot : source.MapRoot;
        if (!DropCommand.IsValidDropCoord(source, root, coord, cargo))
            return;

        var context = BuildImmediateCommandContext(
            source,
            targetUnit: cargo,
            targetCoord: coord,
            hasTargetCoord: true);
        if (!CommandExecutor.Execute(new DropCommand(), context))
            return;

        ExitDropTargeting(clearHighlights: true);
        HighlightManager.Instance?.ClearMoveHighlights();
        HighlightManager.Instance?.ClearAttackHighlights();
        _selectionManager?.ClearSelection();
    }

    private CommandContext BuildCommandContext(UnitController unit, bool consumeActionOnCancel = false)
    {
        return new CommandContext
        {
            Unit = unit,
            CurrentCell = unit.CurrentCell,
            MapRoot = _mapRoot != null ? _mapRoot : unit.MapRoot,
            SessionState = _sessionState,
            SelectionManager = _selectionManager,
            HighlightManager = HighlightManager.Instance,
            GridCoord = unit.GridCoord,
            ConsumeActionOnCancel = consumeActionOnCancel,
            TurnController = this,
            AttackTargetingSession = _attackTargetingSession,
            Mode = CommandContext.ExecutionMode.PlayerInteractive
        };
    }

    private CommandContext BuildImmediateCommandContext(
        UnitController unit,
        UnitController targetUnit = null,
        Vector2Int targetCoord = default,
        bool hasTargetCoord = false)
    {
        return new CommandContext
        {
            Mode = CommandContext.ExecutionMode.AIImmediate,
            Unit = unit,
            TargetUnit = targetUnit,
            CurrentCell = unit != null ? unit.CurrentCell : null,
            MapRoot = _mapRoot != null ? _mapRoot : (unit != null ? unit.MapRoot : null),
            GridCoord = unit != null ? unit.GridCoord : default,
            TargetCoord = targetCoord,
            HasTargetCoord = hasTargetCoord
        };
    }

    private static string FormatCoord(Vector2Int coord)
    {
        return $"({coord.x},{coord.y})";
    }

    private bool CanOperateUnit(UnitController unit)
    {
        if (unit == null)
            return false;
        if (unit.HasActed)
            return false;

        var currentFaction = _sessionState != null ? _sessionState.CurrentFaction : UnitFaction.None;
        if (currentFaction == UnitFaction.None)
            return true;

        return unit.OwnerFaction == currentFaction;
    }

    private static void LogFactoryOpen(string message) { }

    private static bool IsFactorySelectionBlocked(Cell cell)
    {
        if (cell == null)
            return false;

        var building = cell.Building;
        if (building == null)
            return false;

        var spawner = building.GetComponent<FactorySpawner>();
        if (spawner == null)
            return false;

        return building.State != null && building.State.HasSpawnedThisTurn;
    }

    private static bool IsCellSelectionBlocked(Cell cell)
    {
        if (cell == null)
            return true;

        var unit = cell.UnitController;
        if (unit != null && unit.HasActed)
            return true;

        if (IsFactorySelectionBlocked(cell))
            return true;

        return false;
    }
}
