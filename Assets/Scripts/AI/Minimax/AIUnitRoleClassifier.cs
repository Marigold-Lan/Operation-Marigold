using System.Collections.Generic;
using OperationMarigold.AI.Simulation;
using UnityEngine;

namespace OperationMarigold.AI.Minimax
{
    [System.Flags]
    public enum AIUnitRoleCapabilities
    {
        None = 0,
        AssaultMain = 1 << 0,
        AssaultSupport = 1 << 1,
        CaptureTeam = 1 << 2,
        TransportLogistics = 1 << 3,
        SupplyLogistics = 1 << 4,
        RangedStrike = 1 << 5
    }

    /// <summary>
    /// 将兵种归类到 AI 小队角色（生产与编制平衡用）。
    /// </summary>
    public enum AIUnitProductionRole
    {
        AssaultMain,
        AssaultSupport,
        CaptureTeam,
        RangedStrike,
        TransportLogistics,
        SupplyLogistics,
        Generalist
    }

    public static class AIUnitRoleClassifier
    {
        public static AIUnitRoleCapabilities GetCapabilitiesForProduction(UnitData d)
        {
            if (d == null)
                return AIUnitRoleCapabilities.None;

            if (d.aiRoleOverride != UnitAIRoleTag.Auto)
                return CapabilityFromRole(ClassifyFromOverride(d.aiRoleOverride));

            var caps = AIUnitRoleCapabilities.None;

            if (d.prefab != null)
            {
                if (d.prefab.GetComponent<ITransporter>() != null)
                    caps |= AIUnitRoleCapabilities.TransportLogistics;
                if (d.prefab.GetComponent<ISupplier>() != null)
                    caps |= AIUnitRoleCapabilities.SupplyLogistics;
            }

            if (d.category == UnitCategory.Soldier &&
                (d.movementType == MovementType.Foot || d.movementType == MovementType.Mech))
            {
                // 步兵/机甲既能占领也能辅助进攻（由战场紧迫度决定具体归属）
                caps |= AIUnitRoleCapabilities.CaptureTeam;
                caps |= AIUnitRoleCapabilities.AssaultSupport;
            }

            if (d.category == UnitCategory.Vehicle)
            {
                int ammo = d.MaxPrimaryAmmo;
                int maxR = d.GetAvailableAttackRangeMax(ammo);
                bool stationary = d.HasPrimaryWeapon && d.primaryWeapon.requiresStationaryToAttack;
                if (maxR >= 3 || stationary)
                    caps |= AIUnitRoleCapabilities.RangedStrike;
                else
                    caps |= AIUnitRoleCapabilities.AssaultMain;
            }

            return caps;
        }

        public static AIUnitRoleCapabilities GetCapabilitiesFromSnapshot(in AIUnitSnapshot u)
        {
            if (!u.alive)
                return AIUnitRoleCapabilities.None;

            var caps = AIUnitRoleCapabilities.None;

            if (u.transportCapacity > 0)
                caps |= AIUnitRoleCapabilities.TransportLogistics;
            if (u.canSupply)
                caps |= AIUnitRoleCapabilities.SupplyLogistics;

            if (u.IsLoadableInfantry)
            {
                caps |= AIUnitRoleCapabilities.CaptureTeam;
                caps |= AIUnitRoleCapabilities.AssaultSupport;
            }

            if (u.category == UnitCategory.Vehicle)
            {
                if (u.primaryRequiresStationary || u.primaryRangeMax >= 3)
                    caps |= AIUnitRoleCapabilities.RangedStrike;
                else
                    caps |= AIUnitRoleCapabilities.AssaultMain;
            }

            return caps;
        }

        public static bool HasCapability(AIUnitRoleCapabilities caps, AIUnitProductionRole role)
        {
            return role switch
            {
                AIUnitProductionRole.AssaultMain => (caps & AIUnitRoleCapabilities.AssaultMain) != 0,
                AIUnitProductionRole.AssaultSupport => (caps & AIUnitRoleCapabilities.AssaultSupport) != 0,
                AIUnitProductionRole.CaptureTeam => (caps & AIUnitRoleCapabilities.CaptureTeam) != 0,
                AIUnitProductionRole.RangedStrike => (caps & AIUnitRoleCapabilities.RangedStrike) != 0,
                AIUnitProductionRole.TransportLogistics => (caps & AIUnitRoleCapabilities.TransportLogistics) != 0,
                AIUnitProductionRole.SupplyLogistics => (caps & AIUnitRoleCapabilities.SupplyLogistics) != 0,
                _ => false
            };
        }

