using UnityEngine;

/// <summary>
/// 单位补给与修理规则。聚合 Fuel/Ammo 回满与建筑修理数值，供回合服务与指令复用。
/// </summary>
public static class UnitResupplyRules
{
    private const float BuildingRepairHpRate = 0.2f;
    private const float BuildingRepairCostRate = 0.2f;

    public static bool RefillFuelAndPrimaryAmmo(UnitController unit)
    {
        if (unit == null || unit.Data == null)
            return false;

        var changed = false;
        if (unit.CurrentFuel < unit.Data.maxFuel)
        {
            unit.CurrentFuel = unit.Data.maxFuel;
            changed = true;
        }

        if (unit.CurrentAmmo < unit.Data.MaxPrimaryAmmo)
        {
            unit.CurrentAmmo = unit.Data.MaxPrimaryAmmo;
            changed = true;
        }

        return changed;
    }

    public static int CalculateBuildingRepairHp(UnitController unit)
    {
        if (unit == null || unit.Data == null)
            return 0;

        return Mathf.Max(1, Mathf.CeilToInt(unit.Data.maxHp * BuildingRepairHpRate));
    }

    public static int CalculateBuildingRepairCost(UnitController unit)
    {
        if (unit == null || unit.Data == null)
            return 0;

        return Mathf.Max(0, Mathf.CeilToInt(unit.Data.cost * BuildingRepairCostRate));
    }
}
