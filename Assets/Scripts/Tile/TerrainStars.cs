using UnityEngine;

/// <summary>
/// 地形星数相关常量。地形星数决定驻扎单位的掩体减伤比例。
/// 0星=暴露(道路/桥梁)，1星=平原10%，2星=森林20%，3星=山脉/城市30%，4星=总部40%。
/// </summary>
public static class TerrainStars
{
    /// <summary>
    /// 每颗星提供的伤害减免（百分比点）。
    /// </summary>
    public const int DamageReductionPerStar = 10;

    /// <summary>
    /// 将星数转换为减伤百分比（0–40）。
    /// </summary>
    public static int StarsToDamageReductionPercent(int stars)
    {
        return Mathf.Clamp(stars, 0, 4) * DamageReductionPerStar;
    }
}
