using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.GameplayEvents;

/// <summary>
/// 处理移动逻辑：执行移动到目标格子；支持沿路径逐格动画移动。
/// 调用方应保证 target 在可移动范围内；MoveTo 不再校验范围。
/// </summary>
public class UnitMovement : MonoBehaviour
{
    public static event Action<bool> OnAnyUnitMoveStateChanged;

    /// <summary>路径移动中每走完一格并落格后触发（参数：单位, 上一格, 当前格）。供音效等表现层订阅，与具体音频实现解耦。</summary>
    public static event Action<UnitController, Vector2Int, Vector2Int> OnMoveStepCompleted;

    /// <summary>该步落格时，出发格与目标格世界高度差超过 <see cref="JumpHeightSoundThreshold"/> 时触发。</summary>
    public static event Action<UnitController, Vector2Int, Vector2Int> OnMoveStepJump;

    /// <summary>移动中因朝向与下一步方向偏差超过阈值而开始转身时触发。</summary>
    public static event Action<UnitController> OnMoveFacingTurnStarted;

    [Tooltip("判定「跳跃」音效：出发格与目标格 transform.position.y 差值超过此值（米）时触发 OnMoveStepJump。")]
    [SerializeField] private float jumpHeightSoundThreshold = 0.08f;

    /// <summary>本单位开始沿路径移动时触发（实例事件，参数为发出事件的 UnitMovement，供场景级表现层订阅）。</summary>
    public event Action<UnitMovement> OnMoveStarted;

    /// <summary>本单位结束沿路径移动时触发（实例事件，参数为发出事件的 UnitMovement）。</summary>
    public event Action<UnitMovement> OnMoveEnded;

    /// <summary>
    /// 移动失败时触发（单位, 目标坐标, 失败原因）。
    /// </summary>
    public event Action<UnitController, Vector2Int, MoveFailReason> OnMoveFailed;

    /// <summary>
    /// 路径移动过程中中止时触发（单位, 当前坐标, 下一步坐标, 原因, 已消耗油量, 剩余油量）。
    /// </summary>
    public event Action<UnitController, Vector2Int, Vector2Int, MoveStopReason, int, int> OnPathTraversalStopped;

    [SerializeField] private float stepDuration = 0.25f;
    [SerializeField] private float turnDuration = 0.15f;
    [Tooltip("朝向与移动方向夹角超过此值时先转身")]
    [SerializeField] private float turnAngleThreshold = 15f;

    private UnitController _controller;
    private MapRoot _mapRoot;
    private Coroutine _movingCoroutine;

    public bool IsMoving => _movingCoroutine != null;

    public void Initialize(UnitController controller)
    {
        _controller = controller;
        _mapRoot = controller.MapRoot;
    }

    /// <summary>
    /// 移动到目标格子。返回是否成功。调用方需保证 target 在可移动范围内。
    /// </summary>
    public bool MoveTo(Vector2Int target)
    {
        if (_controller == null || _mapRoot == null)
        {
            OnMoveFailed?.Invoke(_controller, target, MoveFailReason.MissingControllerOrMapRoot);
            return false;
        }

        var oldCell = _controller.CurrentCell;
        var newCell = _mapRoot.GetCellAt(target);
        if (newCell == null)
        {
            OnMoveFailed?.Invoke(_controller, target, MoveFailReason.DestinationCellMissing);
            return false;
        }
        if (!MovementRules.CanOccupyDestination(newCell, _controller.gameObject))
        {
            OnMoveFailed?.Invoke(_controller, target, MoveFailReason.DestinationNotOccupiable);
            return false;
        }

        if (oldCell != null)
            oldCell.ClearUnit();

        _controller.GridCoord = target;
        _controller.CurrentCell = newCell;
        newCell.SetUnit(_controller.gameObject);

        _controller.transform.position = _mapRoot.GridToWorld(target);

        return true;
    }

    /// <summary>
    /// 沿路径逐格动画移动。path 需含起点，从 path[1] 起执行。
    /// </summary>
    public void MoveAlongPath(List<Vector2Int> path, Action onComplete = null)
    {
        if (path == null || path.Count < 2)
        {
            onComplete?.Invoke();
            return;
        }

        if (_movingCoroutine != null)
            StopCoroutine(_movingCoroutine);

        _movingCoroutine = StartCoroutine(MoveAlongPathCoroutine(path, onComplete));
    }

