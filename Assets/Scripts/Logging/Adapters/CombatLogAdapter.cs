using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using OperationMarigold.GameplayEvents;
using OperationMarigold.Logging.Adapters.Common;
using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    public sealed class CombatLogAdapter : MonoBehaviour
    {
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        private void OnEnable()
        {
            _subscriptions.Add(
                () => UnitCombat.OnAttackStarted += HandleAttackStarted,
                () => UnitCombat.OnAttackStarted -= HandleAttackStarted);
            _subscriptions.Add(
                () => UnitCombat.OnDamageApplied += HandleDamageApplied,
                () => UnitCombat.OnDamageApplied -= HandleDamageApplied);
            _subscriptions.Add(
                () => UnitCombat.OnAmmoConsumed += HandleAmmoConsumed,
                () => UnitCombat.OnAmmoConsumed -= HandleAmmoConsumed);
            _subscriptions.Add(
                () => UnitCombat.OnAttackFailed += HandleAttackFailed,
                () => UnitCombat.OnAttackFailed -= HandleAttackFailed);
            _subscriptions.Add(
                () => UnitCombat.OnCounterAttackStarted += HandleCounterAttackStarted,
                () => UnitCombat.OnCounterAttackStarted -= HandleCounterAttackStarted);
            _subscriptions.Add(
                () => UnitCombat.OnUnitKilled += HandleUnitKilled,
                () => UnitCombat.OnUnitKilled -= HandleUnitKilled);
        }

        private void OnDisable()
        {
            _subscriptions.DisposeAll();
        }

        private void HandleAttackStarted(UnitController attacker, UnitController defender, int predictedDamage, bool usePrimary)
        {
            if (attacker == null || defender == null)
                return;

            var weapon = usePrimary ? "Primary" : "Secondary";
            LogHub.Publish(
                LogChannel.Combat,
                $"{LogFormat.Faction(attacker.OwnerFaction)} {LogFormat.UnitName(attacker)} attacks {LogFormat.Faction(defender.OwnerFaction)} {LogFormat.UnitName(defender)} ({weapon}, predicted {predictedDamage}).");
        }

        private void HandleDamageApplied(UnitController attacker, UnitController defender, int damage)
        {
            if (attacker == null || defender == null)
                return;

            LogHub.Publish(
                LogChannel.Combat,
                $"Hit: {LogFormat.UnitName(attacker)} -> {LogFormat.UnitName(defender)} for {damage} damage.");
        }

        private void HandleAmmoConsumed(UnitController unit, int delta)
        {
            if (unit == null || delta <= 0)
                return;

            LogHub.Publish(LogChannel.Combat, $"{LogFormat.UnitName(unit)} consumed ammo {delta} (remaining {unit.CurrentAmmo}).");
        }

        private void HandleAttackFailed(UnitController attacker, UnitController defender, AttackFailReason reason)
        {
            var attackerName = attacker != null ? LogFormat.UnitName(attacker) : "UnknownUnit";
            var defenderName = defender != null ? LogFormat.UnitName(defender) : "UnknownUnit";
            LogHub.Publish(LogChannel.Combat, $"Attack failed: {attackerName} -> {defenderName} ({reason}).");
        }

        private void HandleCounterAttackStarted(UnitController counterAttacker, UnitController target, int predictedDamage, bool usePrimary)
        {
            if (counterAttacker == null || target == null)
                return;
            var weapon = usePrimary ? "Primary" : "Secondary";
            LogHub.Publish(
                LogChannel.Combat,
                $"Counter-attack: {LogFormat.UnitName(counterAttacker)} -> {LogFormat.UnitName(target)} ({weapon}, predicted {predictedDamage}).");
        }

        private void HandleUnitKilled(UnitController killer, UnitController victim, int damage)
        {
            var killerName = killer != null ? LogFormat.UnitName(killer) : "UnknownUnit";
            var victimName = victim != null ? LogFormat.UnitName(victim) : "UnknownUnit";
            LogHub.Publish(LogChannel.Combat, $"Unit killed: {killerName} -> {victimName} (damage {damage}).");
        }
    }
}

