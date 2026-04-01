using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// DayInfoPanel 控制器：
/// - 常态显示当前阵营名与天数；
/// - 仅在范围高光存在时隐藏；
/// - 天数切换时播放完整动画（含太阳绕圈）；仅轮次切换时播放小动画（无太阳，其余同参数）。
/// </summary>
public class DayInfoPanelController : MonoBehaviour
{
    [Header("依赖")]
    [SerializeField] private GameStateFacade _gameStateFacade;

    [Header("Root")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private RectTransform _panelRect;

    [Header("UI Refs")]
    [SerializeField] private TMP_Text _factionNameText;
    [SerializeField] private TMP_Text _dayCountText;
    [SerializeField] private RectTransform _sunIconRect;
    [SerializeField] private RectTransform _turnIconRect;
    [Tooltip("第二个绅士图标（turn-icon (1)），与 turn-icon 镜像同步旋转，初始为 Y 轴 180°。")]
    [SerializeField] private RectTransform _turnIconMirrorRect;

    [Header("Faction Display Name")]
    [SerializeField] private string _marigoldDisplayName = "Marigold";
    [SerializeField] private string _lancelDisplayName = "Lancel";

    [Header("Animation - Panel")]
    [SerializeField] private float _panelScaleUpDuration = 0.45f;
    [SerializeField] private float _panelScaleDownDuration = 0.45f;
    [SerializeField] private float _panelScaleExpanded = 2.5f;

    [Header("Animation - Sun Move")]
    [Tooltip("绕圈半径（父节点 DayInfoPanel 本地空间，与 localPosition 同单位）。太阳中心会移到该半径的圆上并绕父节点锚点旋转。")]
    [SerializeField] private float _sunOrbitRadius = 80f;
    [SerializeField] private float _sunMoveToOrbitDuration = 0.4f;
    [SerializeField] private float _sunMoveBackDuration = 0.4f;

    [Header("Animation - Orbit")]
    [Tooltip("sun-icon 绕圈一周耗时。")]
    [SerializeField] private float _sunOrbitDuration = 1.4f;
    [Tooltip("sun-icon 旋转方向。true=逆时针。")]
    [SerializeField] private bool _orbitCounterClockwise = true;

    [Header("Animation - Turn Icon Spin")]
    [Tooltip("sun-icon 绕圈一周期间，turn-icon Y 轴转几圈。")]
    [SerializeField] private float _turnIconSpinRevolutions = 1.25f;

    [Header("Animation - Name Transition")]
    [Range(0.1f, 0.9f)]
    [Tooltip("旧名字删除占整段绕圈时长的比例。剩余时间用于输入新名字。")]
    [SerializeField] private float _erasePhaseRatio = 0.45f;

    /// <summary>小太阳开始绕圈公转时触发，参数为公转一周的时长（秒）。与 RunTurnIconAndNameTransition 中 orbitSun 为 true 时同步。</summary>
    public event Action<float> OnSunOrbitStarted;

    private CanvasGroup _panelCanvasGroup;
    private bool _isAnimating;
    private Coroutine _dayChangeRoutine;

    private Vector3 _panelOriginalScale = Vector3.one;
    private Vector3 _sunOriginalLocalPosition;
    private Quaternion _turnOriginalLocalRotation = Quaternion.identity;
    private Quaternion _turnIconMirrorOriginalLocalRotation = Quaternion.identity;

    private int _lastDay;
    private UnitFaction _lastFaction = UnitFaction.None;

    private void Reset()
    {
        if (_panelRoot == null)
            _panelRoot = gameObject;
        if (_panelRect == null)
            _panelRect = transform as RectTransform;
    }

    private void Awake()
    {
        if (_gameStateFacade == null)
        {
#if UNITY_2023_1_OR_NEWER
            _gameStateFacade = FindFirstObjectByType<GameStateFacade>(FindObjectsInactive.Include);
#else
            _gameStateFacade = FindObjectOfType<GameStateFacade>();
#endif
        }
        EnsureInitialized();
        CacheOriginalPose();
    }

    private void Start()
    {
        // Session 在 GameStateFacade.Awake 中创建，Start 时已就绪，在此同步阵营名与天数
        Initialize();
    }

    /// <summary>
    /// 初始化面板：从当前游戏状态同步阵营名、天数等显示，并恢复图标初始姿态。
    /// 可在外部调用以在适当时机刷新面板（如关卡加载后）。
    /// </summary>
    public void Initialize()
    {
        EnsureInitialized();
        CacheOriginalPose();
        SyncLastTurnState();
        RefreshImmediate();

        if (_turnIconRect != null)
            _turnIconRect.localRotation = _turnOriginalLocalRotation;
        if (_turnIconMirrorRect != null)
            _turnIconMirrorRect.localRotation = _turnIconMirrorOriginalLocalRotation;
    }

    private void OnEnable()
    {
        TurnManager.OnTurnStarted += HandleTurnStarted;
        TurnManager.OnDayChanged += HandleDayChanged;
        Initialize();
    }

    private void OnDisable()
    {
        TurnManager.OnTurnStarted -= HandleTurnStarted;
        TurnManager.OnDayChanged -= HandleDayChanged;
        if (_isAnimating)
            InputManager.UnblockInput();
    }

    private void Update()
    {
        SetPanelVisible(!ShouldHideDayInfoPanel());
    }

    private static bool ShouldHideDayInfoPanel()
    {
        return HighlightManager.Instance != null && HighlightManager.Instance.HasRangeHighlights;
    }

    private void EnsureInitialized()
    {
        if (_panelRoot == null)
            _panelRoot = gameObject;
        if (_panelRect == null)
            _panelRect = _panelRoot != null ? _panelRoot.transform as RectTransform : null;

        if (_panelRoot == gameObject)
        {
            _panelCanvasGroup = _panelRoot.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null)
                _panelCanvasGroup = _panelRoot.AddComponent<CanvasGroup>();
        }
    }

