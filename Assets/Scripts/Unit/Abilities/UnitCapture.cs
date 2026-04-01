using System.Collections;
using UnityEngine;
using OperationMarigold.GameplayEvents;

/// <summary>
/// 占领建筑能力实现。只有挂载此组件的单位（如步兵/机甲）才能与建筑交互占领。
/// </summary>
public class UnitCapture : MonoBehaviour, ICapturable
{
    [SerializeField] private int _capturePointPerHp = 1;

    private UnitActionMotion _motion;
    private bool _isCapturing;

    public int CapturePointPerHp => _capturePointPerHp;

    public static event System.Action<UnitController, BuildingController, int> OnCaptureStarted;
    public static event System.Action<UnitController, BuildingController, CaptureInterruptReason> OnCaptureInterrupted;
    public static event System.Action<UnitController, BuildingController, int, int, int, bool> OnCaptureApplied;

    private void Awake()
    {
        _motion = GetComponent<UnitActionMotion>();
        if (_motion == null)
            _motion = gameObject.AddComponent<UnitActionMotion>();
    }

    public bool TryCapture(object building)
    {
        if (building == null)
        {
            OnCaptureInterrupted?.Invoke(GetComponent<UnitController>(), null, CaptureInterruptReason.InvalidTarget);
            return false;
        }
        var target = building as ICaptureTarget;
        if (target == null)
        {
            OnCaptureInterrupted?.Invoke(GetComponent<UnitController>(), building as BuildingController, CaptureInterruptReason.InvalidTarget);
            return false;
        }
        if (_isCapturing)
        {
            OnCaptureInterrupted?.Invoke(GetComponent<UnitController>(), building as BuildingController, CaptureInterruptReason.AlreadyCapturing);
            return false;
        }

        var controller = GetComponent<UnitController>();
        if (controller == null)
        {
            OnCaptureInterrupted?.Invoke(null, building as BuildingController, CaptureInterruptReason.CapturerMissing);
            return false;
        }

        var hp = controller.Health != null ? controller.Health.CurrentHp : (controller.Data != null ? controller.Data.maxHp : 0);
        var capturePower = Mathf.Max(1, hp * Mathf.Max(1, _capturePointPerHp));
        var targetBuilding = target as BuildingController;

        if (_motion == null)
        {
            OnCaptureStarted?.Invoke(controller, targetBuilding, capturePower);
            var oldHp = target.CurrentCaptureHp;
            var captured = target.ApplyCapture(capturePower, controller.OwnerFaction, controller);
            var newHp = target.CurrentCaptureHp;
            OnCaptureApplied?.Invoke(controller, targetBuilding, capturePower, oldHp, newHp, captured);
            return captured;
        }

        OnCaptureStarted?.Invoke(controller, targetBuilding, capturePower);
        StartCoroutine(CaptureSequence(target, capturePower, controller));
        return true;
    }

    private IEnumerator CaptureSequence(ICaptureTarget target, int capturePower, UnitController controller)
    {
        _isCapturing = true;

        yield return _motion.PlayBounce(UnitActionMotion.BouncePreset.Capture);

        var isControllerValid = controller != null && (controller.Health == null || !controller.Health.IsDead);
        var isStillOnTargetBuilding = controller != null &&
                                      controller.CurrentCell != null &&
                                      (UnityEngine.Object)controller.CurrentCell.Building == (UnityEngine.Object)target;
        var targetBuilding = target as BuildingController;
        if (target == null)
        {
            OnCaptureInterrupted?.Invoke(controller, targetBuilding, CaptureInterruptReason.InvalidTarget);
        }
        else if (!isControllerValid)
        {
            OnCaptureInterrupted?.Invoke(controller, targetBuilding, CaptureInterruptReason.CapturerDead);
        }
        else if (!isStillOnTargetBuilding)
        {
            OnCaptureInterrupted?.Invoke(controller, targetBuilding, CaptureInterruptReason.CapturerLeftTargetCell);
        }
        else
        {
            var oldHp = target.CurrentCaptureHp;
            var captured = target.ApplyCapture(capturePower, controller.OwnerFaction, controller);
            var newHp = target.CurrentCaptureHp;
            OnCaptureApplied?.Invoke(controller, targetBuilding, capturePower, oldHp, newHp, captured);
        }

        _isCapturing = false;
    }
}
