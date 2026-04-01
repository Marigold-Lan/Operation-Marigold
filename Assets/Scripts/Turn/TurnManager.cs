using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 核心回合管理器：负责 Day / Player / Phase 的切换，并通过事件通知其他系统。
/// 不直接操作具体单位或建筑，只负责“报时”和同步会话状态。
/// </summary>
public class TurnManager : Singleton<TurnManager>
{
    [Header("依赖")]
    [SerializeField] private GameStateFacade _gameStateFacade;

    [Header("玩家顺序（阵营列表）")]
    [SerializeField] private List<UnitFaction> _playerOrder = new List<UnitFaction>
    {
        UnitFaction.Marigold,
        UnitFaction.Lancel
    };

    [Header("起始天数")]
    [SerializeField] private int _startingDay = 1;

    private int _currentDay;
    private int _currentPlayerIndex;
    private TurnPhase _phase = TurnPhase.Start;

    public static event Action<int> OnDayChanged;
    public static event Action<TurnContext> OnTurnStarted;
    public static event Action<TurnContext> OnTurnMainPhase;
    public static event Action<TurnContext> OnTurnEnded;

    /// <summary>回合开场动画开始时触发（由 ReportIntroAnimationStarted 调用）。供 UI 在开场期间隐藏/锁定某些元素。</summary>
    public static event Action<TurnContext> OnTurnIntroAnimationStarted;

    /// <summary>回合开场动画结束后触发，供可行动 Mark 等表现使用。无开场时由 TurnManager 下一帧自动触发。</summary>
    public static event Action<TurnContext> OnTurnIntroAnimationComplete;

    private bool _introAnimationReportedThisTurn;
    private GameSessionState Session => _gameStateFacade != null ? _gameStateFacade.Session : null;

    /// <summary>当前回合上下文。</summary>
    public TurnContext CurrentContext => new TurnContext
    {
        Day = _currentDay,
        PlayerIndex = _currentPlayerIndex,
        Faction = GetCurrentPlayerFaction(),
        Phase = _phase
    };

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
        _currentDay = Mathf.Max(1, _startingDay);
        _currentPlayerIndex = Mathf.Clamp(_currentPlayerIndex, 0, Mathf.Max(0, (_playerOrder?.Count ?? 1) - 1));
    }

    private void Start()
    {
        EnsurePlayerOrderValid();
        StartTurn();
    }

    /// <summary>由播放回合开场动画的 UI（如 DayInfoPanelController）在动画开始时调用，避免无动画回退触发。</summary>
    public static void ReportIntroAnimationStarted()
    {
        if (Instance == null)
            return;

        Instance._introAnimationReportedThisTurn = true;
        var context = Instance.CurrentContext;
        OnTurnIntroAnimationStarted?.Invoke(context);
    }

    /// <summary>由播放回合开场动画的 UI 在动画结束时调用，会触发 OnTurnIntroAnimationComplete。</summary>
    public static void ReportIntroAnimationComplete()
    {
        if (Instance == null) return;
        Instance._introAnimationReportedThisTurn = false;
        var context = Instance.CurrentContext;
        OnTurnIntroAnimationComplete?.Invoke(context);
    }

    /// <summary>
    /// 供 UI 调用：玩家点击“结束回合”按钮。
    /// </summary>
    public void PlayerClickEndTurn()
    {
        if (Session != null && Session.IsGameOver)
            return;
        if (_phase != TurnPhase.Main)
            return;

        EnterEndPhase();
    }

    private void StartTurn()
    {
        _phase = TurnPhase.Start;
        SyncSessionState();

        var context = CurrentContext;

        if (context.Faction != UnitFaction.None)
        {
            var preferredRoot = _gameStateFacade != null ? _gameStateFacade.MapRoot : null;
            TurnLifecycleService.Instance.HandleTurnStart(context.Faction, preferredRoot);
        }

        OnTurnStarted?.Invoke(context);

        EnterMainPhase();
    }

    private void EnterMainPhase()
    {
        _phase = TurnPhase.Main;
        SyncSessionState();
        var context = CurrentContext;
        OnTurnMainPhase?.Invoke(context);
        StartCoroutine(NotifyIntroCompleteNextFrameIfNoAnimation(context));
    }

    private System.Collections.IEnumerator NotifyIntroCompleteNextFrameIfNoAnimation(TurnContext context)
    {
        yield return null;
        if (!_introAnimationReportedThisTurn)
            OnTurnIntroAnimationComplete?.Invoke(context);
    }

    private void EnterEndPhase()
    {
        if (Session != null && Session.IsGameOver)
            return;

        _phase = TurnPhase.End;
        SyncSessionState();
        var context = CurrentContext;
        OnTurnEnded?.Invoke(context);

        AdvanceToNextPlayerOrDay();
    }

    private void AdvanceToNextPlayerOrDay()
    {
        _introAnimationReportedThisTurn = false;
        EnsurePlayerOrderValid();
        if (_playerOrder.Count == 0)
            return;

        _currentPlayerIndex++;
        if (_currentPlayerIndex >= _playerOrder.Count)
        {
            _currentPlayerIndex = 0;
            _currentDay++;
            SyncSessionState();
            OnDayChanged?.Invoke(_currentDay);
        }

        StartTurn();
    }

    private void SyncSessionState()
    {
        var session = Session;
        if (session == null)
            return;
        session.CurrentDay = _currentDay;
        session.CurrentPlayerIndex = _currentPlayerIndex;
        session.CurrentFaction = GetCurrentPlayerFaction();
    }

    private void EnsurePlayerOrderValid()
    {
        if (_playerOrder == null)
            _playerOrder = new List<UnitFaction>();
        if (_playerOrder.Count == 0)
            _playerOrder.Add(UnitFaction.Marigold);
    }

    private UnitFaction GetCurrentPlayerFaction()
    {
        EnsurePlayerOrderValid();
        if (_currentPlayerIndex < 0 || _currentPlayerIndex >= _playerOrder.Count)
            return UnitFaction.None;
        return _playerOrder[_currentPlayerIndex];
    }

    /// <summary>玩家顺序列表中的第一个阵营（开局先手）。用于镜头/光标初始聚焦己方总部等。</summary>
    public UnitFaction GetFirstFactionInPlayerOrder()
    {
        EnsurePlayerOrderValid();
        return _playerOrder.Count > 0 ? _playerOrder[0] : UnitFaction.None;
    }
}