    private void CacheOriginalPose()
    {
        if (_panelRect != null)
            _panelOriginalScale = _panelRect.localScale;
        if (_sunIconRect != null)
            _sunOriginalLocalPosition = _sunIconRect.localPosition;
        if (_turnIconRect != null)
            _turnOriginalLocalRotation = _turnIconRect.localRotation;
        if (_turnIconMirrorRect != null)
            _turnIconMirrorOriginalLocalRotation = _turnIconMirrorRect.localRotation;
    }

    private void SyncLastTurnState()
    {
        if (_gameStateFacade == null)
            return;
        _lastDay = _gameStateFacade.CurrentDay;
        _lastFaction = _gameStateFacade.CurrentOwnerFaction;
    }

    private void HandleTurnStarted(TurnContext context)
    {
        if (_isAnimating)
            return;

        if (context.Day != _lastDay)
        {
            _lastDay = context.Day;
            _lastFaction = context.Faction;
            return;
        }

        if (context.Faction == _lastFaction)
        {
            SetText(_factionNameText, GetFactionDisplayName(context.Faction));
            SetText(_dayCountText, context.Day.ToString());
            return;
        }

        _lastFaction = context.Faction;
        var newFactionName = GetFactionDisplayName(context.Faction);
        var oldFactionName = _factionNameText != null ? _factionNameText.text : string.Empty;
        if (string.IsNullOrEmpty(oldFactionName))
            oldFactionName = newFactionName;

        if (_dayChangeRoutine != null)
        {
            StopCoroutine(_dayChangeRoutine);
            InputManager.UnblockInput();
        }

        _dayChangeRoutine = StartCoroutine(PlayTurnSwitchAnimation(oldFactionName, newFactionName, context.Day));
    }

    private void HandleDayChanged(int newDay)
    {
        if (!isActiveAndEnabled)
            return;

        var session = _gameStateFacade != null ? _gameStateFacade.Session : null;
        var newFaction = session != null ? session.CurrentFaction : UnitFaction.None;
        var newFactionName = GetFactionDisplayName(newFaction);
        var oldFactionName = _factionNameText != null ? _factionNameText.text : string.Empty;
        if (string.IsNullOrEmpty(oldFactionName))
            oldFactionName = newFactionName;

        if (_dayChangeRoutine != null)
        {
            StopCoroutine(_dayChangeRoutine);
            InputManager.UnblockInput();
        }

        _dayChangeRoutine = StartCoroutine(PlayDayChangeAnimation(oldFactionName, newFactionName, newDay, newFaction));
    }

