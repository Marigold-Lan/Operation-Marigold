using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 单位静态配置数据，作为 ScriptableObject 存储。不包含运行时的动态状态。
/// </summary>
[CreateAssetMenu(fileName = "NewUnitData", menuName = OperationMarigoldPaths.SoUnitData)]
public class UnitData : ScriptableObject
{
    [SerializeField, HideInInspector] private bool _legacyMigrated;
    [SerializeField, HideInInspector] private bool canAttack = true;
    [SerializeField, HideInInspector] private int attackRangeMin = 1;
    [SerializeField, HideInInspector] private int attackRangeMax = 1;
    [SerializeField, HideInInspector] private int primaryWeaponDamage = 10;
    [SerializeField, HideInInspector] private int secondaryWeaponDamage = 5;
    [SerializeField, HideInInspector] private List<DamageMatrixEntry> primaryDamageMatrix = new();
    [SerializeField, HideInInspector] private List<DamageMatrixEntry> secondaryDamageMatrix = new();

    [Header("基础信息")]
    public string id;
    public string displayName;
    [TextArea(2, 5)]
    public string Description;
    [Tooltip("造价（金钱）")]
    public int cost = 1000;
    [Tooltip("造兵时实例化的 prefab，工厂用。")]
    public GameObject prefab;

    [Header("生命与补给")]
    [Tooltip("最大生命值")]
    public int maxHp = 10;
    [Tooltip("最大燃料")]
    public int maxFuel = 99;
    [Tooltip("最大弹药")]
    public int maxAmmo = 9;

    [Header("移动")]
    [Tooltip("最大移动力（格子数）")]
    public int movementRange = 3;
    public MovementType movementType = MovementType.Foot;

    [Header("视野")]
    public int visionRange = 2;

    [Header("单位类别")]
    [Tooltip("单位标识：Vehicle 或 Soldier。")]
    public UnitCategory category = UnitCategory.Vehicle;

    [Header("AI")]
    [Tooltip("AI 角色覆写。Auto 表示由 AI 根据单位属性自动判定。")]
    public UnitAIRoleTag aiRoleOverride = UnitAIRoleTag.Auto;

    [Header("武器槽位")]
    [Tooltip("主武器槽位。主武器弹药有限。")]
    public bool hasPrimaryWeapon = true;
    [FormerlySerializedAs("primaryWeaponDamage")]
    public UnitWeaponData primaryWeapon = new UnitWeaponData { weaponName = "Primary Weapon", baseDamage = 10, attackRangeMin = 1, attackRangeMax = 1 };

    [Tooltip("副武器槽位。副武器弹药无限。")]
    public bool hasSecondaryWeapon = true;
    [FormerlySerializedAs("secondaryWeaponDamage")]
    public UnitWeaponData secondaryWeapon = new UnitWeaponData { weaponName = "Secondary Weapon", baseDamage = 5, attackRangeMin = 1, attackRangeMax = 1 };

    [Header("主武器弹药")]
    [Tooltip("主武器最大弹药。仅当存在主武器时生效。")]
    [FormerlySerializedAs("maxAmmo")]
    public int primaryAmmoCapacity = 9;

    public bool HasPrimaryWeapon => hasPrimaryWeapon && primaryWeapon != null;
    public bool HasSecondaryWeapon => hasSecondaryWeapon && secondaryWeapon != null;
    public bool HasAnyWeapon => HasPrimaryWeapon || HasSecondaryWeapon;
    public int MaxPrimaryAmmo => HasPrimaryWeapon ? Mathf.Max(0, primaryAmmoCapacity) : 0;

    public bool CanAttackTarget(UnitData targetData, int currentPrimaryAmmo)
    {
        return CanAttackTarget(targetData, currentPrimaryAmmo, hasMovedThisTurn: false);
    }

    public bool CanAttackTarget(UnitData targetData, int currentPrimaryAmmo, bool hasMovedThisTurn)
    {
        if (targetData == null)
            return false;

        return TrySelectWeaponForTarget(targetData, currentPrimaryAmmo, hasMovedThisTurn, out _, out _);
    }

    public bool TrySelectWeaponForTarget(UnitData targetData, int currentPrimaryAmmo, out UnitWeaponData selectedWeapon, out bool usePrimary)
    {
        return TrySelectWeaponForTarget(targetData, currentPrimaryAmmo, hasMovedThisTurn: false, out selectedWeapon, out usePrimary);
    }

