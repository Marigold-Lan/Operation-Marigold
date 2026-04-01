using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FactoryPanelManager : Singleton<FactoryPanelManager>
{
    [Header("根节点")]
    [SerializeField] private GameObject _panelRoot;

    [Header("列表结构")]
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private RectTransform _content;
    [SerializeField] private UnitOptionItem _unitOptionItemPrefab;

    [Header("光标与箭头")]
    [SerializeField] private RectTransform _factoryCursor;
    [SerializeField] private GameObject _topArrow;
    [SerializeField] private GameObject _bottomArrow;
    [SerializeField] private VerticalMenuNavigator _navigator;
    [SerializeField] private MenuCursorFollower _cursorFollower;

    [Header("依赖")]
    [SerializeField] private FundsPanelManager _fundsPanelManager;
    [SerializeField] private UnitDetailPanelController _unitDetailPanel;

    [Header("兼容字段（旧：混合列表）")]
    [Tooltip("旧字段：把所有可能出现在工厂菜单的 UnitData 拖进来。若未配置按阵营列表，将回退使用此列表。")]
    [SerializeField] private List<UnitData> _unitCatalog = new List<UnitData>();

    private readonly List<UnitData> _spawnableUnits = new List<UnitData>();
    private readonly List<UnitOptionItem> _rowItems = new List<UnitOptionItem>();

    private int _selectedIndex;
    private FactorySpawner _activeSpawner;
    private Cell _activeSpawnCell;
    private UnitFaction _activeFaction = UnitFaction.None;
    private bool _isInitialized;
    private bool _detailPanelDisabledDueToError;
    private int _lastCloseFrame = -1;
    private float _itemHeight;
    private float _itemSpacing;
    private float _itemStep;
    private float _paddingTop;
    private float _paddingBottom;
    private float _maxScrollYCached = float.NaN;

    public bool IsOpen { get; private set; }
    public UnitFaction ActiveFaction => _activeFaction;
    public static bool IsAnyOpen => Instance != null && Instance.IsOpen;
    public static bool IsBlockingGameplayConfirm =>
        Instance != null && (Instance.IsOpen || Instance._lastCloseFrame == Time.frameCount);

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
        InputManager.RegisterConfirmHandler(HandleConfirmRequested, priority: 250);
        InputManager.RegisterCancelHandler(HandleCancelRequested, priority: 250);
    }

    private void OnDisable()
    {
        InputManager.UnregisterConfirmHandler(HandleConfirmRequested);
        InputManager.UnregisterCancelHandler(HandleCancelRequested);
    }

    public bool Open(FactorySpawner spawner, UnitFaction faction)
    {
        EnsureInitialized();

        if (spawner == null || _panelRoot == null || _content == null || _viewport == null || _unitOptionItemPrefab == null)
            return false;

        _activeSpawner = spawner;
        _activeFaction = faction;
        _activeSpawnCell = ResolveSpawnCell(spawner);
        _lastCloseFrame = -1;

        BuildSpawnableList(spawner);
        QuickSortUnitsByCost(_spawnableUnits);
        BuildOptionRows();

        _selectedIndex = _spawnableUnits.Count > 0 ? 0 : -1;
        _panelRoot.SetActive(true);
        IsOpen = true;

        GridCursor.Instance?.SetExternalInputLocked(true);
        GridCursor.Instance?.SetVisualVisible(false);

        SnapContentToTop();
        RefreshNavigatorItems();
        _navigator?.SetNavigationEnabled(true);
        _navigator?.SetSelection(_selectedIndex, notifySelectionChanged: false);
        EnsureSelectedVisible();
        RefreshSelectionVisual();
        RefreshArrowVisibility();
        RefreshDetailPanelSafe();

        _activeSpawner.ShowSpawnMenu();
        return true;
    }

    public void Close()
    {
        EnsureInitialized();

        if (IsOpen)
            _lastCloseFrame = Time.frameCount;

        IsOpen = false;
        _activeSpawner = null;
        _activeSpawnCell = null;
        _activeFaction = UnitFaction.None;
        _spawnableUnits.Clear();
        _selectedIndex = -1;

        ClearOptionRows();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        HideDetailPanelSafe();
        GridCursor.Instance?.SetExternalInputLocked(false);
        GridCursor.Instance?.SetVisualVisible(true);
        _navigator?.SetNavigationEnabled(false);
        _navigator?.SetSelection(-1, notifySelectionChanged: false);
    }

    private bool HandleCancelRequested()
    {
        if (!IsOpen)
            return false;
        Close();
        return true;
    }

    private bool HandleConfirmRequested(Vector2Int coord)
    {
        if (!IsOpen)
            return false;
        if (_activeSpawner == null)
            return true;
        if (_selectedIndex < 0 || _selectedIndex >= _spawnableUnits.Count)
            return true;

        var selectedUnit = _spawnableUnits[_selectedIndex];
        if (selectedUnit == null)
            return true;

        var spawnCell = _activeSpawnCell != null ? _activeSpawnCell : ResolveSpawnCell(_activeSpawner);
        if (spawnCell == null)
            return true;

        var context = new CommandContext
        {
            Mode = CommandContext.ExecutionMode.AIImmediate,
            FactorySpawner = _activeSpawner,
            ProduceUnitData = selectedUnit,
            SpawnCell = spawnCell
        };

        var spawned = CommandExecutor.Execute(new ProduceCommand(), context);
        if (!spawned)
            return true;

        Close();
        return true;
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        if (_panelRoot == null)
            _panelRoot = gameObject;
        if (_fundsPanelManager == null)
            _fundsPanelManager = FundsPanelManager.Instance;
        if (_unitDetailPanel == null)
            _unitDetailPanel = GetComponentInChildren<UnitDetailPanelController>(true);
        if (_unitDetailPanel == null)
            _unitDetailPanel = TryFindDetailPanelInScene();
        if (_navigator == null)
            _navigator = GetComponent<VerticalMenuNavigator>();
        if (_navigator == null)
            _navigator = gameObject.AddComponent<VerticalMenuNavigator>();

        _navigator.Configure(
            inputPriority: 250,
            moveMode: VerticalMenuMoveMode.Clamp,
            skipDisabled: false,
            consumeWhenNavigationDisabled: false,
            consumeWhenNoSelectableItems: true);
        _navigator.SelectionChanged -= HandleSelectionChanged;
        _navigator.SelectionChanged += HandleSelectionChanged;
        _navigator.SetNavigationEnabled(IsOpen);
        if (_cursorFollower == null)
            _cursorFollower = GetComponent<MenuCursorFollower>();
        if (_cursorFollower == null)
            _cursorFollower = gameObject.AddComponent<MenuCursorFollower>();
        _cursorFollower.Setup(_factoryCursor, _navigator, useAnchoredPositionY: true);

        _detailPanelDisabledDueToError = false;

        _isInitialized = true;
    }

    private void BuildSpawnableList(FactorySpawner spawner)
    {
        _spawnableUnits.Clear();
        if (spawner == null)
            return;

        var building = spawner.GetComponent<BuildingController>();
        var catalog = building != null && building.Data != null ? building.Data.factoryBuildCatalog : null;
        if (catalog != null)
        {
            _spawnableUnits.AddRange(catalog.GetBuildableUnits(_activeFaction));
            return;
        }

        // 回退：旧混合列表（保持旧场景/Prefab 不配置 SO 时仍可用）
        var added = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _unitCatalog.Count; i++)
        {
            var data = _unitCatalog[i];
            if (data == null)
                continue;
            if (string.IsNullOrWhiteSpace(data.id))
                continue;
            if (!added.Add(data.id))
                continue;

            _spawnableUnits.Add(data);
        }
    }

    private static void QuickSortUnitsByCost(List<UnitData> units)
    {
        if (units == null || units.Count <= 1)
            return;

        QuickSortUnitsByCost(units, 0, units.Count - 1);
    }

    private static void QuickSortUnitsByCost(List<UnitData> units, int left, int right)
    {
        if (left >= right)
            return;

        var i = left;
        var j = right;
        var pivot = units[left + ((right - left) / 2)];

        while (i <= j)
        {
            while (CompareUnitForCostSort(units[i], pivot) < 0) i++;
            while (CompareUnitForCostSort(units[j], pivot) > 0) j--;

            if (i <= j)
            {
                if (i != j)
                {
                    var tmp = units[i];
                    units[i] = units[j];
                    units[j] = tmp;
                }
                i++;
                j--;
            }
        }

        if (left < j) QuickSortUnitsByCost(units, left, j);
        if (i < right) QuickSortUnitsByCost(units, i, right);
    }

    private static int CompareUnitForCostSort(UnitData a, UnitData b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;  // null 放后面
        if (b == null) return -1;

        var costCompare = a.cost.CompareTo(b.cost);
        if (costCompare != 0) return costCompare;

        // 同价时做二级排序，避免快速排序导致 UI 顺序“抖动”
        var nameA = a.displayName ?? string.Empty;
        var nameB = b.displayName ?? string.Empty;
        var nameCompare = System.StringComparer.OrdinalIgnoreCase.Compare(nameA, nameB);
        if (nameCompare != 0) return nameCompare;

        var idA = a.id ?? string.Empty;
        var idB = b.id ?? string.Empty;
        return System.StringComparer.OrdinalIgnoreCase.Compare(idA, idB);
    }

    private void ReadLayoutMetricsFromVerticalGroup()
    {
        _itemSpacing = 0f;
        _paddingTop = 0f;
        _paddingBottom = 0f;
        var onContent = _content.GetComponent<VerticalLayoutGroup>();
        var v = onContent;
        if (v == null && _viewport != null)
            v = _viewport.GetComponent<VerticalLayoutGroup>();
        if (v != null)
        {
            _itemSpacing = Mathf.Max(0f, v.spacing);
            _paddingTop = Mathf.Max(0f, v.padding.top);
            _paddingBottom = Mathf.Max(0f, v.padding.bottom);
        }
    }

    private void BuildOptionRows()
    {
        ClearOptionRows();
        if (_unitOptionItemPrefab == null || _content == null)
            return;

        ReadLayoutMetricsFromVerticalGroup();

        for (var i = 0; i < _spawnableUnits.Count; i++)
        {
            var unitData = _spawnableUnits[i];
            var item = Instantiate(_unitOptionItemPrefab, _content);
            item.Bind(unitData, GetMovementIcon(unitData));
            _rowItems.Add(item);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

        if (_rowItems.Count > 0)
        {
            var r0 = _rowItems[0].RectTransform;
            _itemHeight = r0 != null ? Mathf.Max(1f, r0.rect.height) : 1f;
        }
        else
        {
            var prefabRect = _unitOptionItemPrefab.RectTransform != null
                ? _unitOptionItemPrefab.RectTransform
                : _unitOptionItemPrefab.transform as RectTransform;
            _itemHeight = prefabRect != null ? Mathf.Max(1f, prefabRect.rect.height) : 1f;
        }

        _itemStep = _itemHeight + _itemSpacing;
        if (_itemStep <= 0f)
            _itemStep = 1f;

        InvalidateScrollBounds();
    }

    private void ClearOptionRows()
    {
        InvalidateScrollBounds();

        for (var i = 0; i < _rowItems.Count; i++)
        {
            if (_rowItems[i] != null)
                Destroy(_rowItems[i].gameObject);
        }

        _rowItems.Clear();
    }

    private void RefreshSelectionVisual()
    {
        for (var i = 0; i < _rowItems.Count; i++)
        {
            if (_rowItems[i] != null)
                _rowItems[i].SetSelected(i == _selectedIndex);
        }

        _cursorFollower?.RefreshFromNavigator();
    }

    private void HandleSelectionChanged(int index)
    {
        if (_selectedIndex == index)
            return;

        _selectedIndex = index;
        EnsureSelectedVisible();
        RefreshSelectionVisual();
        RefreshArrowVisibility();
        RefreshDetailPanelSafe();
    }

    private void RefreshNavigatorItems()
    {
        if (_navigator == null)
            return;

        var rects = new List<RectTransform>(_rowItems.Count);
        for (var i = 0; i < _rowItems.Count; i++)
        {
            var rt = _rowItems[i] != null ? _rowItems[i].RectTransform : null;
            if (rt != null)
                rects.Add(rt);
        }

        _navigator.SetItems(rects, _selectedIndex, notifySelectionChanged: false);
    }

    private void EnsureSelectedVisible()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _rowItems.Count || _content == null || _viewport == null)
            return;

        var row = _rowItems[_selectedIndex];
        if (row == null)
            return;

        var itemRt = row.RectTransform;
        if (itemRt == null)
            return;

        const int maxIterations = 4;
        for (var iter = 0; iter < maxIterations; iter++)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_viewport, itemRt);
            var vp = _viewport.rect;

            var dy = 0f;
            if (bounds.size.y > vp.height - 0.01f)
                dy = vp.yMax - bounds.max.y;
            else if (bounds.min.y < vp.yMin)
                dy = vp.yMin - bounds.min.y;
            else if (bounds.max.y > vp.yMax)
                dy = vp.yMax - bounds.max.y;
            else
                break;

            if (Mathf.Abs(dy) < 0.5f)
                break;

            var anchored = _content.anchoredPosition;
            anchored.y += dy;
            anchored.y = Mathf.Clamp(anchored.y, 0f, GetMaxScrollY());
            _content.anchoredPosition = anchored;
        }
    }

    private void SnapContentToTop()
    {
        if (_content == null)
            return;
        var anchored = _content.anchoredPosition;
        anchored.y = 0f;
        _content.anchoredPosition = anchored;
    }

    private void RefreshArrowVisibility()
    {
        if (_content == null)
            return;

        var maxScroll = GetMaxScrollY();
        var y = _content.anchoredPosition.y;
        var atTop = y <= 0.01f;
        var atBottom = y >= maxScroll - 0.01f;

        if (_topArrow != null)
            _topArrow.SetActive(!atTop);
        if (_bottomArrow != null)
            _bottomArrow.SetActive(!atBottom);
    }

    private void InvalidateScrollBounds()
    {
        _maxScrollYCached = float.NaN;
    }

    private float GetMaxScrollY()
    {
        if (_content == null || _viewport == null)
            return 0f;

        if (!float.IsNaN(_maxScrollYCached))
            return _maxScrollYCached;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

        var viewportHeight = Mathf.Max(1f, _viewport.rect.height);
        var contentHeight = LayoutUtility.GetPreferredHeight(_content);
        if (contentHeight < 1f)
            contentHeight = _content.rect.height;
        if (contentHeight < 1f && _rowItems.Count > 0)
            contentHeight = EstimateContentHeightFromRows();

        _maxScrollYCached = Mathf.Max(0f, contentHeight - viewportHeight);
        return _maxScrollYCached;
    }

    private float EstimateContentHeightFromRows()
    {
        var sum = _paddingTop + _paddingBottom;
        for (var i = 0; i < _rowItems.Count; i++)
        {
            var rt = _rowItems[i] != null ? _rowItems[i].RectTransform : null;
            sum += rt != null ? Mathf.Max(1f, rt.rect.height) : _itemHeight;
            if (i < _rowItems.Count - 1)
                sum += _itemSpacing;
        }

        return sum;
    }

    private void LateUpdate()
    {
        if (!IsOpen)
            return;

        RefreshArrowVisibility();
        RefreshSelectionVisual();
    }

    private Sprite GetMovementIcon(UnitData unitData)
    {
        if (unitData == null)
            return null;

        return GlobalConfigManager.GetMovementIcon(unitData.movementType);
    }

    private void RefreshDetailPanelSafe()
    {
        if (_unitDetailPanel == null || _detailPanelDisabledDueToError)
            return;

        try
        {
            if (_selectedIndex < 0 || _selectedIndex >= _spawnableUnits.Count)
            {
                _unitDetailPanel.Hide();
                return;
            }

            _unitDetailPanel.Show(_spawnableUnits[_selectedIndex]);
        }
        catch (System.Exception)
        {
            _detailPanelDisabledDueToError = true;
        }
    }

    private void HideDetailPanelSafe()
    {
        if (_unitDetailPanel == null || _detailPanelDisabledDueToError)
            return;

        try
        {
            _unitDetailPanel.Hide();
        }
        catch (System.Exception)
        {
            _detailPanelDisabledDueToError = true;
        }
    }

    private static UnitDetailPanelController TryFindDetailPanelInScene()
    {
#if UNITY_2023_1_OR_NEWER
        var panels = FindObjectsByType<UnitDetailPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var panels = FindObjectsOfType<UnitDetailPanelController>(true);
#endif
        for (var i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            if (panel == null)
                continue;
            if (!panel.gameObject.scene.IsValid())
                continue;
            return panel;
        }

        return null;
    }

    private static Cell ResolveSpawnCell(FactorySpawner spawner)
    {
        if (spawner == null)
            return null;

        var building = spawner.GetComponent<BuildingController>();
        if (building != null && building.Cell != null)
            return building.Cell;

        return spawner.GetComponentInParent<Cell>();
    }
}
