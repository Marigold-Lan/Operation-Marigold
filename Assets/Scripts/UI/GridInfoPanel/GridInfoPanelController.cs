using TMPro;
using UnityEngine;

/// <summary>
/// 控制地图信息面板显示与内容刷新。
/// 常态（无范围高亮）显示；进入范围预览/攻击选目标等状态时隐藏。
/// </summary>
public class GridInfoPanelController : MonoBehaviour
{
    [Header("根节点")]
    [SerializeField] private GameObject panelRoot;

    [Header("左右面板")]
    [SerializeField] private GameObject terrainPanel;
    [SerializeField] private GameObject unitPanel;

    [Header("Terrain")]
    [SerializeField] private TMP_Text terrainNameText;
    [SerializeField] private TMP_Text terrainDefenseValueText;
    [SerializeField] private GameObject terrainCaptureRow;
    [SerializeField] private TMP_Text terrainCaptureValueText;

    [Header("Unit")]
    [SerializeField] private TMP_Text unitNameText;
    [SerializeField] private TMP_Text unitLifeValueText;
    [SerializeField] private TMP_Text unitFuelValueText;
    [SerializeField] private TMP_Text unitAmmoValueText;
    private CanvasGroup _panelCanvasGroup;
    private bool _isTurnIntroAnimating;

    private void Reset()
    {
        if (panelRoot == null)
            panelRoot = gameObject;
    }

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (panelRoot == gameObject)
        {
            _panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null)
                _panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        GridCursor.OnCursorCoordChanged += HandleCursorCoordChanged;
        TurnManager.OnTurnIntroAnimationStarted += HandleTurnIntroAnimationStarted;
        TurnManager.OnTurnIntroAnimationComplete += HandleTurnIntroAnimationComplete;
        Refresh();
    }

    private void OnDisable()
    {
        GridCursor.OnCursorCoordChanged -= HandleCursorCoordChanged;
        TurnManager.OnTurnIntroAnimationStarted -= HandleTurnIntroAnimationStarted;
        TurnManager.OnTurnIntroAnimationComplete -= HandleTurnIntroAnimationComplete;
    }

    private void Update()
    {
        // 范围高亮状态会在多处逻辑中变化，逐帧刷新可保证 UI 状态始终正确。
        Refresh();
    }

    private void HandleCursorCoordChanged(Vector2Int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        Cell cell;
        var hasCell = TryGetCurrentCell(out cell);
        var shouldShow = !_isTurnIntroAnimating &&
                         hasCell &&
                         (!IsInRangePreviewState() || ShouldShowTerrainInfoDuringAttackRange(cell));
        SetPanelVisible(shouldShow);
        if (!shouldShow || cell == null)
            return;

        RefreshTerrainPanel(cell);
        RefreshUnitPanel(cell);
    }

    private void HandleTurnIntroAnimationStarted(TurnContext _)
    {
        _isTurnIntroAnimating = true;
    }

    private void HandleTurnIntroAnimationComplete(TurnContext _)
    {
        _isTurnIntroAnimating = false;
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

        // 仅在“攻击范围高亮”存在时例外显示（移动范围预览仍隐藏）。
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

    private void RefreshTerrainPanel(Cell cell)
    {
        SetActive(terrainPanel, true);

        var terrainName = GetTerrainDisplayName(cell);
        SetText(terrainNameText, terrainName);
        SetText(terrainDefenseValueText, cell.GetTerrainStars().ToString());

        var building = cell.Building;
        var hasBuilding = building != null;
        SetActive(terrainCaptureRow, hasBuilding);
        if (hasBuilding)
            SetText(terrainCaptureValueText, building.CurrentCaptureHp.ToString());
    }

    private string GetTerrainDisplayName(Cell cell)
    {
        if (cell == null)
            return string.Empty;

        if (IsBridgeBase(cell.BaseType) && !string.IsNullOrWhiteSpace(cell.BaseType.displayName))
            return cell.BaseType.displayName;

        if (cell.PlaceableType != null && !string.IsNullOrWhiteSpace(cell.PlaceableType.displayName))
            return cell.PlaceableType.displayName;

        if (cell.BaseType != null && !string.IsNullOrWhiteSpace(cell.BaseType.displayName))
            return cell.BaseType.displayName;

        return "Unknown";
    }

    private static bool IsBridgeBase(TileBaseType baseType)
    {
        if (baseType == null)
            return false;

        var id = baseType.id;
        return id == "Bridge" || id == "RiverBridge";
    }

    private void RefreshUnitPanel(Cell cell)
    {
        var unit = cell != null ? cell.UnitController : null;
        var hasUnit = unit != null;
        SetActive(unitPanel, hasUnit);
        if (!hasUnit)
            return;

        var unitName = unit.Data != null && !string.IsNullOrWhiteSpace(unit.Data.displayName)
            ? unit.Data.displayName
            : unit.name;

        var life = unit.Health != null ? unit.Health.CurrentHp : 0;
        SetText(unitNameText, unitName);
        SetText(unitLifeValueText, life.ToString());
        SetText(unitFuelValueText, unit.CurrentFuel.ToString());
        // Ammo display rules:
        // - Has primary weapon (finite ammo): show current ammo number
        // - Only secondary weapon (infinite ammo): show +infinity
        // - No weapons: show -infinity
        var data = unit.Data;
        if (data == null || !data.HasAnyWeapon)
        {
            SetText(unitAmmoValueText, "-∞");
        }
        else if (data.HasPrimaryWeapon && data.MaxPrimaryAmmo > 0)
        {
            SetText(unitAmmoValueText, unit.CurrentAmmo.ToString());
        }
        else
        {
            SetText(unitAmmoValueText, "∞");
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot == null)
            return;

        // 若脚本挂在 panelRoot 本体上，不能直接 SetActive(false)，否则脚本会停掉且无法自行恢复显示。
        if (panelRoot == gameObject && _panelCanvasGroup != null)
        {
            _panelCanvasGroup.alpha = visible ? 1f : 0f;
            _panelCanvasGroup.blocksRaycasts = visible;
            _panelCanvasGroup.interactable = visible;
            return;
        }

        SetActive(panelRoot, visible);
    }
}
