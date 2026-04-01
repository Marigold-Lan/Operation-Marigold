using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CommandPanelController : Singleton<CommandPanelController>
{
    private const string PanelOffsetXPrefKey = "CommandPanel.OffsetX";
    private const string PanelOffsetYPrefKey = "CommandPanel.OffsetY";

    [Header("引用")]
    [SerializeField] private RectTransform _panelRoot;
    [SerializeField] private RectTransform _optionContainer;
    [SerializeField] private CommandOptionItem _optionItemPrefab;
    [SerializeField] private RectTransform _commandCursor;
    [SerializeField] private VerticalMenuNavigator _navigator;
    [SerializeField] private MenuCursorFollower _cursorFollower;
    [SerializeField] private Canvas _canvas;
    [Header("命令图标配置")]
    [SerializeField] private Sprite _captureIcon;
    [SerializeField] private Sprite _loadIcon;
    [SerializeField] private Sprite _dropIcon;
    [SerializeField] private Sprite _supplyIcon;
    [SerializeField] private Sprite _fireIcon;
    [SerializeField] private Sprite _waitIcon;

    [Header("位置")]
    [SerializeField] private Vector2 _screenPadding = new Vector2(16f, 16f);
    [SerializeField] private Vector2 _panelScreenOffset = new Vector2(170f, 90f);

    [Header("运行时微调")]
    [SerializeField] private bool _enableRuntimeOffsetTuning = true;
    [SerializeField] private KeyCode _tuningModifierKey = KeyCode.LeftAlt;
    [SerializeField] private KeyCode _moveUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode _moveDownKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode _moveLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode _moveRightKey = KeyCode.RightArrow;
    [SerializeField] private float _tuningStepPixels = 10f;
    [SerializeField] private bool _loadSavedOffsetOnStart = true;
    [SerializeField] private bool _saveOffsetOnAdjust = true;

    private readonly List<CommandOption> _options = new List<CommandOption>();
    private readonly List<CommandOptionItem> _items = new List<CommandOptionItem>();

    private CommandContext _context;
    private int _selectedIndex;
    private bool _isInitialized;

    public bool IsOpen { get; private set; }
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    protected override void Awake()
    {
        base.Awake();
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        if (_panelRoot == null)
            _panelRoot = transform as RectTransform;
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>(true);
        if (_commandCursor == null)
            _commandCursor = transform.Find("CommandCursor") as RectTransform;
        if (_navigator == null)
            _navigator = GetComponent<VerticalMenuNavigator>();
        if (_navigator == null)
            _navigator = gameObject.AddComponent<VerticalMenuNavigator>();

        _navigator.Configure(
            inputPriority: 200,
            moveMode: VerticalMenuMoveMode.Wrap,
            skipDisabled: true,
            consumeWhenNavigationDisabled: false,
            consumeWhenNoSelectableItems: true);
        _navigator.SetSelectableEvaluator(IsOptionSelectable);
        _navigator.SelectionChanged -= HandleSelectionChanged;
        _navigator.SelectionChanged += HandleSelectionChanged;
        _navigator.SetNavigationEnabled(IsOpen);
        if (_cursorFollower == null)
            _cursorFollower = GetComponent<MenuCursorFollower>();
        if (_cursorFollower == null)
            _cursorFollower = gameObject.AddComponent<MenuCursorFollower>();
        _cursorFollower.Setup(_commandCursor, _navigator);
        if (_loadSavedOffsetOnStart)
            LoadOffsetFromPrefs();

        _isInitialized = true;
    }

    private void Start()
    {
        if (!IsOpen)
            Close();
    }

    private void OnEnable()
    {
        InputManager.RegisterConfirmHandler(HandleConfirmRequested, priority: 200);
        InputManager.RegisterCancelHandler(HandleCancelRequested, priority: 200);
    }

    private void OnDisable()
    {
        InputManager.UnregisterConfirmHandler(HandleConfirmRequested);
        InputManager.UnregisterCancelHandler(HandleCancelRequested);
    }

    private void Update()
    {
        UpdateRuntimeOffsetTuning();
    }

    public void Open(CommandContext context, List<CommandOption> options, Vector2 screenPosition)
    {
        EnsureInitialized();

        if (_panelRoot == null || _optionContainer == null || _optionItemPrefab == null)
            return;

        _context = context;
        _options.Clear();
        if (options != null)
            _options.AddRange(options);
        ApplyConfiguredIcons();

        RebuildOptionItems();
        _selectedIndex = FindFirstSelectableIndex();
        if (_selectedIndex < 0)
            _selectedIndex = 0;

        _panelRoot.gameObject.SetActive(true);
        IsOpen = true;
        GridCursor.Instance?.SetExternalInputLocked(true);

        PlacePanel(screenPosition);
        RefreshNavigatorItems();
        _navigator.SetNavigationEnabled(true);
        _navigator.SetSelection(_selectedIndex, notifySelectionChanged: false);
        RefreshVisual();
    }

    public void Close()
    {
        EnsureInitialized();

        IsOpen = false;
        _context = null;
        _options.Clear();
        ClearOptionItems();

        if (_panelRoot != null)
            _panelRoot.gameObject.SetActive(false);

        GridCursor.Instance?.SetExternalInputLocked(false);
        _navigator?.SetNavigationEnabled(false);
        _navigator?.SetSelection(-1, notifySelectionChanged: false);
    }

    private bool HandleConfirmRequested(Vector2Int coord)
    {
        if (!IsOpen || _options.Count == 0) return false;
        if (_selectedIndex < 0 || _selectedIndex >= _options.Count) return true;

        var option = _options[_selectedIndex];
        if (!option.Interactable) return true;

        GameAudioManager.Instance?.PlayUi(AudioCueId.UiConfirm);
        var context = _context;
        Close();
        CommandExecutor.Execute(option.Command, context);
        return true;
    }

    private bool HandleCancelRequested()
    {
        if (!IsOpen) return false;

        GameAudioManager.Instance?.PlayUi(AudioCueId.UiCancel);
        var context = _context;
        Close();

        if (context != null && context.ConsumeActionOnCancel && context.Unit != null)
        {
            context.Unit.HasActed = true;
            context.HighlightManager?.ClearMoveHighlights();
            context.HighlightManager?.ClearAttackHighlights();
            context.SelectionManager?.ClearSelection();
        }

        return true;
    }

    private void RebuildOptionItems()
    {
        ClearOptionItems();

        for (var i = 0; i < _options.Count; i++)
        {
            var item = Instantiate(_optionItemPrefab, _optionContainer);
            item.Bind(_options[i], false);
            _items.Add(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_optionContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRoot);
    }

    private void ApplyConfiguredIcons()
    {
        for (var i = 0; i < _options.Count; i++)
        {
            if (_options[i] == null || _options[i].Icon != null) continue;
            _options[i].Icon = GetIconByCommandType(_options[i].Type);
        }
    }

    private Sprite GetIconByCommandType(CommandType type)
    {
        switch (type)
        {
            case CommandType.Capture:
                return _captureIcon;
            case CommandType.Load:
                return _loadIcon;
            case CommandType.Drop:
                return _dropIcon;
            case CommandType.Supply:
                return _supplyIcon;
            case CommandType.Fire:
                return _fireIcon;
            case CommandType.Wait:
                return _waitIcon;
            default:
                return null;
        }
    }

    private void ClearOptionItems()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null)
                Destroy(_items[i].gameObject);
        }
        _items.Clear();
    }

    private int FindFirstSelectableIndex()
    {
        for (var i = 0; i < _options.Count; i++)
        {
            if (_options[i].Interactable)
                return i;
        }
        return -1;
    }

    private void RefreshVisual()
    {
        for (var i = 0; i < _items.Count; i++)
            _items[i].RefreshVisual(i == _selectedIndex);
        _cursorFollower?.RefreshFromNavigator();
    }

    private void HandleSelectionChanged(int index)
    {
        if (_selectedIndex == index)
            return;

        _selectedIndex = index;
        RefreshVisual();
    }

    private bool IsOptionSelectable(int index)
    {
        if (_options == null || index < 0 || index >= _options.Count)
            return false;
        if (_items == null || index >= _items.Count || _items[index] == null)
            return false;

        return _options[index].Interactable;
    }

    private void RefreshNavigatorItems()
    {
        if (_navigator == null)
            return;

        var itemRects = new List<RectTransform>();
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i] == null)
                continue;
            var rect = _items[i].RectTransform != null
                ? _items[i].RectTransform
                : _items[i].transform as RectTransform;
            if (rect != null)
                itemRects.Add(rect);
        }

        _navigator.SetItems(itemRects, _selectedIndex, notifySelectionChanged: false);
    }

    private void PlacePanel(Vector2 screenPosition)
    {
        if (_panelRoot == null) return;
        screenPosition += _panelScreenOffset;

        var canvasRect = _canvas != null ? _canvas.transform as RectTransform : _panelRoot.parent as RectTransform;
        if (canvasRect == null)
        {
            _panelRoot.position = screenPosition;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out var localPoint);

        var halfWidth = _panelRoot.rect.width * 0.5f;
        var halfHeight = _panelRoot.rect.height * 0.5f;
        var canvasHalfWidth = canvasRect.rect.width * 0.5f;
        var canvasHalfHeight = canvasRect.rect.height * 0.5f;

        var xMin = -canvasHalfWidth + halfWidth + _screenPadding.x;
        var xMax = canvasHalfWidth - halfWidth - _screenPadding.x;
        var yMin = -canvasHalfHeight + halfHeight + _screenPadding.y;
        var yMax = canvasHalfHeight - halfHeight - _screenPadding.y;

        localPoint.x = Mathf.Clamp(localPoint.x, xMin, xMax);
        localPoint.y = Mathf.Clamp(localPoint.y, yMin, yMax);
        _panelRoot.anchoredPosition = localPoint;
    }

    private void UpdateRuntimeOffsetTuning()
    {
        if (!_enableRuntimeOffsetTuning) return;
        if (!IsOpen) return;
        if (!Input.GetKey(_tuningModifierKey)) return;

        var delta = Vector2.zero;
        if (Input.GetKeyDown(_moveUpKey)) delta.y += _tuningStepPixels;
        if (Input.GetKeyDown(_moveDownKey)) delta.y -= _tuningStepPixels;
        if (Input.GetKeyDown(_moveLeftKey)) delta.x -= _tuningStepPixels;
        if (Input.GetKeyDown(_moveRightKey)) delta.x += _tuningStepPixels;
        if (delta == Vector2.zero) return;

        _panelScreenOffset += delta;
        if (_saveOffsetOnAdjust)
            SaveOffsetToPrefs();

        var unitScreenPos = GetCurrentContextScreenPos();
        PlacePanel(unitScreenPos);
    }

    private Vector2 GetCurrentContextScreenPos()
    {
        var unit = _context != null ? _context.Unit : null;
        var worldAnchor = unit != null ? unit.transform.position : Vector3.zero;
        return RectTransformUtility.WorldToScreenPoint(Camera.main, worldAnchor);
    }

    private void SaveOffsetToPrefs()
    {
        PlayerPrefs.SetFloat(PanelOffsetXPrefKey, _panelScreenOffset.x);
        PlayerPrefs.SetFloat(PanelOffsetYPrefKey, _panelScreenOffset.y);
        PlayerPrefs.Save();
    }

    private void LoadOffsetFromPrefs()
    {
        if (!PlayerPrefs.HasKey(PanelOffsetXPrefKey) || !PlayerPrefs.HasKey(PanelOffsetYPrefKey))
            return;

        _panelScreenOffset = new Vector2(
            PlayerPrefs.GetFloat(PanelOffsetXPrefKey),
            PlayerPrefs.GetFloat(PanelOffsetYPrefKey));
    }
}
