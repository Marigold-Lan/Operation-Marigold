using UnityEngine;
using OperationMarigold.GameplayEvents;

/// <summary>
/// 纯规则层：提供攻击武器选择、合法性判定和伤害计算。
/// 不处理动画、协程或事件派发。
/// </summary>
public static class CombatRules
{
    public static bool TryCreateStrike(
        UnitController attacker,
        UnitController defender,
        bool requireDamageCapability,
        out UnitWeaponData weapon,
        out bool usePrimary,
        out int damagePercent,
        out int damage,
        out AttackFailReason failReason,
        int attackerHpOverride = -1)
    {
        weapon = null;
        usePrimary = false;
        damagePercent = 0;
        damage = 0;
        failReason = AttackFailReason.Unknown;

        if (attacker == null || defender == null)
        {
            failReason = AttackFailReason.InvalidAttackerOrTarget;
            return false;
        }

        if (attacker.Data == null || defender.Data == null)
        {
            failReason = AttackFailReason.MissingUnitData;
            return false;
        }

        if (attacker.Health != null && attacker.Health.IsDead)
        {
            failReason = AttackFailReason.AttackerDead;
            return false;
        }

        if (defender.Health != null && defender.Health.IsDead)
        {
            failReason = AttackFailReason.TargetDead;
            return false;
        }

        if (!attacker.Data.TrySelectWeaponForTarget(
                defender.Data,
                attacker.CurrentAmmo,
                attacker.HasMovedThisTurn,
                out weapon,
                out usePrimary))
        {
            failReason = AttackFailReason.NoValidWeapon;
            return false;
        }

        if (weapon == null)
        {
            failReason = AttackFailReason.NoValidWeapon;
            return false;
        }

        var distance = Mathf.Abs(attacker.GridCoord.x - defender.GridCoord.x) + Mathf.Abs(attacker.GridCoord.y - defender.GridCoord.y);
        if (!weapon.IsDistanceInRange(distance))
        {
            failReason = AttackFailReason.OutOfRange;
            return false;
        }

        damagePercent = weapon.GetDamagePercent(defender.Data.id);
        if (requireDamageCapability && (weapon.baseDamage <= 0 || damagePercent <= 0))
        {
            failReason = AttackFailReason.NoDamageCapability;
            return false;
        }

        var attackerMapRoot = attacker.MapRoot != null ? attacker.MapRoot : MapRoot.Instance;
        damage = CalculateScaledDamage(
            weapon.baseDamage,
            damagePercent,
            attackerMapRoot,
            defender.GridCoord,
            attackerHpOverride >= 0 ? attackerHpOverride : (attacker.Health != null ? attacker.Health.CurrentHp : attacker.Data.maxHp),
            attacker.Data.maxHp);

        if (requireDamageCapability && damage <= 0)
        {
            failReason = AttackFailReason.NoDamageCapability;
            return false;
        }

        failReason = AttackFailReason.Unknown;
        return true;
    }

    public static int CalculateScaledDamage(
        int baseDamage,
        int damagePercent,
        MapRoot mapRoot,
        Vector2Int defenderCoord,
        int attackerCurrentHp,
        int attackerMaxHp)
    {
        // 通过只读视图接口计算地形减伤，减少规则层对具体运行时类型的耦合。
        var terrainBonus = DamageResolver.GetTerrainDefenseBonus((IGridReadView)mapRoot, defenderCoord);
        var raw = DamageResolver.ResolveDamage(baseDamage, damagePercent, terrainBonus, out _);
        return DamageResolver.ApplyHpScale(raw, attackerCurrentHp, attackerMaxHp);
    }
}
