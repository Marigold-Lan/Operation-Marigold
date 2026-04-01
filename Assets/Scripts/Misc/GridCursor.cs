using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格光标：上下左右移动 + 自转（平滑）。
/// 职责单一，便于扩展。
/// </summary>
public class GridCursor : Singleton<GridCursor>
{
    public static event Action<Vector2Int> OnCursorCoordChanged;
    public static event Action<Vector2Int> OnCursorVisualCoordChanged;
    public static event Action OnCursorVisualMoveStarted;

    [Header("依赖")]
    public MapRoot mapRoot;

    [Header("移动")]
    public float heightOffset = 0.1f;
    public Vector2Int initialCoord = Vector2Int.zero;
    [Tooltip("光标从当前格移动到目标格的平滑时长（秒）。0 表示瞬移。")]
    [Min(0f)] public float moveSmoothDuration = 0.08f;

    [Header("自转")]
    [Tooltip("自转速度（度/秒），正数顺时针")]
    public float rotationSpeed = 90f;
    [Tooltip("攻击选目标模式下，光标停在敌方单位格时的自转倍率。")]
    public float attackTargetEnemyRotationMultiplier = 2f;

    [Header("显示")]
    [Tooltip("仅控制可视显示的根节点；留空时默认使用整个光标对象。")]
    [SerializeField] private GameObject visualRoot;

    private Vector2Int _coord;
    private float _rotationY;
    private Vector3 _moveStartWorldPos;
    private Vector3 _moveTargetWorldPos;
    private float _moveElapsed;
    private bool _isVisualMoving;
    private bool _locked;
    private bool _externalInputLocked;
    private HashSet<Vector2Int> _allowedCoords;
    private bool _isAttackTargetingRotationBoostEnabled;
    private UnitFaction _attackSourceOwnerFaction = UnitFaction.Marigold;
    private Vector2Int _visualCoord;

    public Vector2Int Coord => _coord;
    public Vector2Int VisualCoord => _visualCoord;
    public bool IsVisualMoving => _isVisualMoving;
    public bool IsLocked => _locked || _externalInputLocked;
    public Cell CurrentCell => mapRoot != null ? mapRoot.GetCellAt(_coord) : null;
    public float RotationY => _rotationY;

