using System;
using UnityEngine;

/// <summary>
/// 将 UI 光标的 Y 对齐到当前选中项中心（worldCenter -> parent 局部坐标 -> 只写 y）。
/// 可绑定 VerticalMenuNavigator，在 SelectionChanged 时自动刷新；也可手动 SetTarget。
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuCursorFollower : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private RectTransform _cursor;
    [SerializeField] private VerticalMenuNavigator _navigator;

    [Header("定位")]
    [Tooltip("为 true 时用 anchoredPosition.y，否则用 localPosition.y。")]
    [SerializeField] private bool _useAnchoredPositionY = false;

    private bool _subscribed;

    /// <summary>
    /// 运行时绑定光标与导航器（例如面板初始化时调用）。
    /// useAnchoredPositionY：为 true 时用 anchoredPosition.y 定位（如 Factory 面板）。
    /// </summary>
    public void Setup(RectTransform cursor, VerticalMenuNavigator navigator, bool useAnchoredPositionY = false)
    {
        _cursor = cursor;
        _navigator = navigator;
        _useAnchoredPositionY = useAnchoredPositionY;
        UnsubscribeNavigator();
        if (isActiveAndEnabled)
        {
            SubscribeNavigator();
            RefreshFromNavigator();
        }
    }

    private void OnEnable()
    {
        SubscribeNavigator();
        RefreshFromNavigator();
    }

    private void OnDisable()
    {
        UnsubscribeNavigator();
    }

    private void SubscribeNavigator()
    {
        if (_navigator == null || _subscribed)
            return;

        _navigator.SelectionChanged += OnNavigatorSelectionChanged;
        _subscribed = true;
    }

    private void UnsubscribeNavigator()
    {
        if (_navigator == null || !_subscribed)
            return;

        _navigator.SelectionChanged -= OnNavigatorSelectionChanged;
        _subscribed = false;
    }

    private void OnNavigatorSelectionChanged(int index)
    {
        RefreshFromNavigator();
    }

    /// <summary>
    /// 根据当前 Navigator 的选中索引刷新光标位置与显隐。无 Navigator 时仅隐藏。
    /// </summary>
    public void RefreshFromNavigator()
    {
        if (_cursor == null)
            return;

        if (_navigator == null)
        {
            SetCursorVisible(false);
            return;
        }

        var idx = _navigator.SelectedIndex;
        if (idx < 0 || idx >= _navigator.ItemCount)
        {
            SetCursorVisible(false);
            return;
        }

        var target = _navigator.GetRectForIndex(idx);
        if (target == null)
        {
            SetCursorVisible(false);
            return;
        }

        SetCursorVisible(true);
        PositionCursorToTarget(_cursor, target, _useAnchoredPositionY);
    }

    /// <summary>
    /// 手动指定当前对齐目标（不依赖 Navigator）。调用后光标显示并对齐到 target。
    /// </summary>
    public void SetTarget(RectTransform target)
    {
        if (_cursor == null)
            return;

        if (target == null)
        {
            SetCursorVisible(false);
            return;
        }

        SetCursorVisible(true);
        PositionCursorToTarget(_cursor, target, _useAnchoredPositionY);
    }

    /// <summary>
    /// 仅刷新显隐：无有效选中时隐藏光标。
    /// </summary>
    public void SetCursorVisible(bool visible)
    {
        if (_cursor == null)
            return;
        if (_cursor.gameObject.activeSelf != visible)
            _cursor.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 静态方法：将光标的 Y 对齐到目标 Rect 中心（在光标父节点局部空间）。
    /// </summary>
    public static void PositionCursorToTarget(RectTransform cursor, RectTransform target, bool useAnchoredPositionY = false)
    {
        if (cursor == null || target == null)
            return;

        var cursorParent = cursor.parent as RectTransform;
        if (cursorParent == null)
            return;

        Canvas.ForceUpdateCanvases();
        var worldCenter = target.TransformPoint(target.rect.center);
        var localPoint = cursorParent.InverseTransformPoint(worldCenter);

        if (useAnchoredPositionY)
        {
            var anchored = cursor.anchoredPosition;
            anchored.y = localPoint.y;
            cursor.anchoredPosition = anchored;
        }
        else
        {
            var local = cursor.localPosition;
            local.y = localPoint.y;
            cursor.localPosition = local;
        }
    }

    private void OnValidate()
    {
        if (_cursor == null && transform.parent != null)
            _cursor = transform.Find("Cursor") as RectTransform;
    }
}