    public bool TrySelectWeaponForTarget(UnitData targetData, int currentPrimaryAmmo, bool hasMovedThisTurn, out UnitWeaponData selectedWeapon, out bool usePrimary)
    {
        selectedWeapon = null;
        usePrimary = false;
        if (targetData == null)
            return false;

        var hasPrimaryAmmo = currentPrimaryAmmo > 0;
        if (HasPrimaryWeapon &&
            hasPrimaryAmmo &&
            (!hasMovedThisTurn || !primaryWeapon.requiresStationaryToAttack) &&
            primaryWeapon.CanAttackCategory(targetData.category))
        {
            selectedWeapon = primaryWeapon;
            usePrimary = true;
            return true;
        }

        if (HasSecondaryWeapon &&
            (!hasMovedThisTurn || !secondaryWeapon.requiresStationaryToAttack) &&
            secondaryWeapon.CanAttackCategory(targetData.category))
        {
            selectedWeapon = secondaryWeapon;
            usePrimary = false;
            return true;
        }

        return false;
    }

    public bool CanAttackAtDistance(int distance, int currentPrimaryAmmo)
    {
        return CanAttackAtDistance(distance, currentPrimaryAmmo, hasMovedThisTurn: false);
    }

    public bool CanAttackAtDistance(int distance, int currentPrimaryAmmo, bool hasMovedThisTurn)
    {
        return GetAvailableWeaponAtDistance(distance, currentPrimaryAmmo, hasMovedThisTurn) != null;
    }

    public int GetAvailableAttackRangeMax(int currentPrimaryAmmo)
    {
        return GetAvailableAttackRangeMax(currentPrimaryAmmo, hasMovedThisTurn: false);
    }

    public int GetAvailableAttackRangeMax(int currentPrimaryAmmo, bool hasMovedThisTurn)
    {
        var max = 0;
        if (HasPrimaryWeapon &&
            currentPrimaryAmmo > 0 &&
            (!hasMovedThisTurn || !primaryWeapon.requiresStationaryToAttack))
            max = Mathf.Max(max, Mathf.Max(1, primaryWeapon.attackRangeMax));
        if (HasSecondaryWeapon &&
            (!hasMovedThisTurn || !secondaryWeapon.requiresStationaryToAttack))
            max = Mathf.Max(max, Mathf.Max(1, secondaryWeapon.attackRangeMax));
        return max;
    }

    private UnitWeaponData GetAvailableWeaponAtDistance(int distance, int currentPrimaryAmmo)
    {
        return GetAvailableWeaponAtDistance(distance, currentPrimaryAmmo, hasMovedThisTurn: false);
    }

    private UnitWeaponData GetAvailableWeaponAtDistance(int distance, int currentPrimaryAmmo, bool hasMovedThisTurn)
    {
        if (distance < 1)
            return null;

        if (HasPrimaryWeapon &&
            currentPrimaryAmmo > 0 &&
            (!hasMovedThisTurn || !primaryWeapon.requiresStationaryToAttack) &&
            primaryWeapon.IsDistanceInRange(distance))
            return primaryWeapon;
        if (HasSecondaryWeapon &&
            (!hasMovedThisTurn || !secondaryWeapon.requiresStationaryToAttack) &&
            secondaryWeapon.IsDistanceInRange(distance))
            return secondaryWeapon;
        return null;
    }

    private void OnValidate()
    {
        MigrateLegacyFieldsIfNeeded();
        primaryAmmoCapacity = Mathf.Max(0, primaryAmmoCapacity);
        if (primaryWeapon == null)
            primaryWeapon = new UnitWeaponData();
        if (secondaryWeapon == null)
            secondaryWeapon = new UnitWeaponData();

        primaryWeapon.ClampRange();
        secondaryWeapon.ClampRange();
    }

    private void OnEnable()
    {
        MigrateLegacyFieldsIfNeeded();
    }

