using System;
using UnityEngine;
using UnityEngine.Serialization;
using OperationMarigold.GameplayEvents;

/// <summary>
/// 建筑核心控制器。持有 BuildingData 与 BuildingState，实现 ICaptureTarget、IIncomeProvider。
/// IGameCondition 由总部专用组件实现；IUnitSpawner 由 FactorySpawner 组件实现。
/// </summary>
public class BuildingController : MonoBehaviour, ICaptureTarget, IIncomeProvider, IBuildingReadView
{
    [SerializeField] private BuildingData _data;
    [SerializeField] private BuildingView _view;
    [Tooltip("初始所属阵营。")]
    [FormerlySerializedAs("_initialOwnerId")]
    [SerializeField] private UnitFaction _initialOwnerFaction = UnitFaction.None;
    [Tooltip("当前所属阵营（运行时）。")]
    [SerializeField] private UnitFaction _currentOwnerFaction = UnitFaction.None;

    private BuildingState _state;
    private UnitController _activeCapturer;
    public UnitFaction OwnerFaction => _state?.OwnerFaction ?? _initialOwnerFaction;
    public UnitFaction InitialOwnerFaction => _initialOwnerFaction;
    private Cell _cell;

    public BuildingData Data => _data;
    public BuildingState State => _state;
    public Cell Cell => _cell;
    public Vector2Int GridCoord => _cell != null ? _cell.gridCoord : new Vector2Int(_state != null ? _state.GridX : 0, _state != null ? _state.GridZ : 0);
    public bool IsHq => _data != null && _data.isHq;
    public bool IsFactory => GetComponent<IUnitSpawner>() != null;
    public int IncomePerTurn => _data != null ? _data.incomePerTurn : 0;
    public int CaptureHp => CurrentCaptureHp;
    public int CaptureDamagePerStep => _data != null ? _data.captureDamagePerStep : 0;

    /// <summary>占领完成时触发（旧阵营, 新阵营）。</summary>
    public event Action<UnitFaction, UnitFaction> OnCaptured;
    /// <summary>阵营被外部设置时触发（初始化/读档等）。</summary>
    public event Action<UnitFaction> OnOwnerFactionSet;
    /// <summary>阵营发生变化时触发（旧阵营, 新阵营）。</summary>
    public event Action<UnitFaction, UnitFaction> OnOwnerFactionChanged;

    /// <summary>
    /// 占领施加导致的占领耐久变化（占领者, 攻击方阵营, 旧耐久, 新耐久, 本次伤害）。
    /// </summary>
    public event Action<UnitController, UnitFaction, int, int, int> OnCaptureProgressChanged;

    /// <summary>
    /// 占领进度被重置（上一次占领者, 原因）。
    /// </summary>
    public event Action<UnitController, CaptureResetReason> OnCaptureProgressReset;

    /// <summary>
    /// 占领尝试被拒绝（占领者, 攻击方阵营, 原因）。
    /// </summary>
    public event Action<UnitController, UnitFaction, CaptureRejectReason> OnCaptureAttemptRejected;

    public int CurrentCaptureHp => _state?.CurrentCaptureHp ?? 0;
    public int MaxCaptureHp => _data != null ? _data.maxCaptureHp : 0;

    private void Awake()
    {
        if (_view == null)
            _view = GetComponent<BuildingView>();
        _view?.Bind(this);

        _cell = GetComponentInParent<Cell>();
        _state = new BuildingState
        {
            OwnerFaction = _initialOwnerFaction,
            CurrentCaptureHp = _data != null ? _data.maxCaptureHp : 20,
            GridX = _cell != null ? _cell.gridCoord.x : 0,
            GridZ = _cell != null ? _cell.gridCoord.y : 0,
            HasSpawnedThisTurn = false
        };
        SyncOwnerFactionForInspector();
        OnOwnerFactionSet?.Invoke(_state.OwnerFaction);
    }

    /// <summary>
    /// 由 Cell 在放置时调用，当 TilePlaceableType.buildingData 存在时注入。
    /// </summary>
    public void InjectDataFromPlaceableType(BuildingData data)
    {
        if (data != null) _data = data;
        if (_state != null && _data != null)
            _state.CurrentCaptureHp = _data.maxCaptureHp;
    }

