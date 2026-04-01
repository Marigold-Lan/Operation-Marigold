using UnityEngine;
using OperationMarigold.GameplayEvents;

/// <summary>
/// 回合开始补给服务：处理建筑上的己方单位补油弹与修理扣费。
/// </summary>
public sealed class TurnResupplyService
{
    private static readonly TurnResupplyService _instance = new TurnResupplyService();

    public static TurnResupplyService Instance => _instance;

    public static event System.Action<UnitFaction> OnTurnResupplyStarted;
    public static event System.Action<UnitController, bool, bool> OnUnitResupplied;
    public static event System.Action<UnitController, int, int, bool, RepairFailReason?> OnUnitRepairAttempt;
    public static event System.Action<UnitFaction, int, int> OnTurnResupplyCompleted;

    private TurnResupplyService() { }

    public int ApplyTurnStartResupply(UnitFaction faction, MapRoot preferredRoot = null)
    {
        if (faction == UnitFaction.None)
            return 0;

        OnTurnResupplyStarted?.Invoke(faction);
        var processedUnits = 0;
        var repairedUnits = 0;
        var buildings = BuildingQueryService.Instance.FindAllBuildings(preferredRoot);
        for (var i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building == null || building.OwnerFaction != faction)
                continue;

            var cell = building.Cell;
            var unit = cell != null ? cell.UnitController : null;
            if (unit == null || unit.OwnerFaction != faction)
                continue;
            if (unit.Health != null && unit.Health.IsDead)
                continue;

            var oldFuel = unit.CurrentFuel;
            var oldAmmo = unit.CurrentAmmo;
            UnitResupplyRules.RefillFuelAndPrimaryAmmo(unit);
            var fuelRefilled = unit.Data != null && oldFuel < unit.Data.maxFuel && unit.CurrentFuel == unit.Data.maxFuel;
            var ammoRefilled = unit.Data != null && oldAmmo < unit.Data.MaxPrimaryAmmo && unit.CurrentAmmo == unit.Data.MaxPrimaryAmmo;
            if (fuelRefilled || ammoRefilled)
                OnUnitResupplied?.Invoke(unit, fuelRefilled, ammoRefilled);

            if (TryRepairOnBuilding(unit, faction))
                repairedUnits++;
            processedUnits++;
        }

        OnTurnResupplyCompleted?.Invoke(faction, processedUnits, repairedUnits);
        return processedUnits;
    }

    private static bool TryRepairOnBuilding(UnitController unit, UnitFaction faction)
    {
        var health = unit != null ? unit.Health : null;
        if (unit == null || health == null || unit.Data == null)
        {
            OnUnitRepairAttempt?.Invoke(unit, 0, 0, false, RepairFailReason.InvalidUnit);
            return false;
        }
        if (health.IsDead)
        {
            OnUnitRepairAttempt?.Invoke(unit, 0, 0, false, RepairFailReason.UnitDead);
            return false;
        }

        var maxHp = Mathf.Max(1, unit.Data.maxHp);
        if (health.CurrentHp >= maxHp)
        {
            OnUnitRepairAttempt?.Invoke(unit, 0, 0, false, RepairFailReason.AlreadyFullHp);
            return false;
        }

        var repairCost = UnitResupplyRules.CalculateBuildingRepairCost(unit);
        var repairHp = UnitResupplyRules.CalculateBuildingRepairHp(unit);
        if (repairCost <= 0 || repairHp <= 0)
        {
            OnUnitRepairAttempt?.Invoke(unit, repairCost, repairHp, false, RepairFailReason.InvalidCostOrHp);
            return false;
        }

        if (!FactionFundsLedger.Instance.TrySpendFunds(faction, repairCost))
        {
            OnUnitRepairAttempt?.Invoke(unit, repairCost, repairHp, false, RepairFailReason.InsufficientFunds);
            return false;
        }

        health.Heal(repairHp);
        OnUnitRepairAttempt?.Invoke(unit, repairCost, repairHp, true, null);
        return true;
    }
}
