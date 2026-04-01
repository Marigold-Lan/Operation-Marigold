using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum VerticalMenuMoveMode
{
    Clamp = 0,
    Wrap = 1
}

[DisallowMultipleComponent]
public sealed class VerticalMenuNavigator : MonoBehaviour
{
    [Serializable]
    public sealed class SelectionChangedEvent : UnityEvent<int> { }

    [Header("Input")]
    [SerializeField] private int _inputPriority = 0;
    [SerializeField] private VerticalMenuMoveMode _moveMode = VerticalMenuMoveMode.Clamp;
    [SerializeField] private bool _skipDisabled = false;

    [Header("Behavior")]
    [SerializeField] private bool _navigationEnabled = false;
    [SerializeField] private bool _consumeWhenNavigationDisabled = false;
    [SerializeField] private bool _consumeWhenNoSelectableItems = false;

    [Header("Items")]
    [SerializeField] private List<RectTransform> _items = new List<RectTransform>();
    [SerializeField] private int _virtualItemCount = 0;
    [SerializeField] private int _selectedIndex = -1;
    [SerializeField] private SelectionChangedEvent _onSelectionChanged = new SelectionChangedEvent();

    private Func<int, bool> _customSelectableEvaluator;
    private Func<int, RectTransform> _virtualRectResolver;
    private bool _isRegistered;

    public event Action<int> SelectionChanged;

    public IReadOnlyList<RectTransform> Items => _items;
    public int ItemCount => IsVirtualMode ? _virtualItemCount : _items.Count;
    public int SelectedIndex => _selectedIndex;
    public int InputPriority => _inputPriority;
    public VerticalMenuMoveMode MoveMode => _moveMode;
    public bool SkipDisabled => _skipDisabled;
    public bool NavigationEnabled => _navigationEnabled;
    public bool ConsumeWhenNavigationDisabled => _consumeWhenNavigationDisabled;
    public bool ConsumeWhenNoSelectableItems => _consumeWhenNoSelectableItems;
    public bool IsVirtualMode => _virtualRectResolver != null;

    private void OnEnable()
    {
        RegisterInput();
    }

    private void OnDisable()
    {
        UnregisterInput();
    }

    public void Configure(
        int inputPriority,
        VerticalMenuMoveMode moveMode,
        bool skipDisabled,
        bool consumeWhenNavigationDisabled,
        bool consumeWhenNoSelectableItems)
    {
        _inputPriority = inputPriority;
        _moveMode = moveMode;
        _skipDisabled = skipDisabled;
        _consumeWhenNavigationDisabled = consumeWhenNavigationDisabled;
        _consumeWhenNoSelectableItems = consumeWhenNoSelectableItems;

        if (isActiveAndEnabled)
            ReRegisterInput();
    }

    public void SetSelectableEvaluator(Func<int, bool> evaluator)
    {
        _customSelectableEvaluator = evaluator;
    }

    public void SetNavigationEnabled(bool enabled)
    {
        _navigationEnabled = enabled;
    }