    private IEnumerator MoveAlongPathCoroutine(List<Vector2Int> path, Action onComplete)
    {
        if (_controller == null)
            _controller = GetComponent<UnitController>();
        if (_mapRoot == null && _controller != null)
            _mapRoot = _controller.MapRoot;
        if (_controller == null || _mapRoot == null)
        {
            _movingCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        OnAnyUnitMoveStateChanged?.Invoke(true);
        OnMoveStarted?.Invoke(this);
        var moved = false;
        var consumedFuel = 0;
        var startCoord = path[0];

        for (var i = 1; i < path.Count; i++)
        {
            var target = path[i];
            var leavingCell = _controller.CurrentCell;
            var enteringCell = _mapRoot.GetCellAt(target);
            if (!MovementRules.TryGetTraversalCost(_controller, target, startCoord, enteringCell, out var stepFuelCost))
            {
                var remainingFuel = Mathf.Max(0, _controller.CurrentFuel - consumedFuel);
                OnPathTraversalStopped?.Invoke(_controller, _controller.GridCoord, target, MoveStopReason.TraversalCostUnavailable, consumedFuel, remainingFuel);
                break;
            }
            if (_controller.CurrentFuel < consumedFuel + stepFuelCost)
            {
                var remainingFuel = Mathf.Max(0, _controller.CurrentFuel - consumedFuel);
                OnPathTraversalStopped?.Invoke(_controller, _controller.GridCoord, target, MoveStopReason.InsufficientFuel, consumedFuel, remainingFuel);
                break;
            }

            if (leavingCell != null)
                leavingCell.NotifyUnitWillLeave(_controller.gameObject, stepDuration);
            if (enteringCell != null)
                enteringCell.NotifyUnitWillEnter(_controller.gameObject, stepDuration);

            var moveDir = target - path[i - 1];
            var worldDir = new Vector3(moveDir.x, 0f, moveDir.y).normalized;
            if (worldDir.sqrMagnitude < 0.01f) worldDir = _controller.transform.forward;

            var currentForward = _controller.transform.forward;
            currentForward.y = 0f;
            if (currentForward.sqrMagnitude < 0.01f) currentForward = Vector3.forward;
            else currentForward.Normalize();

            var angle = Vector3.Angle(currentForward, worldDir);
            if (angle > turnAngleThreshold)
            {
                OnMoveFacingTurnStarted?.Invoke(_controller);
                yield return TurnToFaceCoroutine(worldDir);
            }

            var startPos = _controller.transform.position;
            var endPos = _mapRoot.GridToWorld(target);
            var elapsedTime = 0f;

            while (elapsedTime < stepDuration)
            {
                elapsedTime += Time.deltaTime;
                var t = elapsedTime / stepDuration;
                if (t > 1f) t = 1f;
                _controller.transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            MoveTo(target);
            moved = true;
            consumedFuel += stepFuelCost;
            _controller.View?.PlayMoveEffect();

            var fromCoord = path[i - 1];
            OnMoveStepCompleted?.Invoke(_controller, fromCoord, target);

            var yFrom = leavingCell != null ? leavingCell.transform.position.y : startPos.y;
            var yTo = enteringCell != null ? enteringCell.transform.position.y : endPos.y;
            if (Mathf.Abs(yTo - yFrom) > jumpHeightSoundThreshold)
                OnMoveStepJump?.Invoke(_controller, fromCoord, target);
        }

        _movingCoroutine = null;
        if (consumedFuel > 0)
            _controller.CurrentFuel -= consumedFuel;
        if (moved)
            _controller.HasMovedThisTurn = true;
        OnMoveEnded?.Invoke(this);
        OnAnyUnitMoveStateChanged?.Invoke(false);
        onComplete?.Invoke();
    }

    private IEnumerator TurnToFaceCoroutine(Vector3 worldDir)
    {
        var targetRot = Quaternion.LookRotation(worldDir);
        var startRot = _controller.transform.rotation;
        var elapsed = 0f;

        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            var t = elapsed / turnDuration;
            if (t > 1f) t = 1f;
            _controller.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        _controller.transform.rotation = targetRot;
    }
}
