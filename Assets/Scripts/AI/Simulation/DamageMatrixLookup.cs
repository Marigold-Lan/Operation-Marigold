using System.Collections.Generic;

namespace OperationMarigold.AI.Simulation
{
    /// <summary>
    /// 共享的伤害矩阵查找表。
    /// 由 BoardSnapshotFactory 在拍快照时从 UnitData 中提取，整个搜索过程共享只读。
    /// key = attackerUnitId, value = { targetUnitId -> (primaryPercent, secondaryPercent) }
    /// </summary>
    public class DamageMatrixLookup
    {
        public struct WeaponPercents
        {
            public int primary;
            public int secondary;
        }

        private readonly Dictionary<string, Dictionary<string, WeaponPercents>> _table
            = new Dictionary<string, Dictionary<string, WeaponPercents>>();

        public void Set(string attackerId, string targetId, int primaryPercent, int secondaryPercent)
        {
            if (!_table.TryGetValue(attackerId, out var inner))
            {
                inner = new Dictionary<string, WeaponPercents>();
                _table[attackerId] = inner;
            }
            inner[targetId] = new WeaponPercents { primary = primaryPercent, secondary = secondaryPercent };
        }

        public int GetPercent(string attackerId, string targetId, bool usePrimary)
        {
            if (_table.TryGetValue(attackerId, out var inner))
            {
                if (inner.TryGetValue(targetId, out var wp))
                    return usePrimary ? wp.primary : wp.secondary;
            }
            return 0;
        }
    }
}
