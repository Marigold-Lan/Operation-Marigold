using System;
using System.Collections.Generic;
using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace OperationMarigold.Logging.UI
{
    /// <summary>
    /// 仅负责显示：订阅 LogHub、对象池复用 LogItem、自动滚动控制。
    /// </summary>
    public sealed class LogPanelController : MonoBehaviour
    {
        [Header("Visibility (match GridInfoPanel)")]
        [Tooltip("LogPanel 根节点（通常就是你场景里的 LogPanel 物体）。可为 inactive；运行时会自动激活并由 CanvasGroup 控制显隐。")]
        [SerializeField] private GameObject panelRoot;
        private CanvasGroup _panelCanvasGroup;
        private bool _isTurnIntroAnimating;

        [Header("References")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private RectTransform _viewport;
        [SerializeField] private LogItemView _itemPrefab;
        [Tooltip("对象池容器（用于存放未激活的 LogItem，避免堆在 Content 下）。留空则自动创建。")]
        [SerializeField] private RectTransform _poolRoot;

        [Header("Rendering")]
        [Tooltip("强制最大渲染条数；为 0 则根据 viewport 高度自动估算。")]
        [SerializeField] private int _maxRenderedItemsOverride = 0;
        [SerializeField] private int _extraBufferItems = 16;
        [SerializeField] private int _prewarmItems = 32;

        [Header("Channel Icons (by LogChannel enum order)")]
        [SerializeField] private Sprite[] _channelIcons;

        [Header("Text")]
        [Tooltip("用于显示日志的 TMP 字体覆盖（建议使用包含中文的 TMP Font Asset）。留空则使用 prefab 自带字体。")]
        [SerializeField] private TMP_FontAsset _messageFontOverride;

        // 虚拟化：只保留当前可见窗口的 items，滚动时复用。
        private readonly List<LogItemView> _visibleItems = new List<LogItemView>();
        private readonly Stack<LogItemView> _pool = new Stack<LogItemView>();

        private bool _autoScroll = true;
        private int _maxRenderedItems;
        private bool _isVisible;
        private float _itemHeight;
        private int _windowStartIndex;
        private int _lastAppliedTotalCount = -1;
        private int _lastAppliedStartIndex = -1;
        private int _lastAppliedCount = -1;
        private bool _pendingScrollRefresh;
        private readonly Dictionary<long, float> _heightBySequence = new Dictionary<long, float>();
        private LayoutElement _topSpacer;
        private LayoutElement _bottomSpacer;
        private VerticalLayoutGroup _layoutGroup;
        private float _layoutSpacing;
        private int _layoutPaddingTop;

        private void Awake()
        {
            if (panelRoot == null)
                panelRoot = _scrollRect != null ? _scrollRect.gameObject : null;

            // 即便场景里 LogPanel 是 inactive，也要让它在运行时可被显示（显隐交给 CanvasGroup）
            if (panelRoot != null && !panelRoot.activeSelf)
                panelRoot.SetActive(true);

            // 允许该控制器挂在任意物体上：优先使用序列化引用；若为空且自己在面板层级内，则尝试向下查找。
            if (_scrollRect == null)
                _scrollRect = GetComponentInChildren<ScrollRect>(includeInactive: true);

            // 若挂在独立物体上，GetComponentInChildren 很可能找不到；尝试在场景中挑一个最像日志面板的 ScrollRect。
            if (_scrollRect == null)
                _scrollRect = TryFindLogScrollRectInScene();

            if (_scrollRect != null)
            {
                if (_viewport == null)
                    _viewport = _scrollRect.viewport;
                if (_content == null)
                    _content = _scrollRect.content;
            }

            if (_content != null)
            {
                _layoutGroup = _content.GetComponent<VerticalLayoutGroup>();
                _layoutSpacing = _layoutGroup != null ? _layoutGroup.spacing : 0f;
                _layoutPaddingTop = _layoutGroup != null ? _layoutGroup.padding.top : 0;
            }

            EnsurePoolRoot();

            if (panelRoot == null && _scrollRect != null)
                panelRoot = _scrollRect.gameObject;
            EnsureCanvasGroup();
        }

        private void OnEnable()
        {
            LogHub.OnPublished += HandleLogPublished;
            if (_scrollRect != null)
                _scrollRect.onValueChanged.AddListener(HandleScrollValueChanged);

            GridCursor.OnCursorCoordChanged += HandleCursorCoordChanged;
            TurnManager.OnTurnIntroAnimationStarted += HandleTurnIntroAnimationStarted;
            TurnManager.OnTurnIntroAnimationComplete += HandleTurnIntroAnimationComplete;

            GameStateFacade.OnGameOver += HandleGameOver;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            UnitTransport.OnLoaded += HandleTransportLoaded;
            UnitTransport.OnDropped += HandleTransportDropped;
            UnitSupply.OnSupplyPerformed += HandleSupplyPerformed;
        }

        private void OnDisable()
        {
            LogHub.OnPublished -= HandleLogPublished;
            if (_scrollRect != null)
                _scrollRect.onValueChanged.RemoveListener(HandleScrollValueChanged);

            GridCursor.OnCursorCoordChanged -= HandleCursorCoordChanged;
            TurnManager.OnTurnIntroAnimationStarted -= HandleTurnIntroAnimationStarted;
            TurnManager.OnTurnIntroAnimationComplete -= HandleTurnIntroAnimationComplete;

            GameStateFacade.OnGameOver -= HandleGameOver;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            UnitTransport.OnLoaded -= HandleTransportLoaded;
            UnitTransport.OnDropped -= HandleTransportDropped;
            UnitSupply.OnSupplyPerformed -= HandleSupplyPerformed;
        }

        private void Start()
        {
            if (!ValidateReferences())
                return;

            ComputeMaxRenderedItems();
            _itemHeight = Mathf.Max(1f, EstimateItemHeight());
            EnsureSpacers();
            Prewarm();

            // 初始填充：默认滚到最新
            RefreshWindow(forceAutoScroll: true);
        }

        private void Update()
        {
            RefreshVisibility();
            // 视口尺寸变化时重新估算渲染数量，并更新窗口
            var previousMax = _maxRenderedItems;
            ComputeMaxRenderedItems();
            if (_maxRenderedItems != previousMax)
                RefreshWindow(forceAutoScroll: _autoScroll);
        }

        private void LateUpdate()
        {
            // 拖动滚动条时 onValueChanged 会高频触发；把刷新合并到每帧最多一次。
            if (_pendingScrollRefresh && _isVisible)
            {
                _pendingScrollRefresh = false;
                RefreshWindow(forceAutoScroll: false);
            }
        }

        private void HandleCursorCoordChanged(Vector2Int _)
        {
            RefreshVisibility();
        }

        private void HandleTurnIntroAnimationStarted(TurnContext _)
        {
            _isTurnIntroAnimating = true;
        }

        private void HandleTurnIntroAnimationComplete(TurnContext _)
        {
            _isTurnIntroAnimating = false;
        }

        private void RefreshVisibility()
        {
            var facade = GameStateFacade.Instance;
            if (facade != null && facade.Session != null && facade.Session.IsGameOver)
            {
                SetPanelVisible(false);
                if (_isVisible)
                {
                    _isVisible = false;
                    ClearVisibleToPool();
                }
                return;
            }

            var hasCell = TryGetCurrentCell(out var cell);
            // Match GridInfoPanel's visibility logic
            var gridInfoShouldShow = !_isTurnIntroAnimating &&
                                     hasCell &&
                                     (!IsInRangePreviewState() || ShouldShowTerrainInfoDuringAttackRange(cell));

            // Match FundsPanel's visibility logic (FundsPanelManager.ShouldHideFundsPanel)
            var fundsShouldShow = !_isTurnIntroAnimating &&
                                  !CommandPanelController.IsAnyOpen &&
                                  !(HighlightManager.Instance != null && HighlightManager.Instance.HasRangeHighlights);

            // New rule: LogPanel shows only when BOTH FundsPanel and GridInfoPanel are visible.
            var shouldShow = gridInfoShouldShow && fundsShouldShow;

            SetPanelVisible(shouldShow);

            // 关键：隐藏时可以把 item 全部回收到池（减少 Content 负担），
            // 但再次显示时要从 LogHub 的数据源立刻补齐最新一段，做到“无限列表 + item 复用”。
            if (_isVisible != shouldShow)
            {
                _isVisible = shouldShow;
                if (!_isVisible)
                {
                    ClearVisibleToPool();
                }
                else
                {
                    RefreshWindow(forceAutoScroll: true);
                }
            }
        }

        private bool TryGetCurrentCell(out Cell cell)
        {
            var cursor = GridCursor.Instance;
            cell = cursor != null ? cursor.CurrentCell : null;
            return cell != null;
        }

        private bool IsInRangePreviewState()
        {
            if (CommandPanelController.IsAnyOpen)
                return true;
            if (FactoryPanelManager.IsAnyOpen)
                return true;

            var highlightManager = HighlightManager.Instance;
            return highlightManager != null && highlightManager.HasRangeHighlights;
        }

        private bool ShouldShowTerrainInfoDuringAttackRange(Cell cell)
        {
            if (cell == null)
                return false;

            var highlightManager = HighlightManager.Instance;
            if (highlightManager == null || !highlightManager.HasAttackRangeHighlights)
                return false;

            var selection = SelectionManager.Instance;
            if (selection == null || !selection.IsAttackTargeting)
                return false;

            var source = selection.AttackTargetingSourceUnit;
            if (source == null)
                return false;

            var unit = cell.UnitController;
            if (unit == null)
                return false;

            return unit.OwnerFaction != source.OwnerFaction;
        }

        private void EnsureCanvasGroup()
        {
            if (panelRoot == null)
                return;
            _panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null)
                _panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot == null || _panelCanvasGroup == null)
                return;

            _panelCanvasGroup.alpha = visible ? 1f : 0f;
            _panelCanvasGroup.blocksRaycasts = visible;
            _panelCanvasGroup.interactable = visible;
        }

        private void HandleGameOver(bool _, string __)
        {
            // Requirement: game over should hide LogPanel
            SetPanelVisible(false);
            _isVisible = false;
            ClearVisibleToPool();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Requirement: reloading scene should clear all logs
            LogHub.Clear();
            ClearVisibleToPool();
            _heightBySequence.Clear();
            _lastAppliedTotalCount = -1;
            _lastAppliedStartIndex = -1;
            _lastAppliedCount = -1;
            _autoScroll = true;
        }

        private void HandleTransportLoaded(UnitController transporter, UnitController cargo)
        {
            LogHub.Publish(
                LogChannel.Movement,
                $"Load: {Adapters.LogFormat.UnitName(cargo)} -> {Adapters.LogFormat.UnitName(transporter)}.");
        }

        private void HandleTransportDropped(UnitController transporter, UnitController cargo, Vector2Int coord)
        {
            LogHub.Publish(
                LogChannel.Movement,
                $"Drop: {Adapters.LogFormat.UnitName(transporter)} -> {Adapters.LogFormat.UnitName(cargo)} at {Adapters.LogFormat.Coord(coord)}.");
        }

        private void HandleSupplyPerformed(UnitController supplier, UnitController target, bool ok)
        {
            if (!ok)
                return;
            LogHub.Publish(
                LogChannel.Economy,
                $"Supply: {Adapters.LogFormat.UnitName(supplier)} -> {Adapters.LogFormat.UnitName(target)}.");
        }

        private bool ValidateReferences()
        {
            if (_scrollRect == null || _viewport == null || _content == null || _itemPrefab == null)
            {
                Debug.LogError(
                    $"[LogPanelController] 引用未绑定：ScrollRect={(_scrollRect != null)}, Viewport={(_viewport != null)}, Content={(_content != null)}, ItemPrefab={(_itemPrefab != null)}。请在 Inspector 里手动拖拽绑定。",
                    this);
                return false;
            }

            return true;
        }

        private static ScrollRect TryFindLogScrollRectInScene()
        {
#if UNITY_2023_1_OR_NEWER
            var all = FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var all = FindObjectsOfType<ScrollRect>();
#endif
            if (all == null || all.Length == 0)
                return null;

            // 优先名字里带 Log 的
            for (var i = 0; i < all.Length; i++)
            {
                var sr = all[i];
                if (sr == null) continue;
                var n = sr.gameObject.name;
                if (!string.IsNullOrEmpty(n) && n.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0)
                    return sr;
            }

            // 兜底：只有一个时就用它
            if (all.Length == 1)
                return all[0];

            return null;
        }

        private void EnsurePoolRoot()
        {
            if (_poolRoot != null)
                return;

            var go = new GameObject("LogItemPool");
            go.SetActive(true);
            go.transform.SetParent(transform, worldPositionStays: false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _poolRoot = rt;
        }

        private void ComputeMaxRenderedItems()
        {
            if (_maxRenderedItemsOverride > 0)
            {
                _maxRenderedItems = Mathf.Max(1, _maxRenderedItemsOverride);
                return;
            }

            var viewportHeight = _viewport != null ? _viewport.rect.height : 0f;
            var estimatedItemHeight = Mathf.Max(1f, _itemHeight > 1f ? _itemHeight : EstimateItemHeight());
            if (viewportHeight <= 1f || estimatedItemHeight <= 1f)
            {
                _maxRenderedItems = 80; // fallback
                return;
            }

            var visible = Mathf.CeilToInt(viewportHeight / estimatedItemHeight);
            _maxRenderedItems = Mathf.Clamp(visible + Mathf.Max(0, _extraBufferItems), 8, 300);
        }

        private float EstimateItemHeight()
        {
            if (_itemPrefab == null)
                return 0f;

            // 优先取 prefab 上的 LayoutElement.preferredHeight（稳定、不会污染 Content）
            var prefabRt = _itemPrefab.transform as RectTransform;
            if (prefabRt != null)
            {
                var le = _itemPrefab.GetComponent<LayoutElement>();
                if (le != null && le.preferredHeight > 1f)
                    return le.preferredHeight;
                if (prefabRt.rect.height > 1f)
                    return prefabRt.rect.height;
            }

            // 兜底：用一个温和的常量，避免估算为 0 导致 maxRenderedItems 过大或异常。
            return 36f;
        }

        private void Prewarm()
        {
            var n = Mathf.Max(0, _prewarmItems);
            for (var i = 0; i < n; i++)
            {
                var item = CreateItemInstance(parent: _poolRoot);
                if (item == null)
                    break;
                ReleaseItem(item);
            }
        }

        private void HandleLogPublished(LogEntry entry)
        {
            if (!_isVisible)
                return;

            // 有新日志：如果在底部就跟随最新；否则保持当前窗口（用户正在翻看历史）。
            RefreshWindow(forceAutoScroll: _autoScroll);
        }

        private void HandleScrollValueChanged(Vector2 _)
        {
            // ScrollRect: bottom 通常接近 0，top 接近 1
            if (IsAtBottom())
                _autoScroll = true;
            else
                _autoScroll = false;

            if (_isVisible)
                _pendingScrollRefresh = true;
        }

        private bool IsAtBottom()
        {
            if (_scrollRect == null)
                return true;
            return _scrollRect.verticalNormalizedPosition <= 0.001f;
        }

        private void ForceScrollToBottom()
        {
            if (_scrollRect == null)
                return;
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private void RefreshWindow(bool forceAutoScroll)
        {
            if (_content == null || _viewport == null)
                return;
            EnsureSpacers();

            var snapshot = LogHub.GetSnapshot();
            var total = snapshot.Count;
            if (total <= 0)
            {
                ClearVisibleToPool();
                SetSpacerHeights(0f, 0f);
                _lastAppliedTotalCount = 0;
                _lastAppliedStartIndex = 0;
                _lastAppliedCount = 0;
                return;
            }

            var desiredCount = Mathf.Clamp(_maxRenderedItems, 1, 300);
            var maxStart = Mathf.Max(0, total - desiredCount);
            var prefix = BuildHeightPrefix(snapshot);
            var startIndex = forceAutoScroll || _autoScroll ? maxStart : CalculateStartIndexFromScroll(prefix, total);
            startIndex = Mathf.Clamp(startIndex, 0, maxStart);

            var count = Mathf.Min(desiredCount, total - startIndex);

            // 避免无效刷新：拖动时很多 onValueChanged 其实落在同一“行”，不需要重复 Bind。
            if (!forceAutoScroll &&
                _lastAppliedTotalCount == total &&
                _lastAppliedStartIndex == startIndex &&
                _lastAppliedCount == count)
            {
                return;
            }

            ApplyWindow(snapshot, startIndex, count, total);
            _lastAppliedTotalCount = total;
            _lastAppliedStartIndex = startIndex;
            _lastAppliedCount = count;

            if (forceAutoScroll || _autoScroll)
                ForceScrollToBottom();
        }

        private float[] BuildHeightPrefix(List<LogEntry> snapshot)
        {
            var total = snapshot != null ? snapshot.Count : 0;
            var prefix = new float[total + 1];
            var spacing = Mathf.Max(0f, _layoutSpacing);
            for (var i = 0; i < total; i++)
            {
                prefix[i + 1] = prefix[i] + GetEstimatedEntryHeight(snapshot[i]) + spacing;
            }
            return prefix;
        }

        private float GetEstimatedEntryHeight(LogEntry entry)
        {
            if (_itemHeight <= 1f)
                _itemHeight = Mathf.Max(1f, EstimateItemHeight());

            if (entry.Sequence > 0 && _heightBySequence.TryGetValue(entry.Sequence, out var h) && h > 1f)
                return h;

            return _itemHeight;
        }

        private int CalculateStartIndexFromScroll(float[] prefix, int totalCount)
        {
            if (_content == null || prefix == null || prefix.Length != totalCount + 1)
                return 0;

            // anchoredPosition.y：从顶部向下的偏移（向上滚动时变大）
            var y = Mathf.Max(0f, _content.anchoredPosition.y - _layoutPaddingTop);

            // 二分找最大的 i 使 prefix[i] <= y
            var lo = 0;
            var hi = totalCount;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) >> 1;
                if (prefix[mid] <= y)
                    lo = mid;
                else
                    hi = mid - 1;
            }

            return Mathf.Clamp(lo, 0, Mathf.Max(0, totalCount - 1));
        }

        private void ApplyWindow(List<LogEntry> snapshot, int startIndex, int count, int totalCount)
        {
            _windowStartIndex = startIndex;

            // Spacer heights: simulate offscreen items
            // 需要把 VerticalLayoutGroup.spacing 也计入，否则滚动时会因“理论高度 < 实际高度”产生底部空洞跳变。
            var top = 0f;
            for (var i = 0; i < startIndex; i++)
                top += GetEstimatedEntryHeight(snapshot[i]) + _layoutSpacing;
            var bottom = 0f;
            for (var i = startIndex + count; i < totalCount; i++)
                bottom += GetEstimatedEntryHeight(snapshot[i]) + _layoutSpacing;
            SetSpacerHeights(top, bottom);

            // Ensure we have exactly `count` visible items
            while (_visibleItems.Count > count)
            {
                var lastIdx = _visibleItems.Count - 1;
                var item = _visibleItems[lastIdx];
                _visibleItems.RemoveAt(lastIdx);
                ReleaseItem(item);
            }
            while (_visibleItems.Count < count)
            {
                var item = GetOrCreateItem();
                item.transform.SetParent(_content, worldPositionStays: false);
                // 插入到 bottomSpacer 之前
                if (_bottomSpacer != null)
                    item.transform.SetSiblingIndex(_bottomSpacer.transform.GetSiblingIndex());
                item.gameObject.SetActive(true);
                _visibleItems.Add(item);
            }

            // Bind data
            var anyHeightChanged = false;
            for (var i = 0; i < _visibleItems.Count; i++)
            {
                var entry = snapshot[startIndex + i];
                var item = _visibleItems[i];
                if (_messageFontOverride != null)
                    item.SetFont(_messageFontOverride);
                item.Bind(entry, ResolveIcon(entry.Channel));

                // 记录真实高度：文本长短导致 item 高度变化，需用于后续滚动映射与 spacer 计算。
                if (entry.Sequence > 0)
                {
                    var itemRt = item.transform as RectTransform;
                    if (itemRt != null)
                    {
                        LayoutRebuilder.ForceRebuildLayoutImmediate(itemRt);
                        var measured = Mathf.Max(1f, LayoutUtility.GetPreferredHeight(itemRt));
                        if (_heightBySequence.TryGetValue(entry.Sequence, out var old))
                        {
                            if (Mathf.Abs(old - measured) > 0.5f)
                            {
                                _heightBySequence[entry.Sequence] = measured;
                                anyHeightChanged = true;
                            }
                        }
                        else
                        {
                            _heightBySequence.Add(entry.Sequence, measured);
                            anyHeightChanged = true;
                        }
                    }
                }
            }

            if (anyHeightChanged)
                _pendingScrollRefresh = true;
        }

        private void EnsureSpacers()
        {
            if (_content == null)
                return;
            if (_topSpacer != null && _bottomSpacer != null)
                return;

            _topSpacer = FindOrCreateSpacer("TopSpacer", siblingIndex: 0);
            _bottomSpacer = FindOrCreateSpacer("BottomSpacer", siblingIndex: _content.childCount);

            // 保证 bottomSpacer 在最后
            _bottomSpacer.transform.SetAsLastSibling();
        }

        private LayoutElement FindOrCreateSpacer(string name, int siblingIndex)
        {
            var existing = _content.Find(name);
            GameObject go;
            if (existing != null)
                go = existing.gameObject;
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(_content, worldPositionStays: false);
            }

            go.SetActive(true);
            go.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, _content.childCount - 1));
            var le = go.GetComponent<LayoutElement>();
            if (le == null)
                le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = false;
            le.preferredHeight = 0f;
            le.minHeight = 0f;
            le.flexibleHeight = 0f;
            return le;
        }

        private void SetSpacerHeights(float top, float bottom)
        {
            if (_topSpacer != null)
                _topSpacer.preferredHeight = Mathf.Max(0f, top);
            if (_bottomSpacer != null)
                _bottomSpacer.preferredHeight = Mathf.Max(0f, bottom);
        }

        private void ClearVisibleToPool()
        {
            for (var i = _visibleItems.Count - 1; i >= 0; i--)
                ReleaseItem(_visibleItems[i]);
            _visibleItems.Clear();

            // 保持 spacers，但清为 0
            SetSpacerHeights(0f, 0f);
        }

        private Sprite ResolveIcon(LogChannel channel)
        {
            var idx = (int)channel;
            if (_channelIcons == null || idx < 0 || idx >= _channelIcons.Length)
                return null;
            return _channelIcons[idx];
        }

        private LogItemView GetOrCreateItem()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            return CreateItemInstance(parent: _poolRoot);
        }

        private LogItemView CreateItemInstance(Transform parent)
        {
            if (_itemPrefab == null)
                return null;
            var inst = Instantiate(_itemPrefab, parent);
            inst.gameObject.SetActive(false);
            return inst;
        }

        private void ReleaseItem(LogItemView item)
        {
            if (item == null)
                return;
            item.gameObject.SetActive(false);
            if (_poolRoot != null)
                item.transform.SetParent(_poolRoot, worldPositionStays: false); // 放回专用池容器，避免污染 Content
            else
                item.transform.SetParent(transform, worldPositionStays: false);
            _pool.Push(item);
        }
    }
}

