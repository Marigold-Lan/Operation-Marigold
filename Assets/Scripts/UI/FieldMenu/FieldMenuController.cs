using System.Collections.Generic;
using UnityEngine;

public class FieldMenuController : Singleton<FieldMenuController>
{
    [Header("根节点")]
    [SerializeField] private GameObject _panelRoot;

    [Header("选项（顺序：0=EndTurn，1=QuitGame）")]
    [SerializeField] private List<FieldMenuOptionItem> _optionItems = new List<FieldMenuOptionItem>();
    [Header("菜单光标")]
    [SerializeField] private RectTransform _fieldMenuCursor;
    [SerializeField] private VerticalMenuNavigator _navigator;
    [SerializeField] private MenuCursorFollower _cursorFollower;

    private int _selectedIndex = -1;
    private bool _isInitialized;

    public bool IsOpen { get; private set; }
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;

    protected override void Awake()
    {
        base.Awake();
        EnsureInitialized();
    }

    private void Start()
    {
        if (!IsOpen)
            Close();
    }

    private void OnEnable()
    {
        InputManager.RegisterConfirmHandler(HandleConfirmRequested, priority: 300);
    }

    private void OnDisable()
    {
        InputManager.UnregisterConfirmHandler(HandleConfirmRequested);
    }

    public bool Open()
    {
        EnsureInitialized();
        if (_optionItems == null || _optionItems.Count == 0)
            return false;

        _selectedIndex = 0;
        IsOpen = true;
        if (_panelRoot != null && !_panelRoot.activeSelf)
            _panelRoot.SetActive(true);

        GridCursor.Instance?.SetExternalInputLocked(true);
        GridCursor.Instance?.SetVisualVisible(false);
        RefreshNavigatorItems();
        _navigator?.SetNavigationEnabled(true);
        _navigator?.SetSelection(_selectedIndex, notifySelectionChanged: false);
        RefreshSelectionVisual();
        return true;
    }

    public void Close()
    {
        EnsureInitialized();

        IsOpen = false;
        _selectedIndex = -1;
        if (_panelRoot != null && _panelRoot.activeSelf)
            _panelRoot.SetActive(false);

        GridCursor.Instance?.SetExternalInputLocked(false);
        GridCursor.Instance?.SetVisualVisible(true);
        _navigator?.SetNavigationEnabled(false);
        _navigator?.SetSelection(-1, notifySelectionChanged: false);
        RefreshSelectionVisual();
    }

    public bool Toggle()
    {
        if (IsOpen)
        {
            Close();
            return false;
        }

        return Open();
    }

    private bool HandleConfirmRequested(Vector2Int coord)
    {
        if (!IsOpen)
            return false;
        if (_selectedIndex < 0 || _selectedIndex >= _optionItems.Count)
            return true;

        switch (_selectedIndex)
        {
            case 0:
                Close();
                TurnManager.Instance?.PlayerClickEndTurn();
                break;
            case 1:
                Close();
                QuitGame();
                break;
        }

        return true;
    }

    private void RefreshSelectionVisual()
    {
        if (_optionItems == null)
            return;

        for (var i = 0; i < _optionItems.Count; i++)
        {
            if (_optionItems[i] != null)
                _optionItems[i].SetSelected(IsOpen && i == _selectedIndex);
        }

        _cursorFollower?.RefreshFromNavigator();
    }

    private void HandleSelectionChanged(int index)
    {
        if (_selectedIndex == index)
            return;

        _selectedIndex = index;
        RefreshSelectionVisual();
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        if (_panelRoot == null)
            _panelRoot = gameObject;

        if (_optionItems == null)
            _optionItems = new List<FieldMenuOptionItem>();

        // 未配置列表时尝试自动收集，方便快速挂载。
        if (_optionItems.Count == 0)
            AutoCollectOptionItems();
        if (_fieldMenuCursor == null)
            _fieldMenuCursor = transform.Find("FieldMenuCursor") as RectTransform;
        if (_navigator == null)
            _navigator = GetComponent<VerticalMenuNavigator>();
        if (_navigator == null)
            _navigator = gameObject.AddComponent<VerticalMenuNavigator>();
        if (_cursorFollower == null)
            _cursorFollower = GetComponent<MenuCursorFollower>();
        if (_cursorFollower == null)
            _cursorFollower = gameObject.AddComponent<MenuCursorFollower>();
        _cursorFollower.Setup(_fieldMenuCursor, _navigator);

        _navigator.Configure(
            inputPriority: 300,
            moveMode: VerticalMenuMoveMode.Clamp,
            skipDisabled: false,
            consumeWhenNavigationDisabled: false,
            consumeWhenNoSelectableItems: true);
        _navigator.SelectionChanged -= HandleSelectionChanged;
        _navigator.SelectionChanged += HandleSelectionChanged;
        _navigator.SetNavigationEnabled(IsOpen);

        _isInitialized = true;
    }

    private void RefreshNavigatorItems()
    {
        if (_navigator == null)
            return;

        var items = new List<RectTransform>();
        if (_optionItems != null)
        {
            for (var i = 0; i < _optionItems.Count; i++)
            {
                if (_optionItems[i] == null)
                    continue;
                var rect = _optionItems[i].RectTransform != null
                    ? _optionItems[i].RectTransform
                    : _optionItems[i].transform as RectTransform;
                if (rect != null)
                    items.Add(rect);
            }
        }

        _navigator.SetItems(items, _selectedIndex, notifySelectionChanged: false);
    }

    private void AutoCollectOptionItems()
    {
        var collected = new List<FieldMenuOptionItem>();
        var transforms = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null || t == transform)
                continue;

            var lower = t.name.ToLowerInvariant();
            if (!lower.Contains("optionitem"))
                continue;

            var item = t.GetComponent<FieldMenuOptionItem>();
            if (item == null)
                item = t.gameObject.AddComponent<FieldMenuOptionItem>();

            collected.Add(item);
        }

        if (collected.Count == 0)
            return;

        var endTurn = FindItemByNameContains(collected, "endturn");
        var quitGame = FindItemByNameContains(collected, "quitgame");
        if (endTurn != null)
            _optionItems.Add(endTurn);
        if (quitGame != null)
            _optionItems.Add(quitGame);

        for (var i = 0; i < collected.Count; i++)
        {
            var item = collected[i];
            if (item == null || _optionItems.Contains(item))
                continue;
            _optionItems.Add(item);
        }
    }

    private static FieldMenuOptionItem FindItemByNameContains(List<FieldMenuOptionItem> items, string keyword)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
                continue;
            if (item.name.ToLowerInvariant().Contains(keyword))
                return item;
        }

        return null;
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
