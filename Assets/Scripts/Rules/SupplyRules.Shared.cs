using OperationMarigold.AI.Simulation;

public static class SupplyRulesShared
{
    public static bool NeedsRuntimeSupply(IUnitReadView unit)
    {
        if (unit == null)
            return false;

        bool fuel = unit.Fuel < unit.MaxFuel;
        bool ammo = unit.MaxAmmo > 0 && unit.Ammo < unit.MaxAmmo;
        return fuel || ammo;
    }

    public static bool NeedsRuntimeSupply(UnitController unit)
    {
        return NeedsRuntimeSupply(unit as IUnitReadView);
    }

    public static bool CanSnapshotSupply(AIUnitSnapshot supplier, AIUnitSnapshot target, int distance)
    {
        if (!supplier.alive || !target.alive)
            return false;
        if (supplier.faction != target.faction)
            return false;
        if (!supplier.canSupply || supplier.hasActed)
            return false;
        if (target.embarkedOnUnitIndex >= 0)
            return false;
        if (distance != 1)
            return false;
        return NeedsSnapshotSupply(target);
    }

    public static bool NeedsSnapshotSupply(AIUnitSnapshot unit)
    {
        bool fuel = unit.fuel < unit.maxFuel;
        bool ammo = unit.hasPrimaryWeapon && unit.maxAmmo > 0 && unit.ammo < unit.maxAmmo;
        return fuel || ammo;
    }

    public static void ApplySnapshotSupply(ref AIUnitSnapshot supplier, ref AIUnitSnapshot target)
    {
        target.fuel = target.maxFuel;
        target.ammo = target.maxAmmo;
        supplier.hasActed = true;
    }
}