    /// <summary>
    /// 仅切换光标可视，不影响逻辑与输入订阅。
    /// </summary>
    public void SetVisualVisible(bool visible)
    {
        if (visualRoot != null)
        {
            if (visualRoot.activeSelf != visible)
                visualRoot.SetActive(visible);
            return;
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;
    }

    private void OnEnable()
    {
        InputManager.RegisterMoveHandler(HandleMoveRequested, priority: -100);
        UnitMovement.OnAnyUnitMoveStateChanged += HandleAnyUnitMoveStateChanged;
    }

    private void OnDisable()
    {
        InputManager.UnregisterMoveHandler(HandleMoveRequested);
        UnitMovement.OnAnyUnitMoveStateChanged -= HandleAnyUnitMoveStateChanged;
    }

    private void HandleAnyUnitMoveStateChanged(bool isMoving)
    {
        _locked = isMoving;
    }

    private void Start()
    {
        // 进入游戏时兜底清理一次输入锁，避免上一状态残留导致光标无法移动。
        _externalInputLocked = false;

        if (mapRoot == null)
            mapRoot = MapRoot.Instance;

        if (mapRoot != null)
        {
            _coord = Clamp(initialCoord);
            SyncPosition(immediate: true);
            OnCursorCoordChanged?.Invoke(_coord);
            _visualCoord = _coord;
            OnCursorVisualCoordChanged?.Invoke(_visualCoord);
        }
    }

    private void Update()
    {
        UpdateVisualMove();
        Rotate();
    }

    private bool HandleMoveRequested(int dx, int dy)
    {
        if (FactoryPanelManager.IsAnyOpen)
            return false;
        if (IsLocked) return false;
        if (dx == 0 && dy == 0) return false;
        Move(dx, dy);
        return true;
    }

    /// <summary>
    /// 外部输入锁。用于 UI（如指令面板）打开时临时禁用地图光标移动。
    /// </summary>
    public void SetExternalInputLocked(bool locked)
    {
        _externalInputLocked = locked;
    }

    private void Rotate()
    {
        var speed = rotationSpeed;
        if (ShouldBoostRotationOnCurrentCell())
            speed *= Mathf.Max(1f, attackTargetEnemyRotationMultiplier);

        _rotationY = (_rotationY + speed * Time.deltaTime) % 360f;
        transform.rotation = Quaternion.Euler(0f, _rotationY, 0f);
    }

    // ---------- 移动 API ----------

    public void SetPosition(Vector2Int coord, bool immediate = false)
    {
        var clamped = Clamp(coord);
        if (!IsCoordAllowed(clamped)) return;
        if (_coord == clamped) return;

        _coord = clamped;
        SyncPosition(immediate);
        OnCursorCoordChanged?.Invoke(_coord);
        InputManager.NotifyCursorMoved();

        if (!_isVisualMoving)
        {
            _visualCoord = _coord;
            OnCursorVisualCoordChanged?.Invoke(_visualCoord);
        }
    }

    public void Move(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;

        // 在限制模式下，按方向跳到下一个可达白名单格子（可跨过中间无效格）。
        if (_allowedCoords != null && _allowedCoords.Count > 0)
        {
            if (TryFindNextAllowedCoordInDirection(dx, dy, out var nextAllowed))
                SetPosition(nextAllowed);
            else if (TryFindBestAllowedCoordInDirection(dx, dy, out var nextBest))
                SetPosition(nextBest);
            return;
        }

        SetPosition(_coord + new Vector2Int(dx, dy));
    }
    public void MoveUp() => Move(0, 1);
    public void MoveDown() => Move(0, -1);
    public void MoveLeft() => Move(-1, 0);
    public void MoveRight() => Move(1, 0);

    /// <summary>
    /// 设置光标可移动的坐标白名单。传空可清除限制。
    /// </summary>
    public void SetAllowedCoords(ICollection<Vector2Int> allowedCoords, bool snapToNearestAllowed = false)
    {
        if (allowedCoords == null || allowedCoords.Count == 0)
        {
            ClearAllowedCoords();
            return;
        }

        if (_allowedCoords == null)
            _allowedCoords = new HashSet<Vector2Int>();
        else
            _allowedCoords.Clear();

        foreach (var coord in allowedCoords)
        {
            var clamped = Clamp(coord);
            if (mapRoot == null || mapRoot.IsInBounds(clamped))
                _allowedCoords.Add(clamped);
        }

        if (_allowedCoords.Count == 0)
        {
            _allowedCoords = null;
            return;
        }

        if (snapToNearestAllowed && !_allowedCoords.Contains(_coord))
            SetPosition(FindNearestAllowedCoord(_coord));
    }

    public void ClearAllowedCoords()
    {
        _allowedCoords = null;
    }

    /// <summary>
    /// 设置攻击选目标模式下的旋转加速上下文。
    /// </summary>
    public void SetAttackTargetingRotationBoost(bool enabled, UnitFaction sourceOwnerFaction = UnitFaction.Marigold)
    {
        _isAttackTargetingRotationBoostEnabled = enabled;
        _attackSourceOwnerFaction = sourceOwnerFaction;
    }

    // ---------- 内部 ----------

    private void SyncPosition(bool immediate)
    {
        if (mapRoot == null) return;
        var targetPos = GetWorldPosition(_coord);
        if (immediate || moveSmoothDuration <= 0f)
        {
            transform.position = targetPos;
            _moveStartWorldPos = targetPos;
            _moveTargetWorldPos = targetPos;
            _moveElapsed = 0f;
            _isVisualMoving = false;
            return;
        }

        _moveStartWorldPos = transform.position;
        _moveTargetWorldPos = targetPos;
        _moveElapsed = 0f;
        _isVisualMoving = true;
        OnCursorVisualMoveStarted?.Invoke();
    }

    private Vector3 GetWorldPosition(Vector2Int coord)
    {
        var pos = mapRoot.GridToWorld(coord);
        pos.y += heightOffset;
        return pos;
    }

    private void UpdateVisualMove()
    {
        if (!_isVisualMoving)
            return;

        var duration = Mathf.Max(0.0001f, moveSmoothDuration);
        _moveElapsed += Time.deltaTime;
        var t = Mathf.Clamp01(_moveElapsed / duration);
        transform.position = Vector3.Lerp(_moveStartWorldPos, _moveTargetWorldPos, t);

        if (t >= 1f)
        {
            _isVisualMoving = false;
            _visualCoord = _coord;
            OnCursorVisualCoordChanged?.Invoke(_visualCoord);
        }
    }

    private Vector2Int Clamp(Vector2Int c)
    {
        if (mapRoot == null) return c;
        return new Vector2Int(
            Mathf.Clamp(c.x, 0, mapRoot.gridWidth - 1),
            Mathf.Clamp(c.y, 0, mapRoot.gridHeight - 1));
    }

    private bool IsCoordAllowed(Vector2Int coord)
    {
        return _allowedCoords == null || _allowedCoords.Count == 0 || _allowedCoords.Contains(coord);
    }

    private Vector2Int FindNearestAllowedCoord(Vector2Int from)
    {
        if (_allowedCoords == null || _allowedCoords.Count == 0)
            return from;

        var nearest = from;
        var bestDistance = int.MaxValue;
        foreach (var coord in _allowedCoords)
        {
            var distance = Mathf.Abs(coord.x - from.x) + Mathf.Abs(coord.y - from.y);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = coord;
        }

        return nearest;
    }

    private bool TryFindNextAllowedCoordInDirection(int dx, int dy, out Vector2Int next)
    {
        next = _coord;
        if (mapRoot == null || _allowedCoords == null || _allowedCoords.Count == 0)
            return false;

        var step = new Vector2Int(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1));
        var probe = _coord;
        while (true)
        {
            probe += step;
            if (!mapRoot.IsInBounds(probe))
                return false;

            if (_allowedCoords.Contains(probe))
            {
                next = probe;
                return true;
            }
        }
    }

    private bool TryFindBestAllowedCoordInDirection(int dx, int dy, out Vector2Int next)
    {
        next = _coord;
        if (_allowedCoords == null || _allowedCoords.Count == 0)
            return false;

        var dir = new Vector2Int(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1));
        if (dir == Vector2Int.zero) return false;

        var found = false;
        var bestCoord = _coord;
        var bestDistance = int.MaxValue;
        var bestForward = int.MinValue;

        foreach (var candidate in _allowedCoords)
        {
            if (candidate == _coord) continue;

            var delta = candidate - _coord;
            var forward = delta.x * dir.x + delta.y * dir.y;
            if (forward <= 0) continue;

            var distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            if (distance > bestDistance) continue;

            if (distance < bestDistance || forward > bestForward)
            {
                found = true;
                bestDistance = distance;
                bestForward = forward;
                bestCoord = candidate;
            }
        }

        if (!found) return false;
        next = bestCoord;
        return true;
    }

    private bool ShouldBoostRotationOnCurrentCell()
    {
        if (!_isAttackTargetingRotationBoostEnabled) return false;
        var cell = CurrentCell;
        if (cell == null) return false;

        var unit = cell.UnitController;
        if (unit == null) return false;

        return unit.OwnerFaction != _attackSourceOwnerFaction;
    }
}
