using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class VictoryUIController : MonoBehaviour
{
    private enum BuiltinOptionAction
    {
        None = 0,
        RestartGame = 1,
        QuitGame = 2
    }

    [Serializable]
    private sealed class OptionEntry
    {
        public RectTransform root;
        public Image selectedImage;
        public BuiltinOptionAction builtinAction = BuiltinOptionAction.None;
        public UnityEvent onConfirm;
    }

    [Header("Root")]
    [SerializeField] private GameObject _victoryRoot;

    [Header("Fill")]
    [SerializeField] private RectTransform _fillPanel;
    [SerializeField] private float _fillDuration = 0.45f;
    [SerializeField] private AnimationCurve _fillEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Result Image")]
    [SerializeField] private RectTransform _resultImageTransform;
    [SerializeField] private Image _resultImage;
    [SerializeField] private Sprite _victorySprite;
    [SerializeField] private Sprite _defeatSprite;
    [SerializeField] private float _resultImageDuration = 0.32f;
    [SerializeField] private AnimationCurve _resultImageEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Texts")]
    [SerializeField] private TMP_Text _resultText1;
    [SerializeField] private TMP_Text _resultText2;
    [SerializeField] private float _charInterval = 0.04f;

    [Header("Options")]
    [SerializeField] private GameObject _optionPanel;
    [SerializeField] private float _optionPanelDelay = 0.5f;
    [SerializeField] private float _optionPanelFadeDuration = 0.35f;
    [SerializeField] private AnimationCurve _optionPanelFadeEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private RectTransform _cursor;
    [SerializeField] private List<OptionEntry> _options = new List<OptionEntry>();
    [SerializeField] private VerticalMenuNavigator _navigator;
    [SerializeField] private MenuCursorFollower _cursorFollower;

    [Header("Manual trigger fallback（仅用于 PlayVictory/PlayDefeat 的阵营显示）")]
    [SerializeField] private UnitFaction _selfFaction = UnitFaction.Marigold;

    [Header("Game Lock")]
    [SerializeField] private bool _lockTimeScaleOnShow = true;

    [Header("Input Priority")]
    [SerializeField] private int _inputPriority = 5000;

    [Header("Dependencies")]
    [SerializeField] private GameStateFacade _gameStateFacade;

    private bool _isSequenceStarted;
    private bool _isUiBlockingInput;
    private bool _optionInputEnabled;
    private int _selectedIndex = -1;
    private bool _didLockTimeScale;
    private float _cachedTimeScale = 1f;
    private GameSessionState Session => _gameStateFacade != null ? _gameStateFacade.Session : null;

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
        EnsureReferences();
        PrepareInitialVisualState();
    }

    private void OnEnable()
    {
        InputManager.RegisterConfirmHandler(HandleConfirmRequested, _inputPriority);
        InputManager.RegisterCancelHandler(HandleCancelRequested, _inputPriority);
        GameStateFacade.OnGameOver += HandleGameOver;
        EnsureNavigator();
        SyncNavigatorState();
    }

    private void OnDisable()
    {
        InputManager.UnregisterConfirmHandler(HandleConfirmRequested);
        InputManager.UnregisterCancelHandler(HandleCancelRequested);
        GameStateFacade.OnGameOver -= HandleGameOver;

        if (_didLockTimeScale)
        {
            Time.timeScale = _cachedTimeScale;
            _didLockTimeScale = false;
        }
    }

    private void HandleGameOver(bool isVictory, string selfFactionName)
    {
        PlayResult(isVictory, string.IsNullOrEmpty(selfFactionName) ? FormatFactionName(_selfFaction) : selfFactionName);
    }

    public void PlayVictory()
    {
        PlayResult(true, _selfFaction);
    }

    public void PlayDefeat()
    {
        PlayResult(false, _selfFaction);
    }

    public void PlayResult(bool isVictory, UnitFaction selfFaction)
    {
        if (_isSequenceStarted)
            return;

        _selfFaction = selfFaction;
        StartCoroutine(PlaySequence(isVictory, FormatFactionName(selfFaction)));
    }

    public void PlayResult(bool isVictory, string selfFactionName)
    {
        if (_isSequenceStarted)
            return;

        StartCoroutine(PlaySequence(isVictory, selfFactionName));
    }

    private IEnumerator PlaySequence(bool isVictory, string selfFactionName)
    {
        _isSequenceStarted = true;
        _isUiBlockingInput = true;
        _optionInputEnabled = false;
        _selectedIndex = -1;
        SyncNavigatorState();

        // 无论从事件还是手动 PlayVictory/PlayDefeat 进入，都统一触发游戏结束事件，保证 BGM 等监听方收到
        var facade = _gameStateFacade != null ? _gameStateFacade : GameStateFacade.Instance;
        facade?.NotifyGameOver(isVictory, selfFactionName);

        if (_lockTimeScaleOnShow && !_didLockTimeScale)
        {
            _cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _didLockTimeScale = true;
        }

        EnsureReferences();
        PrepareInitialVisualState();

        if (_victoryRoot != null && !_victoryRoot.activeSelf)
            _victoryRoot.SetActive(true);

        GridCursor.Instance?.SetExternalInputLocked(true);
        GridCursor.Instance?.SetVisualVisible(false);

        if (_fillPanel != null)
            yield return AnimateScale(_fillPanel, Vector3.zero, Vector3.one, _fillDuration, _fillEase);

        SetupResultImage(isVictory);

        if (_resultImageTransform != null)
            yield return AnimateScale(_resultImageTransform, Vector3.zero, Vector3.one, _resultImageDuration, _resultImageEase);

        var line1 = isVictory ? "Victory" : "Defeated";
        var line2Top = isVictory ? "You Won!" : "You Lost!";
        var factionSuffix = isVictory ? "!!!" : ".";
        var line2 = string.Concat(line2Top, "\n", selfFactionName, factionSuffix);

        yield return TypewriterUtility.RunTypewriter(this, _resultText1, line1, _charInterval, useUnscaledTime: true);
        yield return TypewriterUtility.RunTypewriter(this, _resultText2, line2, _charInterval, useUnscaledTime: true);

        yield return ShowOptionsWithDelayAndFade();
    }

    private IEnumerator ShowOptionsWithDelayAndFade()
    {
        var delay = Mathf.Max(0f, _optionPanelDelay);
        if (delay > 0f)
        {
            var elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        if (_optionPanel != null && !_optionPanel.activeSelf)
            _optionPanel.SetActive(true);

        var fadeDuration = Mathf.Max(0f, _optionPanelFadeDuration);
        if (_optionPanel != null && fadeDuration > 0f)
        {
            var cg = _optionPanel.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = _optionPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeDuration);
                var k = _optionPanelFadeEase != null ? _optionPanelFadeEase.Evaluate(t) : t;
                cg.alpha = k;
                yield return null;
            }
            cg.alpha = 1f;
        }

        if (_options == null || _options.Count == 0)
        {
            _optionInputEnabled = false;
            _cursorFollower?.SetCursorVisible(false);
            yield break;
        }

        _selectedIndex = 0;
        _optionInputEnabled = true;
        SyncNavigatorItems();
        SyncNavigatorState();
        RefreshOptionSelectionVisuals();
    }

    private IEnumerator AnimateScale(RectTransform target, Vector3 from, Vector3 to, float duration, AnimationCurve ease)
    {
        if (target == null)
            yield break;

        if (duration <= 0f)
        {
            target.localScale = to;
            yield break;
        }

        target.localScale = from;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var k = ease != null ? ease.Evaluate(t) : t;
            target.localScale = Vector3.LerpUnclamped(from, to, k);
            yield return null;
        }

        target.localScale = to;
    }

    private bool HandleConfirmRequested(Vector2Int coord)
    {
        if (!_isUiBlockingInput)
            return false;
        if (!_optionInputEnabled)
            return true;

        _ = coord;
        ExecuteCurrentOption();
        return true;
    }

    private bool HandleCancelRequested()
    {
        return _isUiBlockingInput;
    }

    private void ExecuteCurrentOption()
    {
        if (_options == null || _options.Count == 0)
            return;
        if (_selectedIndex < 0 || _selectedIndex >= _options.Count)
            return;

        var option = _options[_selectedIndex];
        option.onConfirm?.Invoke();

        switch (option.builtinAction)
        {
            case BuiltinOptionAction.RestartGame:
                RestartGame();
                break;
            case BuiltinOptionAction.QuitGame:
                QuitGame();
                break;
        }
    }

    private void RefreshOptionSelectionVisuals()
    {
        if (_options == null)
            return;

        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            if (option == null)
                continue;

            var selected = _optionInputEnabled && i == _selectedIndex;
            ToggleSelectedImage(option.selectedImage, option.root, selected);
        }

        _cursorFollower?.RefreshFromNavigator();
    }

    private void HandleNavigatorSelectionChanged(int index)
    {
        if (_selectedIndex == index)
            return;

        _selectedIndex = index;
        RefreshOptionSelectionVisuals();
    }

    private static void ToggleSelectedImage(Image image, RectTransform optionRoot, bool selected)
    {
        if (image == null)
            return;

        if (optionRoot != null && image.transform == optionRoot)
        {
            if (image.enabled != selected)
                image.enabled = selected;
            return;
        }

        if (image.transform == image.transform.root)
        {
            if (image.enabled != selected)
                image.enabled = selected;
            return;
        }

        if (image.gameObject.activeSelf != selected)
            image.gameObject.SetActive(selected);
    }

    private void SetupResultImage(bool isVictory)
    {
        if (_resultImage != null)
        {
            _resultImage.sprite = isVictory ? _victorySprite : _defeatSprite;
            _resultImage.enabled = _resultImage.sprite != null;
        }

        if (_resultImageTransform != null)
            _resultImageTransform.localScale = Vector3.zero;
    }

    private void PrepareInitialVisualState()
    {
        if (_fillPanel != null)
            _fillPanel.localScale = new Vector3(0f, 0f, 1f);

        if (_resultImageTransform != null)
            _resultImageTransform.localScale = new Vector3(0f, 0f, 1f);

        if (_resultText1 != null)
            _resultText1.text = string.Empty;
        if (_resultText2 != null)
            _resultText2.text = string.Empty;

        if (_optionPanel != null && _optionPanel.activeSelf)
            _optionPanel.SetActive(false);

        _cursorFollower?.SetCursorVisible(false);
        SetAllOptionSelectedVisuals(false);
        SyncNavigatorItems();
        SyncNavigatorState();
    }

    private void SetAllOptionSelectedVisuals(bool selected)
    {
        if (_options == null)
            return;

        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            if (option == null)
                continue;
            ToggleSelectedImage(option.selectedImage, option.root, selected);
        }
    }

    private void EnsureReferences()
    {
        if (_victoryRoot == null)
            _victoryRoot = gameObject;

        if (_fillPanel == null)
            _fillPanel = transform.Find("FillPanel") as RectTransform;

        if (_resultImageTransform == null)
            _resultImageTransform = transform.Find("ResultPanel/ResultImage") as RectTransform;

        if (_resultImage == null && _resultImageTransform != null)
            _resultImage = _resultImageTransform.GetComponent<Image>();

        if (_resultText1 == null)
            _resultText1 = transform.Find("ResultPanel/ResultText_1")?.GetComponent<TMP_Text>();
        if (_resultText2 == null)
            _resultText2 = transform.Find("ResultPanel/ResultText_2")?.GetComponent<TMP_Text>();

        if (_optionPanel == null)
            _optionPanel = transform.Find("ResultPanel/OptionPanel")?.gameObject;

        if (_cursor == null)
            _cursor = transform.Find("ResultPanel/Cursor") as RectTransform;

        EnsureOptionsInitialized();
        EnsureNavigator();
    }

    private void EnsureOptionsInitialized()
    {
        if (_options == null)
            _options = new List<OptionEntry>();

        if (_options.Count == 0)
            AutoCollectOptionEntries();

        for (var i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            if (option == null)
                continue;

            if (option.root == null)
                continue;

            if (option.selectedImage == null)
                option.selectedImage = ResolveSelectedImage(option.root);
        }
    }

    private void EnsureNavigator()
    {
        if (_navigator == null)
            _navigator = GetComponent<VerticalMenuNavigator>();
        if (_navigator == null)
            _navigator = gameObject.AddComponent<VerticalMenuNavigator>();
        if (_cursorFollower == null)
            _cursorFollower = GetComponent<MenuCursorFollower>();
        if (_cursorFollower == null)
            _cursorFollower = gameObject.AddComponent<MenuCursorFollower>();
        if (_cursor != null)
            _cursorFollower.Setup(_cursor, _navigator);

        _navigator.Configure(
            inputPriority: _inputPriority,
            moveMode: VerticalMenuMoveMode.Clamp,
            skipDisabled: false,
            consumeWhenNavigationDisabled: _isUiBlockingInput,
            consumeWhenNoSelectableItems: _isUiBlockingInput);
        _navigator.SelectionChanged -= HandleNavigatorSelectionChanged;
        _navigator.SelectionChanged += HandleNavigatorSelectionChanged;
    }

    private void SyncNavigatorItems()
    {
        if (_navigator == null)
            return;

        var itemRects = new List<RectTransform>();
        if (_options != null)
        {
            for (var i = 0; i < _options.Count; i++)
            {
                var option = _options[i];
                if (option == null || option.root == null)
                    continue;
                itemRects.Add(option.root);
            }
        }

        _navigator.SetItems(itemRects, _selectedIndex, notifySelectionChanged: false);
    }

    private void SyncNavigatorState()
    {
        if (_navigator == null)
            return;

        _navigator.Configure(
            inputPriority: _inputPriority,
            moveMode: VerticalMenuMoveMode.Clamp,
            skipDisabled: false,
            consumeWhenNavigationDisabled: _isUiBlockingInput,
            consumeWhenNoSelectableItems: _isUiBlockingInput);
        _navigator.SetNavigationEnabled(_optionInputEnabled);
        _navigator.SetSelection(_selectedIndex, notifySelectionChanged: false);
    }

    private void AutoCollectOptionEntries()
    {
        if (_optionPanel == null)
            return;

        var panelTransform = _optionPanel.transform;
        for (var i = 0; i < panelTransform.childCount; i++)
        {
            var child = panelTransform.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            var builtinAction = GuessBuiltinActionByName(child.name);
            if (builtinAction == BuiltinOptionAction.None)
            {
                if (i == 0) builtinAction = BuiltinOptionAction.RestartGame;
                else if (i == 1) builtinAction = BuiltinOptionAction.QuitGame;
            }

            var entry = new OptionEntry
            {
                root = child,
                selectedImage = ResolveSelectedImage(child),
                builtinAction = builtinAction
            };
            _options.Add(entry);
        }
    }

    private static Image ResolveSelectedImage(RectTransform optionRoot)
    {
        if (optionRoot == null)
            return null;

        var images = optionRoot.GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var lower = images[i].name.ToLowerInvariant();
            if (lower.Contains("select") ||
                lower.Contains("highlight") ||
                lower.Contains("frame") ||
                lower.Contains("border"))
            {
                return images[i];
            }
        }

        return optionRoot.GetComponent<Image>();
    }

    private static BuiltinOptionAction GuessBuiltinActionByName(string optionName)
    {
        if (string.IsNullOrEmpty(optionName))
            return BuiltinOptionAction.None;

        var lower = optionName.ToLowerInvariant();
        if (lower.Contains("restart"))
            return BuiltinOptionAction.RestartGame;
        if (lower.Contains("quit") || lower.Contains("exit"))
            return BuiltinOptionAction.QuitGame;

        return BuiltinOptionAction.None;
    }

    private static string FormatFactionName(UnitFaction faction)
    {
        return faction == UnitFaction.None ? "Unknown" : faction.ToString();
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        if (Session != null)
            Session.IsGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
