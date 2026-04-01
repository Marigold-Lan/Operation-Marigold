using UnityEngine;

[DisallowMultipleComponent]
public sealed class AttackInfoHoverPreviewController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform _uiContainer;
    [SerializeField] private AttackInfoView _attackInfoPrefab;

    [Header("Content (Attacker)")]
    [SerializeField] private Sprite _attackerIcon;

    [Header("Content (Defender / Counter)")]
    [SerializeField] private Sprite _defenderCounterIcon;

    [Header("Content (Defender / No Counter)")]
    [SerializeField] private Sprite _defenderNoCounterIcon;

    [Header("Positioning")]
    [SerializeField] private Vector3 _worldAnchorOffset = Vector3.zero;
    [SerializeField] private Vector2 _canvasOffset = Vector2.zero;
    [SerializeField] private Vector2 _leftPanelCanvasOffset = Vector2.zero;
    [SerializeField] private Vector2 _rightPanelCanvasOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float _overlapPushPixels = 50f;

    private AttackInfoView _attackerViewInstance;
    private AttackInfoView _defenderViewInstance;
    private Canvas _canvas;
    private Camera _uiCamera;

    private void Awake()
    {
        if (_uiContainer != null)
        {
            _canvas = _uiContainer.GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCamera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
        }
    }

    private void OnEnable()
    {
        GridCursor.OnCursorCoordChanged += HandleCursorCoordChanged;
        UnitCombat.OnAttackStarted += HandleAttackStarted;
        Refresh();
    }

    private void OnDisable()
    {
        GridCursor.OnCursorCoordChanged -= HandleCursorCoordChanged;
        UnitCombat.OnAttackStarted -= HandleAttackStarted;
        HideAll();
    }

    private void LateUpdate()
    {
        // Keep UI positions stable if camera moves while hovering,
        // and ensure first-frame refresh when entering attack targeting.
        var selection = SelectionManager.Instance;
        if (selection != null && selection.IsAttackTargeting)
            Refresh();
    }

    private void HandleCursorCoordChanged(Vector2Int _)
    {
        Refresh();
    }

    private void HandleAttackStarted(UnitController attacker, UnitController defender, int predictedDamage, bool usePrimary)
    {
        // Requirement: pressing/confirming attack should immediately destroy the attack info panels.
        DestroyInstances();
    }

    private void Refresh()
    {
        var selection = SelectionManager.Instance;
        if (selection == null || !selection.IsAttackTargeting)
        {
            HideAll();
            return;
        }

        var attacker = selection.AttackTargetingSourceUnit;
        var cursor = GridCursor.Instance;
        var cell = cursor != null ? cursor.CurrentCell : null;
        var defender = cell != null ? cell.UnitController : null;

        if (attacker == null || defender == null || attacker == defender)
        {
            HideAll();
            return;
        }

        if (defender.OwnerFaction == attacker.OwnerFaction)
        {
            HideAll();
            return;
        }

        if (!IsValidAttackTarget(attacker, defender))
        {
            HideAll();
            return;
        }

        EnsureInstances();
        if (_attackerViewInstance == null || _defenderViewInstance == null)
            return;

        var canAttack = CombatPreviewService.TryPreviewStrike(
            attacker,
            defender,
            requireDamageCapability: false,
            out var attackerDamage,
            out _);

        if (!canAttack)
        {
            HideAll();
            return;
        }

        var defenderHpBefore = defender.Health != null ? defender.Health.CurrentHp : 0;
        var defenderHpAfter = Mathf.Max(0, defenderHpBefore - Mathf.Max(0, attackerDamage));

        var counterDamage = 0;
        var counterPossible = defenderHpAfter > 0 &&
                              CombatPreviewService.TryPreviewStrike(
                                  defender,
                                  attacker,
                                  requireDamageCapability: true,
                                  out counterDamage,
                                  out _,
                                  attackerHpOverride: defenderHpAfter);

        var attackerHpBefore = attacker.Health != null ? attacker.Health.CurrentHp : 0;
        var attackerHpAfter = Mathf.Max(0, attackerHpBefore - (counterPossible ? Mathf.Max(0, counterDamage) : 0));

        var attackerScreen = RectTransformUtility.WorldToScreenPoint(Camera.main, attacker.transform.position + _worldAnchorOffset);
        var defenderScreen = RectTransformUtility.WorldToScreenPoint(Camera.main, defender.transform.position + _worldAnchorOffset);

        var attackerIsLeft = attackerScreen.x < defenderScreen.x;
        var defenderIsLeft = !attackerIsLeft;

        _attackerViewInstance.SetContent(_attackerIcon, attackerHpBefore, attackerHpAfter);
        _attackerViewInstance.ApplyMirrorForLeftSide(attackerIsLeft);

        if (counterPossible)
            _defenderViewInstance.SetContent(_defenderCounterIcon, defenderHpBefore, defenderHpAfter);
        else
            _defenderViewInstance.SetContent(_defenderNoCounterIcon, defenderHpBefore, defenderHpAfter);
        _defenderViewInstance.ApplyMirrorForLeftSide(defenderIsLeft);

        var attackerRect = _attackerViewInstance.transform as RectTransform;
        var defenderRect = _defenderViewInstance.transform as RectTransform;
        var attackerSideOffset = attackerIsLeft ? _leftPanelCanvasOffset : _rightPanelCanvasOffset;
        var defenderSideOffset = defenderIsLeft ? _leftPanelCanvasOffset : _rightPanelCanvasOffset;
        PlaceAtScreen(attackerRect, attackerScreen, attackerSideOffset);
        PlaceAtScreen(defenderRect, defenderScreen, defenderSideOffset);
        ResolveOverlap(attackerRect, defenderRect);

        SetVisible(_attackerViewInstance, true);
        SetVisible(_defenderViewInstance, true);
    }

    private bool IsValidAttackTarget(UnitController attacker, UnitController defender)
    {
        if (attacker == null || defender == null) return false;
        if (attacker.Data == null || defender.Data == null) return false;
        if (!attacker.Data.CanAttackTarget(defender.Data, attacker.CurrentAmmo, attacker.HasMovedThisTurn))
            return false;

        var distance = Mathf.Abs(attacker.GridCoord.x - defender.GridCoord.x) + Mathf.Abs(attacker.GridCoord.y - defender.GridCoord.y);
        return attacker.Data.CanAttackAtDistance(distance, attacker.CurrentAmmo, attacker.HasMovedThisTurn);
    }

    private void EnsureInstances()
    {
        if (_uiContainer == null || _attackInfoPrefab == null)
            return;

        if (_attackerViewInstance == null)
            _attackerViewInstance = Instantiate(_attackInfoPrefab, _uiContainer);
        if (_defenderViewInstance == null)
            _defenderViewInstance = Instantiate(_attackInfoPrefab, _uiContainer);
    }

    private void DestroyInstances()
    {
        if (_attackerViewInstance != null)
            Destroy(_attackerViewInstance.gameObject);
        if (_defenderViewInstance != null)
            Destroy(_defenderViewInstance.gameObject);
        _attackerViewInstance = null;
        _defenderViewInstance = null;
    }

    private void HideAll()
    {
        SetVisible(_attackerViewInstance, false);
        SetVisible(_defenderViewInstance, false);
    }

    private void SetVisible(AttackInfoView view, bool visible)
    {
        if (view == null) return;
        var go = view.gameObject;
        if (go.activeSelf != visible)
            go.SetActive(visible);
    }

    private void PlaceAtScreen(RectTransform rect, Vector2 screenPos, Vector2 sideOffset)
    {
        if (rect == null || _uiContainer == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_uiContainer, screenPos, _uiCamera, out var localPoint);
        rect.anchoredPosition = localPoint + _canvasOffset + sideOffset;
    }

    private void ResolveOverlap(RectTransform a, RectTransform b)
    {
        if (a == null || b == null) return;
        if (_overlapPushPixels <= 0f) return;

        var pa = a.anchoredPosition;
        var pb = b.anchoredPosition;
        var delta = pb - pa;
        var dist = delta.magnitude;

        // If the two UI anchors are too close, push them outward along their connecting line.
        // When exactly same point, push along X.
        var minDist = _overlapPushPixels;
        if (dist >= minDist)
            return;

        var dir = dist > 0.001f ? (delta / dist) : Vector2.right;
        var push = (minDist - dist) * 0.5f;
        a.anchoredPosition = pa - dir * push;
        b.anchoredPosition = pb + dir * push;
    }
}