    private void MigrateLegacyFieldsIfNeeded()
    {
        if (_legacyMigrated)
            return;

        if (primaryWeapon == null)
            primaryWeapon = new UnitWeaponData();
        if (secondaryWeapon == null)
            secondaryWeapon = new UnitWeaponData();

        hasPrimaryWeapon = canAttack;
        hasSecondaryWeapon = canAttack;

        primaryWeapon.baseDamage = primaryWeaponDamage;
        secondaryWeapon.baseDamage = secondaryWeaponDamage;
        primaryWeapon.attackRangeMin = attackRangeMin;
        primaryWeapon.attackRangeMax = attackRangeMax;
        secondaryWeapon.attackRangeMin = attackRangeMin;
        secondaryWeapon.attackRangeMax = attackRangeMax;

        if (primaryWeapon.damageMatrix == null || primaryWeapon.damageMatrix.Count == 0)
            primaryWeapon.damageMatrix = CloneDamageMatrix(primaryDamageMatrix);
        if (secondaryWeapon.damageMatrix == null || secondaryWeapon.damageMatrix.Count == 0)
            secondaryWeapon.damageMatrix = CloneDamageMatrix(secondaryDamageMatrix);

        if (!canAttack)
            primaryAmmoCapacity = 0;

        _legacyMigrated = true;
    }

    private static List<DamageMatrixEntry> CloneDamageMatrix(List<DamageMatrixEntry> source)
    {
        var result = new List<DamageMatrixEntry>();
        if (source == null)
            return result;

        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (entry == null)
                continue;

            result.Add(new DamageMatrixEntry
            {
                targetUnitId = entry.targetUnitId,
                damagePercent = entry.damagePercent
            });
        }

        return result;
    }
}

/// <summary>
/// 移动类型，影响地形移动消耗。
/// </summary>
public enum MovementType
{
    Foot,
    Wheeled,
    Treads,
    Mech
}

public enum UnitCategory
{
    Vehicle = 0,
    Soldier = 1
}

public enum UnitAIRoleTag
{
    Auto = 0,
    AssaultMain = 1,
    AssaultSupport = 2,
    CaptureTeam = 3,
    LogisticsTransport = 4,
    LogisticsSupply = 5,
    RangedStrike = 6
}

[Serializable]
public class UnitWeaponData
{
    [Tooltip("武器名称，仅用于展示和调试。")]
    public string weaponName = "Weapon";

    [Tooltip("武器基础伤害。最终伤害会叠加伤害矩阵和地形减伤计算。")]
    public int baseDamage = 10;

    [Tooltip("攻击最小距离（按曼哈顿距离计算）。")]
    [Min(1)]
    public int attackRangeMin = 1;

    [Tooltip("攻击最大距离（按曼哈顿距离计算）。")]
    [Min(1)]
    public int attackRangeMax = 1;

    [Tooltip("是否允许攻击 Vehicle。")]
    public bool canAttackVehicle = true;

    [Tooltip("是否允许攻击 Soldier。")]
    public bool canAttackSoldier = true;

    [Tooltip("是否需要在本回合未移动时才能开火（移动后不可使用该武器攻击）。")]
    public bool requiresStationaryToAttack = false;

    [Tooltip("伤害矩阵：目标单位 id -> 伤害系数（百分比）。")]
    public List<DamageMatrixEntry> damageMatrix = new();

    public bool CanAttackCategory(UnitCategory category)
    {
        return category == UnitCategory.Vehicle ? canAttackVehicle : canAttackSoldier;
    }

    public bool IsDistanceInRange(int distance)
    {
        var min = Mathf.Max(1, attackRangeMin);
        var max = Mathf.Max(min, attackRangeMax);
        return distance >= min && distance <= max;
    }

    public int GetDamagePercent(string targetUnitId)
    {
        if (damageMatrix == null) return 0;
        foreach (var entry in damageMatrix)
        {
            if (string.Equals(entry.targetUnitId, targetUnitId, StringComparison.OrdinalIgnoreCase))
                return entry.damagePercent;
        }
        return 0;
    }

    public void ClampRange()
    {
        attackRangeMin = Mathf.Max(1, attackRangeMin);
        attackRangeMax = Mathf.Max(attackRangeMin, attackRangeMax);
    }
}

/// <summary>
/// 伤害矩阵条目：目标单位 id -> 伤害系数（百分比）。
/// </summary>
[Serializable]
public class DamageMatrixEntry
{
    public string targetUnitId;
    [Range(0, 999)]
    public int damagePercent;
}