    private void RefreshImmediate()
    {
        if (_gameStateFacade == null)
            return;

        SetText(_factionNameText, GetFactionDisplayName(_gameStateFacade.CurrentOwnerFaction));
        SetText(_dayCountText, _gameStateFacade.CurrentDay.ToString());
        SetPanelVisible(!ShouldHideDayInfoPanel());
    }

    private IEnumerator PlayDayChangeAnimation(string oldFactionName, string newFactionName, int newDay, UnitFaction newFaction)
    {
        TurnManager.ReportIntroAnimationStarted();
        _isAnimating = true;
        InputManager.BlockInput();

        EnsureInitialized();
        CacheOriginalPose();

        if (_panelRect != null)
            yield return ScalePanel(_panelOriginalScale, Vector3.one * _panelScaleExpanded, _panelScaleUpDuration);

        if (_sunIconRect != null)
            yield return MoveSunLocal(_sunOriginalLocalPosition, SunOrbitStartLocalPosition(), _sunMoveToOrbitDuration);

        yield return RunTurnIconAndNameTransition(oldFactionName, newFactionName, newDay, _sunOrbitDuration, orbitSun: true);

        if (_sunIconRect != null)
            yield return MoveSunLocal(SunOrbitStartLocalPosition(), _sunOriginalLocalPosition, _sunMoveBackDuration);

        if (_panelRect != null)
            yield return ScalePanel(Vector3.one * _panelScaleExpanded, _panelOriginalScale, _panelScaleDownDuration);

        if (_turnIconRect != null)
            _turnIconRect.localRotation = _turnOriginalLocalRotation;
        if (_turnIconMirrorRect != null)
            _turnIconMirrorRect.localRotation = _turnIconMirrorOriginalLocalRotation;

        SetText(_factionNameText, newFactionName);
        SetText(_dayCountText, newDay.ToString());
        _lastDay = newDay;
        _lastFaction = newFaction;

        TurnManager.ReportIntroAnimationComplete();
        InputManager.UnblockInput();
        _isAnimating = false;
        _dayChangeRoutine = null;
    }

    private IEnumerator PlayTurnSwitchAnimation(string oldFactionName, string newFactionName, int day)
    {
        TurnManager.ReportIntroAnimationStarted();
        _isAnimating = true;
        InputManager.BlockInput();

        EnsureInitialized();
        CacheOriginalPose();

        if (_panelRect != null)
            yield return ScalePanel(_panelOriginalScale, Vector3.one * _panelScaleExpanded, _panelScaleUpDuration);

        yield return RunTurnIconAndNameTransition(oldFactionName, newFactionName, day, _sunOrbitDuration, orbitSun: false);

        if (_panelRect != null)
            yield return ScalePanel(Vector3.one * _panelScaleExpanded, _panelOriginalScale, _panelScaleDownDuration);

        if (_turnIconRect != null)
            _turnIconRect.localRotation = _turnOriginalLocalRotation;
        if (_turnIconMirrorRect != null)
            _turnIconMirrorRect.localRotation = _turnIconMirrorOriginalLocalRotation;

        SetText(_factionNameText, newFactionName);
        SetText(_dayCountText, day.ToString());

        TurnManager.ReportIntroAnimationComplete();
        InputManager.UnblockInput();
        _isAnimating = false;
        _dayChangeRoutine = null;
    }

    /// <summary>轨道起点：父节点本地空间下圆底部 (0, -radius)，即绕 DayInfoPanel 锚点(0,0) 的圆周上。</summary>
    private Vector3 SunOrbitStartLocalPosition()
    {
        var z = _sunIconRect != null ? _sunIconRect.localPosition.z : 0f;
        return new Vector3(0f, -_sunOrbitRadius, z);
    }

