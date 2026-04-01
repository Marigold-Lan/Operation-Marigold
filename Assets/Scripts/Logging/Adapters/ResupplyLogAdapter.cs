using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using OperationMarigold.Logging.Adapters.Common;
using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    public sealed class ResupplyLogAdapter : MonoBehaviour
    {
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        private void OnEnable()
        {
            _subscriptions.Add(
                () => TurnIncomeService.OnTurnIncomeStarted += HandleIncomeStarted,
                () => TurnIncomeService.OnTurnIncomeStarted -= HandleIncomeStarted);
            _subscriptions.Add(
                () => TurnIncomeService.OnIncomeFromBuilding += HandleIncomeFromBuilding,
                () => TurnIncomeService.OnIncomeFromBuilding -= HandleIncomeFromBuilding);
            _subscriptions.Add(
                () => TurnIncomeService.OnTurnIncomeCompleted += HandleIncomeCompleted,
                () => TurnIncomeService.OnTurnIncomeCompleted -= HandleIncomeCompleted);

            _subscriptions.Add(
                () => TurnResupplyService.OnTurnResupplyStarted += HandleResupplyStarted,
                () => TurnResupplyService.OnTurnResupplyStarted -= HandleResupplyStarted);
            _subscriptions.Add(
                () => TurnResupplyService.OnUnitResupplied += HandleUnitResupplied,
                () => TurnResupplyService.OnUnitResupplied -= HandleUnitResupplied);
            _subscriptions.Add(
                () => TurnResupplyService.OnUnitRepairAttempt += HandleRepairAttempt,
                () => TurnResupplyService.OnUnitRepairAttempt -= HandleRepairAttempt);
            _subscriptions.Add(
                () => TurnResupplyService.OnTurnResupplyCompleted += HandleResupplyCompleted,
                () => TurnResupplyService.OnTurnResupplyCompleted -= HandleResupplyCompleted);
        }

        private void OnDisable()
        {
            _subscriptions.DisposeAll();
        }

        private void HandleIncomeStarted(UnitFaction faction)
        {
            if (faction == UnitFaction.None) return;
            LogHub.Publish(LogChannel.Economy, $"Turn income starts: {LogFormat.Faction(faction)}.");
        }

        private void HandleIncomeFromBuilding(UnitFaction faction, BuildingController building, int income)
        {
            if (faction == UnitFaction.None || building == null || income <= 0)
                return;
            LogHub.Publish(LogChannel.Economy, $"Income: {LogFormat.Faction(faction)} +{income} from {LogFormat.BuildingName(building)}.");
        }

        private void HandleIncomeCompleted(UnitFaction faction, int totalIncome, int contributingBuildings)
        {
            if (faction == UnitFaction.None) return;
            LogHub.Publish(LogChannel.Economy, $"Turn income completed: {LogFormat.Faction(faction)} +{totalIncome} ({contributingBuildings} buildings).");
        }

        private void HandleResupplyStarted(UnitFaction faction)
        {
            if (faction == UnitFaction.None) return;
            LogHub.Publish(LogChannel.Economy, $"Turn resupply starts: {LogFormat.Faction(faction)}.");
        }

        private void HandleUnitResupplied(UnitController unit, bool fuelRefilled, bool ammoRefilled)
        {
            if (unit == null)
                return;

            if (fuelRefilled && ammoRefilled)
                LogHub.Publish(LogChannel.Economy, $"Resupply: {LogFormat.UnitName(unit)} refilled fuel+ammo.");
            else if (fuelRefilled)
                LogHub.Publish(LogChannel.Economy, $"Resupply: {LogFormat.UnitName(unit)} refilled fuel.");
            else if (ammoRefilled)
                LogHub.Publish(LogChannel.Economy, $"Resupply: {LogFormat.UnitName(unit)} refilled ammo.");
        }

        private void HandleRepairAttempt(UnitController unit, int repairCost, int repairHp, bool success, OperationMarigold.GameplayEvents.RepairFailReason? reason)
        {
            if (unit == null)
                return;

            if (success)
            {
                LogHub.Publish(LogChannel.Economy, $"Repair: {LogFormat.UnitName(unit)} +{repairHp}HP (cost {repairCost}).");
                return;
            }

            // 失败原因只在会造成“为何没修/没钱”困惑的情况下输出
            if (reason.HasValue && reason.Value != OperationMarigold.GameplayEvents.RepairFailReason.AlreadyFullHp)
                LogHub.Publish(LogChannel.Economy, $"Repair failed: {LogFormat.UnitName(unit)} (cost {repairCost}, hp {repairHp}) ({reason.Value}).");
        }

        private void HandleResupplyCompleted(UnitFaction faction, int processedUnits, int repairedUnits)
        {
            if (faction == UnitFaction.None) return;
            LogHub.Publish(LogChannel.Economy, $"Turn resupply completed: {LogFormat.Faction(faction)} processed {processedUnits}, repaired {repairedUnits}.");
        }
    }
}

