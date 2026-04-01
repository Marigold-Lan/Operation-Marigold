using UnityEngine;

/// <summary>
/// 纯计算服务：负责伤害百分比与地形减伤结算，不依赖 MonoBehaviour 生命周期。
/// </summary>
public static class DamageResolver
{
    public static int ResolveDamage(int baseDamage, int damagePercent, int terrainDefenseBonus, out int finalPercent)
    {
        finalPercent = Mathf.Clamp(damagePercent - terrainDefenseBonus, 0, 999);
        if (baseDamage <= 0 || finalPercent <= 0)
            return 0;

        var damage = Mathf.RoundToInt(baseDamage * finalPercent / 100f);
        return Mathf.Max(0, damage);
    }

    public static int GetTerrainDefenseBonus(MapRoot mapRoot, Vector2Int coord)
    {
        if (mapRoot == null)
            return 0;

        var cell = mapRoot.GetCellAt(coord);
        if (cell == null)
            return 0;

        var stars = cell.GetTerrainStars();
        return stars * TerrainStars.DamageReductionPerStar;
    }

    /// <summary>
    /// 只读视图版本：用于规则/计算层，避免直接依赖运行时 MapRoot/Cell。
    /// </summary>
    public static int GetTerrainDefenseBonus(IGridReadView grid, Vector2Int coord)
    {
        if (grid == null)
            return 0;

        return grid.TryGetCell(coord, out var cell)
            ? cell.TerrainStars * TerrainStars.DamageReductionPerStar
            : 0;
    }

    /// <summary>
    /// 与运行时战斗保持一致的 HP 缩放：ceil(rawDamage * currentHp / maxHp)。
    /// </summary>
    public static int ApplyHpScale(int rawDamage, int currentHp, int maxHp)
    {
        if (rawDamage <= 0)
            return Mathf.Max(0, rawDamage);

        var safeMaxHp = Mathf.Max(1, maxHp);
        var clampedHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
        if (clampedHp <= 0)
            return 0;
        if (clampedHp >= safeMaxHp)
            return rawDamage;

        var scaled = (rawDamage * clampedHp + (safeMaxHp - 1)) / safeMaxHp;
        return Mathf.Max(1, scaled);
    }
}