    /// <summary>绅士旋转 + 阵营名擦除/输入，可选是否同时做太阳绕圈。与天数动画共用时长和参数。</summary>
    private IEnumerator RunTurnIconAndNameTransition(string oldFactionName, string newFactionName, int dayToShow, float duration, bool orbitSun)
    {
        SetText(_dayCountText, dayToShow.ToString());

        var eraseDuration = Mathf.Max(0.01f, duration * _erasePhaseRatio);
        var typeDuration = Mathf.Max(0.01f, duration - eraseDuration);
        var oldLen = string.IsNullOrEmpty(oldFactionName) ? 0 : oldFactionName.Length;
        var newLen = string.IsNullOrEmpty(newFactionName) ? 0 : newFactionName.Length;

        float radius = 0f;
        float z = 0f;
        float startAngleRad = 0f;
        float directionSign = 1f;
        if (orbitSun && _sunIconRect != null)
        {
            radius = Mathf.Max(0.001f, _sunOrbitRadius);
            z = _sunIconRect.localPosition.z;
            startAngleRad = -Mathf.PI * 0.5f;
            directionSign = _orbitCounterClockwise ? 1f : -1f;
            OnSunOrbitStarted?.Invoke(duration);
        }

        var elapsed = 0f;
        var lastTypedCount = 0;
        var lastRemain = oldLen;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);

            if (orbitSun && _sunIconRect != null)
            {
                var angle = startAngleRad + directionSign * t * Mathf.PI * 2f;
                _sunIconRect.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
            }

            if (_turnIconRect != null)
            {
                var yAngle = 360f * _turnIconSpinRevolutions * t;
                _turnIconRect.localRotation = _turnOriginalLocalRotation * Quaternion.Euler(0f, yAngle, 0f);
                if (_turnIconMirrorRect != null)
                    _turnIconMirrorRect.localRotation = _turnIconMirrorOriginalLocalRotation * Quaternion.Euler(0f, -yAngle, 0f);
            }

            if (elapsed <= eraseDuration)
            {
                var eraseT = Mathf.Clamp01(elapsed / eraseDuration);
                var remain = Mathf.Clamp(Mathf.CeilToInt((1f - eraseT) * oldLen), 0, oldLen);
                SetText(_factionNameText, oldFactionName.Substring(0, remain));
                for (var i = lastRemain - 1; i >= remain && i >= 0 && i < oldFactionName.Length; i--)
                    TypewriterUtility.NotifyCharacterTyped(i, oldFactionName[i]);
                lastRemain = remain;
                lastTypedCount = 0;
            }
            else
            {
                var typeT = Mathf.Clamp01((elapsed - eraseDuration) / typeDuration);
                var count = Mathf.Clamp(Mathf.FloorToInt(typeT * newLen), 0, newLen);
                SetText(_factionNameText, newFactionName.Substring(0, count));
                for (var i = lastTypedCount; i < count && i < newFactionName.Length; i++)
                    TypewriterUtility.NotifyCharacterTyped(i, newFactionName[i]);
                lastTypedCount = count;
            }

            yield return null;
        }

        if (orbitSun && _sunIconRect != null)
            _sunIconRect.localPosition = SunOrbitStartLocalPosition();
        if (_turnIconRect != null)
            _turnIconRect.localRotation = _turnOriginalLocalRotation;
        if (_turnIconMirrorRect != null)
            _turnIconMirrorRect.localRotation = _turnIconMirrorOriginalLocalRotation;
        SetText(_factionNameText, newFactionName);
    }

    private IEnumerator ScalePanel(Vector3 from, Vector3 to, float duration)
    {
        if (_panelRect == null)
            yield break;

        if (duration <= 0f)
        {
            _panelRect.localScale = to;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            _panelRect.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        _panelRect.localScale = to;
    }

    private IEnumerator MoveSunLocal(Vector3 from, Vector3 to, float duration)
    {
        if (_sunIconRect == null)
            yield break;

        if (duration <= 0f)
        {
            _sunIconRect.localPosition = to;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            _sunIconRect.localPosition = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        _sunIconRect.localPosition = to;
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
