using System;
using UnityEngine;

/// <summary>
/// 兼容层：将旧 GameState 拆分后的模块。
/// </summary>
public class GameStateFacade : Singleton<GameStateFacade>
{
    /// <summary>游戏结束时触发，参数为 (是否胜利, 己方阵营显示名)。由 GameOverDetector 触发，VictoryUIController、AudioEventBinder 等仅监听此事件。</summary>
    public static event Action<bool, string> OnGameOver;
    [SerializeField] private MapRoot _mapRoot;

    [Header("初始状态")]
    [SerializeField] private int _initialDay = 1;
    [SerializeField] private int _initialPlayerIndex;
    [SerializeField] private UnitFaction _initialFaction = UnitFaction.Marigold;
    private GameSessionState _session;

    public MapRoot MapRoot => _mapRoot;
    public GameSessionState Session => _session;

    /// <summary>当前天数（Day）。</summary>
    public int CurrentDay
    {
        get => _session != null ? _session.CurrentDay : 1;
        set
        {
            if (_session != null)
                _session.CurrentDay = value;
        }
    }

    /// <summary>当前玩家在玩家顺序列表中的索引。</summary>
    public int CurrentPlayerIndex
    {
        get => _session != null ? _session.CurrentPlayerIndex : 0;
        set
        {
            if (_session != null)
                _session.CurrentPlayerIndex = value;
        }
    }

    /// <summary>当前行动玩家所属阵营。</summary>
    public UnitFaction CurrentOwnerFaction => _session != null ? _session.CurrentFaction : UnitFaction.None;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        _session = new GameSessionState(_initialDay, _initialPlayerIndex, _initialFaction);
    }

    public void StartNewSession(int day = 1, int playerIndex = 0, UnitFaction faction = UnitFaction.Marigold)
    {
        _session = new GameSessionState(day, playerIndex, faction);
    }

    public void SetSession(GameSessionState session)
    {
        _session = session ?? new GameSessionState(_initialDay, _initialPlayerIndex, _initialFaction);
    }

    private void Start()
    {
        // 正式回合流程由 TurnManager 驱动。
        // 若场景中没有 TurnManager（例如旧测试场景），则保留原有简单逻辑。
        var turnManager = FindTurnManager();
        if (turnManager == null)
        {
            _session.Reset(_initialDay, _initialPlayerIndex, _initialFaction);

            if (CurrentOwnerFaction != UnitFaction.None)
                OnTurnStart(CurrentOwnerFaction);
        }
    }

    /// <summary>获取指定阵营当前资金。</summary>
    public int GetFunds(UnitFaction ownerFaction)
    {
        return GameFundsService.Instance.GetFunds(ownerFaction);
    }

    /// <summary>设置指定阵营资金。</summary>
    public void SetFunds(UnitFaction ownerFaction, int amount)
    {
        GameFundsService.Instance.SetFunds(ownerFaction, amount);
    }

    /// <summary>增加指定阵营资金。</summary>
    public void AddFunds(UnitFaction ownerFaction, int amount)
    {
        GameFundsService.Instance.AddFunds(ownerFaction, amount);
    }

    /// <summary>尝试扣除指定阵营资金，余额不足返回 false。</summary>
    public bool TrySpendFunds(UnitFaction ownerFaction, int amount)
    {
        return GameFundsService.Instance.TrySpendFunds(ownerFaction, amount);
    }

    /// <summary>
    /// 回合开始：收集己方建筑收入，重置工厂造兵状态。
    /// 由外部（如 TurnManager）在切换回合时调用。
    /// </summary>
    public void OnTurnStart(UnitFaction newOwnerFaction)
    {
        if (newOwnerFaction == UnitFaction.None)
            return;

        if (_session != null)
            _session.CurrentFaction = newOwnerFaction;
        TurnLifecycleService.Instance.HandleTurnStart(newOwnerFaction, _mapRoot);
    }

    private static TurnManager FindTurnManager()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<TurnManager>(FindObjectsInactive.Include);
#else
        return FindObjectOfType<TurnManager>();
#endif
    }

    /// <summary>
    /// 检查所有 IGameCondition 建筑（如总部），返回首个非空的胜负结果。
    /// </summary>
    public GameConditionResult CheckWinConditions()
    {
        return GameOverService.Instance.CheckWinConditions(_mapRoot);
    }

    /// <summary>
    /// 通知游戏结束。由 GameOverDetector 在检测到胜负时调用，仅会触发一次（已 IsGameOver 时忽略）。
    /// </summary>
    public void NotifyGameOver(bool isVictory, string selfFactionName)
    {
        if (_session != null && _session.IsGameOver)
            return;
        if (_session != null)
            _session.IsGameOver = true;
        OnGameOver?.Invoke(isVictory, selfFactionName ?? string.Empty);
    }
}