    public void SetItems(IList<RectTransform> items, int initialIndex, bool notifySelectionChanged)
    {
        _virtualRectResolver = null;
        _virtualItemCount = 0;
        _items.Clear();
        if (items != null)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    _items.Add(items[i]);
            }
        }

        SetSelection(initialIndex, notifySelectionChanged);
    }

    public void SetVirtualItems(int itemCount, Func<int, RectTransform> rectResolver, int initialIndex, bool notifySelectionChanged)
    {
        _items.Clear();
        _virtualItemCount = Mathf.Max(0, itemCount);
        _virtualRectResolver = rectResolver;
        SetSelection(initialIndex, notifySelectionChanged);
    }

    public bool SetSelection(int index, bool notifySelectionChanged = true)
    {
        var next = ResolveSelectionIndex(index);
        if (next < 0)
        {
            var changedToNone = _selectedIndex != -1;
            _selectedIndex = -1;
            if (changedToNone && notifySelectionChanged)
                RaiseSelectionChanged(_selectedIndex);
            return changedToNone;
        }

        if (_selectedIndex == next)
            return false;

        _selectedIndex = next;
        if (notifySelectionChanged)
            RaiseSelectionChanged(_selectedIndex);
        return true;
    }

    public bool TryMoveByDy(int dy)
    {
        if (!_navigationEnabled)
            return _consumeWhenNavigationDisabled;
        if (dy == 0)
            return _consumeWhenNavigationDisabled;

        var direction = dy > 0 ? -1 : 1;
        return TryMoveStep(direction);
    }

    private bool HandleMoveInput(int dx, int dy)
    {
        _ = dx;
        return TryMoveByDy(dy);
    }

    private bool TryMoveStep(int step)
    {
        if (ItemCount == 0)
            return _consumeWhenNoSelectableItems;

        if (!HasSelectableItems())
            return _consumeWhenNoSelectableItems;

        if (_selectedIndex < 0 || _selectedIndex >= ItemCount || !IsSelectable(_selectedIndex))
        {
            var fallback = FindFirstSelectableIndex();
            if (fallback < 0)
                return _consumeWhenNoSelectableItems;

            _selectedIndex = fallback;
            RaiseSelectionChanged(_selectedIndex);
            InputManager.NotifyCursorMoved();
            return true;
        }

        var candidate = _selectedIndex;
        var maxLoop = Mathf.Max(1, ItemCount);
        for (var i = 0; i < maxLoop; i++)
        {
            candidate = NextIndex(candidate, step);
            if (candidate < 0)
                break;
            if (candidate == _selectedIndex)
                continue;
            if (!IsSelectable(candidate))
                continue;

            _selectedIndex = candidate;
            RaiseSelectionChanged(_selectedIndex);
            InputManager.NotifyCursorMoved();
            return true;
        }

        return true;
    }

    private int NextIndex(int current, int step)
    {
        if (ItemCount == 0)
            return -1;

        if (_moveMode == VerticalMenuMoveMode.Wrap)
        {
            var next = current + step;
            if (next < 0) next = ItemCount - 1;
            if (next >= ItemCount) next = 0;
            return next;
        }

        return Mathf.Clamp(current + step, 0, ItemCount - 1);
    }

    private int ResolveSelectionIndex(int preferred)
    {
        if (ItemCount == 0)
            return -1;

        if (_skipDisabled)
        {
            if (preferred >= 0 && preferred < ItemCount && IsSelectable(preferred))
                return preferred;

            return FindFirstSelectableIndex();
        }

        return Mathf.Clamp(preferred, 0, ItemCount - 1);
    }

    private int FindFirstSelectableIndex()
    {
        if (ItemCount <= 0)
            return -1;

        for (var i = 0; i < ItemCount; i++)
        {
            if (IsSelectable(i))
                return i;
        }

        return -1;
    }

    private bool HasSelectableItems()
    {
        if (ItemCount <= 0)
            return false;

        for (var i = 0; i < ItemCount; i++)
        {
            if (IsSelectable(i))
                return true;
        }

        return false;
    }

    private bool IsSelectable(int index)
    {
        if (index < 0 || index >= ItemCount)
            return false;

        var item = GetRectForIndex(index);
        if (item == null)
        {
            if (!_skipDisabled && _customSelectableEvaluator == null)
                return true;
            if (_customSelectableEvaluator != null)
                return _customSelectableEvaluator.Invoke(index);
            return false;
        }

        if (_customSelectableEvaluator != null)
            return _customSelectableEvaluator.Invoke(index);

        if (!_skipDisabled)
            return true;

        return item.gameObject.activeInHierarchy;
    }

    public RectTransform GetRectForIndex(int index)
    {
        if (index < 0 || index >= ItemCount)
            return null;

        if (IsVirtualMode)
            return _virtualRectResolver != null ? _virtualRectResolver.Invoke(index) : null;

        if (_items == null || index >= _items.Count)
            return null;
        return _items[index];
    }

    private void RaiseSelectionChanged(int index)
    {
        SelectionChanged?.Invoke(index);
        _onSelectionChanged?.Invoke(index);
    }

    private void RegisterInput()
    {
        if (_isRegistered)
            return;

        InputManager.RegisterMoveHandler(HandleMoveInput, _inputPriority);
        _isRegistered = true;
    }

    private void UnregisterInput()
    {
        if (!_isRegistered)
            return;

        InputManager.UnregisterMoveHandler(HandleMoveInput);
        _isRegistered = false;
    }

    private void ReRegisterInput()
    {
        UnregisterInput();
        RegisterInput();
    }
}
