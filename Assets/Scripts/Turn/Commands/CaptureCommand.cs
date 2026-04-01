using UnityEngine;
using OperationMarigold.GameplayEvents;

public class CaptureCommand : ICommand
{
    public CommandType Type => CommandType.Capture;

    /// <summary>
    /// 占领命令被拒绝时触发（单位、建筑、原因）。这是一个事实事件出口，供日志/遥测等订阅。
    /// </summary>
    public static event System.Action<UnitController, BuildingController, CaptureRejectReason> OnCaptureCommandRejected;

    public bool CanExecute(CommandContext context)
    {
        if (context?.Unit == null)
            return false;

        var unit = context.Unit;
        if (!CaptureRulesShared.IsRuntimeCapturer((IUnitReadView)unit))
            return false;

        var building = context.CurrentCell != null ? context.CurrentCell.Building : null;
        if (building == null)
            return false;

        var buildingRead = building as IBuildingReadView;
        if (buildingRead == null)
            return false;

        return buildingRead.OwnerFaction != unit.OwnerFaction;
    }

    public void Execute(CommandContext context)
    {
        var unit = context?.Unit;
        if (unit == null)
        {
            OnCaptureCommandRejected?.Invoke(null, null, CaptureRejectReason.UnitMissing);
            return;
        }

        var building = context.CurrentCell != null ? context.CurrentCell.Building : null;
        if (building == null)
        {
            OnCaptureCommandRejected?.Invoke(unit, null, CaptureRejectReason.NoBuildingOnCell);
            return;
        }

        var target = building as ICaptureTarget;
        if (target == null)
        {
            OnCaptureCommandRejected?.Invoke(unit, building, CaptureRejectReason.TargetNotCaptureTarget);
            return;
        }

        if (target.OwnerFaction == unit.OwnerFaction)
        {
            OnCaptureCommandRejected?.Invoke(unit, building, CaptureRejectReason.AlreadyOwnedByFaction);
            return;
        }

        TryCaptureWithFallback(unit, target);
        unit.HasActed = true;
        context.HighlightManager?.ClearMoveHighlights();
        context.HighlightManager?.ClearAttackHighlights();
        context.SelectionManager?.ClearSelection();
    }

    public static bool IsInfantryOrMech(UnitController unit)
    {
        return CaptureRulesShared.IsRuntimeCapturer((IUnitReadView)unit);
    }

    private static bool TryCaptureWithFallback(UnitController unit, ICaptureTarget target)
    {
        var capturable = unit.GetComponent<ICapturable>();
        if (capturable != null)
            return capturable.TryCapture(target);

        var hp = unit.Health != null ? unit.Health.CurrentHp : (unit.Data != null ? unit.Data.maxHp : 0);
        var capturePower = Mathf.Max(1, hp);
        return target.ApplyCapture(capturePower, unit.OwnerFaction, unit);
    }
}