    /// <summary>
    /// 从外部初始化（如读档、关卡加载）。可选调用。
    /// </summary>
    public void Initialize(BuildingData data, UnitFaction ownerFaction, int captureHp)
    {
        var previousOwner = OwnerFaction;
        if (data != null) _data = data;
        if (_state == null) _state = new BuildingState();
        _state.OwnerFaction = ownerFaction;
        _state.CurrentCaptureHp = Mathf.Clamp(captureHp, 0, MaxCaptureHp);
        if (_cell != null)
        {
            _state.GridX = _cell.gridCoord.x;
            _state.GridZ = _cell.gridCoord.y;
        }
        SyncOwnerFactionForInspector();
        RaiseOwnerFactionEvents(previousOwner, _state.OwnerFaction);
    }

    public bool ApplyCapture(int power, UnitFaction attackerFaction, UnitController capturer)
    {
        if (_state == null || _data == null)
        {
            OnCaptureAttemptRejected?.Invoke(capturer, attackerFaction, CaptureRejectReason.BuildingMissingDataOrState);
            return false;
        }
        if (_state.OwnerFaction == attackerFaction)
        {
            OnCaptureAttemptRejected?.Invoke(capturer, attackerFaction, CaptureRejectReason.AlreadyOwnedByFaction);
            return false;
        }
        if (capturer == null)
        {
            OnCaptureAttemptRejected?.Invoke(null, attackerFaction, CaptureRejectReason.CapturerMissing);
            return false;
        }

        if (_activeCapturer != null && _activeCapturer != capturer)
            ResetCaptureProgress(CaptureResetReason.CapturerChanged);

        _activeCapturer = capturer;

        var oldHp = _state.CurrentCaptureHp;
        var damage = _data.captureDamagePerStep * Mathf.Max(1, power);
        _state.CurrentCaptureHp = Mathf.Max(0, _state.CurrentCaptureHp - damage);
        OnCaptureProgressChanged?.Invoke(capturer, attackerFaction, oldHp, _state.CurrentCaptureHp, damage);

        if (_state.CurrentCaptureHp <= 0)
        {
            var oldOwner = _state.OwnerFaction;
            _state.OwnerFaction = attackerFaction;
            _state.CurrentCaptureHp = _data.maxCaptureHp;
            ClearActiveCaptureSession();
            SyncOwnerFactionForInspector();
            RaiseOwnerFactionEvents(oldOwner, attackerFaction);
            OnCaptured?.Invoke(oldOwner, attackerFaction);
            return true;
        }

        return false;
    }

    public int GetIncome()
    {
        if (_data == null) return 0;
        return _data.incomePerTurn;
    }

    private void OnEnable()
    {
        TurnManager.OnTurnStarted += HandleTurnStarted;
        if (_cell != null)
            _cell.OnUnitLeft += HandleUnitLeft;
    }

    private void OnDisable()
    {
        TurnManager.OnTurnStarted -= HandleTurnStarted;
        if (_cell != null)
            _cell.OnUnitLeft -= HandleUnitLeft;
    }

    /// <summary>
    /// 回合开始时重置工厂等建筑的造兵标记。
    /// </summary>
    private void HandleTurnStarted(TurnContext context)
    {
        if (_state == null)
            return;
        if (_state.OwnerFaction == context.Faction)
            _state.HasSpawnedThisTurn = false;
    }

    private void SyncOwnerFactionForInspector()
    {
        _currentOwnerFaction = OwnerFaction;
    }

    private void RaiseOwnerFactionEvents(UnitFaction oldFaction, UnitFaction newFaction)
    {
        OnOwnerFactionSet?.Invoke(newFaction);
        if (oldFaction != newFaction)
            OnOwnerFactionChanged?.Invoke(oldFaction, newFaction);
    }

    private void HandleUnitLeft(Cell cell, GameObject leftUnit)
    {
        if (_activeCapturer == null || leftUnit == null)
            return;
        if (leftUnit != _activeCapturer.gameObject)
            return;

        ResetCaptureProgress(CaptureResetReason.CapturerLeftCell);
    }

    private void ResetCaptureProgress(CaptureResetReason reason)
    {
        if (_state == null || _data == null)
            return;

        _state.CurrentCaptureHp = _data.maxCaptureHp;
        var previous = _activeCapturer;
        ClearActiveCaptureSession();
        OnCaptureProgressReset?.Invoke(previous, reason);
    }

    private void ClearActiveCaptureSession()
    {
        _activeCapturer = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            _currentOwnerFaction = _initialOwnerFaction;
        if (_view == null)
            _view = GetComponent<BuildingView>();
    }
#endif
}
