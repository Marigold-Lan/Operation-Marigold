using OperationMarigold.AI.Simulation;
using UnityEngine;

public static class CaptureRulesShared
{
    public static bool IsRuntimeCapturer(IUnitReadView unit)
    {
        if (unit == null)
            return false;

        return unit.MovementType == MovementType.Foot || unit.MovementType == MovementType.Mech;
    }

    public static bool IsRuntimeCapturer(UnitController unit)
    {
        return IsRuntimeCapturer(unit as IUnitReadView);
    }

    public static bool IsSnapshotCapturer(AIUnitSnapshot unit)
    {
        return unit.category == UnitCategory.Soldier &&
               (unit.movementType == MovementType.Foot || unit.movementType == MovementType.Mech);
    }

    public static bool CanSnapshotCapture(AIUnitSnapshot unit, AIBuildingSnapshot building)
    {
        return IsSnapshotCapturer(unit) && unit.faction != building.ownerFaction;
    }

    public static bool ApplySnapshotCapture(ref AIBuildingSnapshot building, ref AIUnitSnapshot unit, int unitIndex)
    {
        if (!CanSnapshotCapture(unit, building))
            return false;

        if (building.captureActorUnitIndex >= 0 && building.captureActorUnitIndex != unitIndex)
            building.captureHp = building.maxCaptureHp;

        building.captureActorUnitIndex = unitIndex;

        int power = Mathf.Max(1, unit.hp);
        int damage = building.captureDamagePerStep * power;
        building.captureHp = Mathf.Max(0, building.captureHp - damage);

        if (building.captureHp <= 0)
        {
            building.ownerFaction = unit.faction;
            building.captureHp = building.maxCaptureHp;
            building.captureActorUnitIndex = -1;
        }

        unit.hasActed = true;
        return true;
    }
}
