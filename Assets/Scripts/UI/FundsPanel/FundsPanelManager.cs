using TMPro;
using UnityEngine;

/// <summary>
/// 资金面板：与 GridInfoPanel 隐藏时机一致——指令面板/工厂/范围高亮时隐藏，常态显示当前阵营资金。
/// 工厂界面仍可正常打开，仅在此类状态下隐藏资金面板。
/// </summary>
public class FundsPanelManager : Singleton<FundsPanelManager>
{
    [Header("依赖")]
    [SerializeField] private GameStateFacade _gameStateFacade;

    [Header("根节点")]
    [SerializeField] private GameObject _panelRoot;

    [Header("文本")]
    [SerializeField] private TMP_Text _factionNameText;
    [SerializeField] private TMP_Text _fundSumText;

    [Header("阵营显示名")]
    [SerializeField] private string _marigoldDisplayName = "Marigold";
    [SerializeField] private string _lancelDisplayName = "Lancel";

    private bool _isInitialized;
    private bool _isTurnIntroAnimating;
    private CanvasGroup _panelCanvasGroup;

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
        EnsureInitialized();
        if (_panelRoot != null && _panelRoot == gameObject)
        {
            _panelCanvasGroup = _panelRoot.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null)
                _panelCanvasGroup = _panelRoot.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        TurnManager.OnTurnIntroAnimationStarted += HandleTurnIntroAnimationStarted;
        TurnManager.OnTurnIntroAnimationComplete += HandleTurnIntroAnimationComplete;
    }

    private void OnDisable()
    {
        TurnManager.OnTurnIntroAnimationStarted -= HandleTurnIntroAnimationStarted;
        TurnManager.OnTurnIntroAnimationComplete -= HandleTurnIntroAnimationComplete;
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    /// <summary>需要隐藏资金面板时返回 true。工厂打开时不隐藏，避免与工厂界面共用根节点时把整块关掉。</summary>
    private bool ShouldHideFundsPanel()
    {
        if (_isTurnIntroAnimating)
            return true;
        if (CommandPanelController.IsAnyOpen)
            return true;
        if (HighlightManager.Instance != null && HighlightManager.Instance.HasRangeHighlights)
            return true;
        return false;
    }

    private void Refresh()
    {
        EnsureInitialized();
        var shouldShow = !ShouldHideFundsPanel();
        SetPanelVisible(shouldShow);
        if (!shouldShow)
            return;
        // 工厂打开时显示工厂阵营资金，否则显示当前回合阵营
        var faction = FactoryPanelManager.IsAnyOpen && FactoryPanelManager.Instance != null
            ? FactoryPanelManager.Instance.ActiveFaction
            : ((_gameStateFacade != null && _gameStateFacade.Session != null) ? _gameStateFacade.Session.CurrentFaction : UnitFaction.None);
        if (faction != UnitFaction.None)
            RefreshContent(faction);
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panelRoot == null)
            return;
        if (_panelRoot == gameObject && _panelCanvasGroup != null)
        {
            _panelCanvasGroup.alpha = visible ? 1f : 0f;
            _panelCanvasGroup.blocksRaycasts = visible;
            _panelCanvasGroup.interactable = visible;
            return;
        }
        if (_panelRoot.activeSelf != visible)
            _panelRoot.SetActive(visible);
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        if (_panelRoot == null)
            _panelRoot = gameObject;
        _isInitialized = true;
    }

    /// <summary>显示指定阵营资金（供外部在需要时强制展示用，常态由 Refresh 驱动）。</summary>
    public void Show(UnitFaction faction)
    {
        EnsureInitialized();
        if (_panelRoot != null && _panelRoot != gameObject && !_panelRoot.activeSelf)
            _panelRoot.SetActive(true);
        if (_panelRoot == gameObject && _panelCanvasGroup != null)
        {
            _panelCanvasGroup.alpha = 1f;
            _panelCanvasGroup.blocksRaycasts = true;
            _panelCanvasGroup.interactable = true;
        }
        RefreshContent(faction);
    }

    /// <summary>常态下显示当前回合阵营资金（由 Refresh 自动调用，也可供外部恢复显示用）。</summary>
    public void ShowCurrentFaction()
    {
        Refresh();
    }

    public void Hide()
    {
        EnsureInitialized();
        SetPanelVisible(false);
    }

    private void HandleTurnIntroAnimationStarted(TurnContext _)
    {
        _isTurnIntroAnimating = true;
    }

    private void HandleTurnIntroAnimationComplete(TurnContext _)
    {
        _isTurnIntroAnimating = false;
    }

    private void RefreshContent(UnitFaction faction)
    {
        if (faction == UnitFaction.None)
            return;
        var funds = FactionFundsLedger.Instance.GetFunds(faction);
        SetText(_factionNameText, GetFactionDisplayName(faction));
        SetText(_fundSumText, funds.ToString());
    }

    private string GetFactionDisplayName(UnitFaction faction)
    {
        switch (faction)
        {
            case UnitFaction.Marigold:
                return string.IsNullOrWhiteSpace(_marigoldDisplayName) ? UnitFaction.Marigold.ToString() : _marigoldDisplayName;
            case UnitFaction.Lancel:
                return string.IsNullOrWhiteSpace(_lancelDisplayName) ? UnitFaction.Lancel.ToString() : _lancelDisplayName;
            default:
                return faction.ToString();
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
