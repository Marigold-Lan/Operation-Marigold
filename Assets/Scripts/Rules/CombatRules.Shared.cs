using UnityEngine;
using OperationMarigold.AI.Simulation;
using OperationMarigold.GameplayEvents;

public static class CombatRulesShared
{
    public static bool CanRuntimeAttack(IUnitReadView attacker, IUnitReadView defender, out AttackFailReason reason)
    {
        reason = AttackFailReason.Unknown;
        if (attacker is not UnitController attackerController)
        {
            reason = AttackFailReason.InvalidAttackerOrTarget;
            return false;
        }
        if (defender is not UnitController defenderController)
        {
            reason = AttackFailReason.InvalidAttackerOrTarget;
            return false;
        }

        return CombatRules.TryCreateStrike(
            attackerController,
            defenderController,
            requireDamageCapability: false,
            out _,
            out _,
            out _,
            out _,
            out reason);
    }

    public static bool CanRuntimeAttack(UnitController attacker, UnitController defender, out AttackFailReason reason)
    {
        return CanRuntimeAttack((IUnitReadView)attacker, (IUnitReadView)defender, out reason);
    }

    public static int CalculateSnapshotDamage(AIBoardState board, AIUnitSnapshot attacker, AIUnitSnapshot defender, bool usePrimary)
    {
        if (board == null)
            return 0;

        int baseDamage = usePrimary ? attacker.primaryBaseDamage : attacker.secondaryBaseDamage;
        int damagePercent = board.damageMatrix.GetPercent(attacker.unitId, defender.unitId, usePrimary);
        int terrainStars = 0;
        if (board.IsInBounds(defender.gridCoord))
            terrainStars = board.grid[defender.gridCoord.x, defender.gridCoord.y].terrainStars;

        int terrainBonus = terrainStars * TerrainStars.DamageReductionPerStar;
        int raw = DamageResolver.ResolveDamage(baseDamage, damagePercent, terrainBonus, out _);
        return DamageResolver.ApplyHpScale(raw, attacker.hp, attacker.maxHp);
    }

    public static bool CanSnapshotAttack(AIUnitSnapshot attacker, AIUnitSnapshot defender, int distance, out bool usePrimary)
    {
        return attacker.TrySelectWeapon(defender.category, distance, out usePrimary, out _);
    }
}