        public static IEnumerable<AIUnitProductionRole> EnumerateRoles(AIUnitRoleCapabilities caps)
        {
            if ((caps & AIUnitRoleCapabilities.AssaultMain) != 0) yield return AIUnitProductionRole.AssaultMain;
            if ((caps & AIUnitRoleCapabilities.AssaultSupport) != 0) yield return AIUnitProductionRole.AssaultSupport;
            if ((caps & AIUnitRoleCapabilities.CaptureTeam) != 0) yield return AIUnitProductionRole.CaptureTeam;
            if ((caps & AIUnitRoleCapabilities.RangedStrike) != 0) yield return AIUnitProductionRole.RangedStrike;
            if ((caps & AIUnitRoleCapabilities.TransportLogistics) != 0) yield return AIUnitProductionRole.TransportLogistics;
            if ((caps & AIUnitRoleCapabilities.SupplyLogistics) != 0) yield return AIUnitProductionRole.SupplyLogistics;
        }

        private static AIUnitProductionRole ClassifyFromOverride(UnitAIRoleTag tag)
        {
            return tag switch
            {
                UnitAIRoleTag.AssaultMain => AIUnitProductionRole.AssaultMain,
                UnitAIRoleTag.AssaultSupport => AIUnitProductionRole.AssaultSupport,
                UnitAIRoleTag.CaptureTeam => AIUnitProductionRole.CaptureTeam,
                UnitAIRoleTag.LogisticsTransport => AIUnitProductionRole.TransportLogistics,
                UnitAIRoleTag.LogisticsSupply => AIUnitProductionRole.SupplyLogistics,
                UnitAIRoleTag.RangedStrike => AIUnitProductionRole.RangedStrike,
                _ => AIUnitProductionRole.Generalist
            };
        }

        private static AIUnitRoleCapabilities CapabilityFromRole(AIUnitProductionRole role)
        {
            return role switch
            {
                AIUnitProductionRole.AssaultMain => AIUnitRoleCapabilities.AssaultMain,
                AIUnitProductionRole.AssaultSupport => AIUnitRoleCapabilities.AssaultSupport,
                AIUnitProductionRole.CaptureTeam => AIUnitRoleCapabilities.CaptureTeam,
                AIUnitProductionRole.RangedStrike => AIUnitRoleCapabilities.RangedStrike,
                AIUnitProductionRole.TransportLogistics => AIUnitRoleCapabilities.TransportLogistics,
                AIUnitProductionRole.SupplyLogistics => AIUnitRoleCapabilities.SupplyLogistics,
                _ => AIUnitRoleCapabilities.None
            };
        }

        public static AIUnitProductionRole ClassifyForProduction(UnitData d, bool prioritizeCapture = false)
        {
            var caps = GetCapabilitiesForProduction(d);
            if (prioritizeCapture)
            {
                if ((caps & AIUnitRoleCapabilities.CaptureTeam) != 0)
                    return AIUnitProductionRole.CaptureTeam;
            }
            if ((caps & AIUnitRoleCapabilities.AssaultSupport) != 0)
                return AIUnitProductionRole.AssaultSupport;
            if ((caps & AIUnitRoleCapabilities.AssaultMain) != 0)
                return AIUnitProductionRole.AssaultMain;
            if ((caps & AIUnitRoleCapabilities.RangedStrike) != 0)
                return AIUnitProductionRole.RangedStrike;
            if ((caps & AIUnitRoleCapabilities.TransportLogistics) != 0)
                return AIUnitProductionRole.TransportLogistics;
            if ((caps & AIUnitRoleCapabilities.SupplyLogistics) != 0)
                return AIUnitProductionRole.SupplyLogistics;
            return AIUnitProductionRole.Generalist;
        }

        public static AIUnitProductionRole ClassifyFromSnapshot(in AIUnitSnapshot u, bool prioritizeCapture)
        {
            var caps = GetCapabilitiesFromSnapshot(u);
            if (prioritizeCapture && (caps & AIUnitRoleCapabilities.CaptureTeam) != 0)
                return AIUnitProductionRole.CaptureTeam;
            if ((caps & AIUnitRoleCapabilities.AssaultSupport) != 0)
                return AIUnitProductionRole.AssaultSupport;
            if ((caps & AIUnitRoleCapabilities.AssaultMain) != 0)
                return AIUnitProductionRole.AssaultMain;
            if ((caps & AIUnitRoleCapabilities.RangedStrike) != 0)
                return AIUnitProductionRole.RangedStrike;
            if ((caps & AIUnitRoleCapabilities.TransportLogistics) != 0)
                return AIUnitProductionRole.TransportLogistics;
            if ((caps & AIUnitRoleCapabilities.SupplyLogistics) != 0)
                return AIUnitProductionRole.SupplyLogistics;
            return AIUnitProductionRole.Generalist;
        }
    }
}
