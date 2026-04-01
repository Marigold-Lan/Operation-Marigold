/// <summary>
/// 纯计算：用于 UI/提示层做“攻击/反击”伤害预览，不触发真实战斗流程。
/// </summary>
public static class CombatPreviewService
{
    public static bool TryPreviewStrike(
        UnitController attacker,
        UnitController defender,
        bool requireDamageCapability,
        out int predictedDamage,
        out bool usesPrimaryWeapon,
        int attackerHpOverride = -1)
    {
        predictedDamage = 0;
        usesPrimaryWeapon = false;

        if (attacker == null || defender == null)
            return false;
        if (attacker.Data == null || defender.Data == null)
            return false;
        if (attacker.Health != null && attacker.Health.IsDead)
            return false;
        if (defender.Health != null && defender.Health.IsDead)
            return false;

        if (!CombatRules.TryCreateStrike(
                attacker,
                defender,
                requireDamageCapability,
                out _,
                out usesPrimaryWeapon,
                out _,
                out predictedDamage,
                out _,
                attackerHpOverride))
            return false;

        if (requireDamageCapability && predictedDamage <= 0)
            return false;

        return true;
    }

    public static bool CanCounterAttack(UnitController defender, UnitController attacker)
    {
        return TryPreviewStrike(defender, attacker, requireDamageCapability: true, out _, out _);
    }
}

