using UnityEngine;

namespace OperationMarigold.AI.Simulation
{
    /// <summary>
    /// 单位轻量快照 (struct)，供 Minimax 搜索使用。
    /// 值类型保证 Clone 时自动深拷贝，零 GC。
    /// </summary>
    public struct AIUnitSnapshot
    {
        public string unitId;
        public Vector2Int gridCoord;
        public int hp;
        public int maxHp;
        public int fuel;
        public int maxFuel;
        public int ammo;
        public int maxAmmo;
        public UnitFaction faction;
        public bool hasActed;
        public bool hasMovedThisTurn;
        public int movementRange;
        public MovementType movementType;
        public UnitCategory category;
        public int cost;

        public bool hasPrimaryWeapon;
        public int primaryBaseDamage;
        public int primaryRangeMin;
        public int primaryRangeMax;
        public bool primaryCanAttackVehicle;
        public bool primaryCanAttackSoldier;
        public bool primaryRequiresStationary;

        public bool hasSecondaryWeapon;
        public int secondaryBaseDamage;
        public int secondaryRangeMin;
        public int secondaryRangeMax;
        public bool secondaryCanAttackVehicle;
        public bool secondaryCanAttackSoldier;
        public bool secondaryRequiresStationary;

        /// <summary>
        /// 伤害矩阵在纯 struct 中无法内联存储，
        /// 由 AIBoardState 持有共享的 DamageMatrixLookup 通过 unitId 查询。
        /// </summary>
        public bool alive;

        /// <summary>≥0 表示搭载在对应索引的运输单位上，不在格子上参与移动/攻击列表。</summary>
        public int embarkedOnUnitIndex;

        /// <summary>可装载容量，0 表示非运输单位。</summary>
        public int transportCapacity;

        /// <summary>可执行补给命令（游戏中带补给组件的单位）。</summary>
        public bool canSupply;

        public bool IsDead => !alive || hp <= 0;

        public bool IsOnMap => embarkedOnUnitIndex < 0;

        public bool IsLoadableInfantry =>
            alive && category == UnitCategory.Soldier &&
            (movementType == MovementType.Foot || movementType == MovementType.Mech);

        public bool CanAttackCategory(bool usePrimary, UnitCategory target)
        {
            if (usePrimary)
                return target == UnitCategory.Vehicle ? primaryCanAttackVehicle : primaryCanAttackSoldier;
            return target == UnitCategory.Vehicle ? secondaryCanAttackVehicle : secondaryCanAttackSoldier;
        }

        public bool IsDistanceInRange(bool usePrimary, int distance)
        {
            int min, max;
            if (usePrimary)
            {
                min = Mathf.Max(1, primaryRangeMin);
                max = Mathf.Max(min, primaryRangeMax);
            }
            else
            {
                min = Mathf.Max(1, secondaryRangeMin);
                max = Mathf.Max(min, secondaryRangeMax);
            }
            return distance >= min && distance <= max;
        }

        /// <summary>
        /// 选择可用武器。返回 true 表示找到了可用武器。
        /// </summary>
        public bool TrySelectWeapon(UnitCategory targetCategory, int distance, out bool usePrimary, out int baseDamage)
        {
            usePrimary = false;
            baseDamage = 0;

            if (hasPrimaryWeapon && ammo > 0 &&
                (!hasMovedThisTurn || !primaryRequiresStationary) &&
                CanAttackCategory(true, targetCategory) &&
                IsDistanceInRange(true, distance))
            {
                usePrimary = true;
                baseDamage = primaryBaseDamage;
                return true;
            }

            if (hasSecondaryWeapon &&
                (!hasMovedThisTurn || !secondaryRequiresStationary) &&
                CanAttackCategory(false, targetCategory) &&
                IsDistanceInRange(false, distance))
            {
                usePrimary = false;
                baseDamage = secondaryBaseDamage;
                return true;
            }

            return false;
        }

        public int GetMaxAttackRange()
        {
            int max = 0;
            if (hasPrimaryWeapon && ammo > 0 && (!hasMovedThisTurn || !primaryRequiresStationary))
                max = Mathf.Max(max, Mathf.Max(1, primaryRangeMax));
            if (hasSecondaryWeapon && (!hasMovedThisTurn || !secondaryRequiresStationary))
                max = Mathf.Max(max, Mathf.Max(1, secondaryRangeMax));
            return max;
        }
    }
}
