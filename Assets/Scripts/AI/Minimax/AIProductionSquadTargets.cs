using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.Minimax
{
    /// <summary>
    /// 根据地图与敌方编制推导各小队“目标人数”，用于生产打分平衡。
    /// </summary>
    public struct AIProductionSquadTargets
    {
        public int AssaultMain;
        public int AssaultSupport;
        public int CaptureTeam;
        public int RangedStrike;
        public int Transports;
        public int Suppliers;

        public static AIProductionSquadTargets Compute(
            AIBoardState board, int ourFaction, int enemyFaction, int uncapturedProperties)
        {
            int enemyVehicles = 0;
            int enemyRangedVehicles = 0;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != enemyFaction)
                    continue;
                if (u.category == UnitCategory.Vehicle)
                {
                    enemyVehicles++;
                    if (u.primaryRequiresStationary || u.primaryRangeMax >= 3)
                        enemyRangedVehicles++;
                }
            }

            int propertyScale = Mathf.Min(20, uncapturedProperties);
            int buildingScale = 0;
            int ourFactories = 0;
            int enemyFactories = 0;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if (b.ownerFaction != UnitFaction.None &&
                    (int)b.ownerFaction != ourFaction)
                {
                    buildingScale++;
                }

                if (!b.isFactory)
                    continue;

                if ((int)b.ownerFaction == ourFaction)
                    ourFactories++;
                else if ((int)b.ownerFaction == enemyFaction)
                    enemyFactories++;
            }

            bool factoryShortage = ourFactories < 2 || ourFactories < enemyFactories;
            int captureBoost = factoryShortage ? 2 : 0;

            return new AIProductionSquadTargets
            {
                AssaultMain = Mathf.Clamp(3 + enemyVehicles + buildingScale / 5, 3, 20),
                AssaultSupport = Mathf.Clamp(2 + propertyScale / 5 + (factoryShortage ? 1 : 0), 2, 12),
                CaptureTeam = Mathf.Clamp(1 + propertyScale / 4 + captureBoost, 2, 14),
                RangedStrike = Mathf.Clamp(1 + enemyRangedVehicles + Mathf.Max(0, enemyVehicles - 2) / 2, 1, 12),
                Transports = Mathf.Clamp(1 + (uncapturedProperties > 4 ? 1 : 0) + (factoryShortage ? 1 : 0), 1, 6),
                Suppliers = Mathf.Clamp(1 + (enemyVehicles > 3 ? 1 : 0) + (buildingScale > 10 ? 1 : 0), 1, 5)
            };
        }
    }

    /// <summary>
    /// 当前己方场上编制（快照），随规划过程中“虚拟造兵”递增。
    /// </summary>
    public sealed class AIProductionRosterState
    {
        public int AssaultMain;
        public int AssaultSupport;
        public int CaptureTeam;
        public int RangedStrike;
        public int Transport;
        public int Supply;
        public readonly Dictionary<string, int> UnitTypeCounts = new Dictionary<string, int>();

        public static AIProductionRosterState FromBoard(AIBoardState board, int ourFaction)
        {
            var s = new AIProductionRosterState();
            int uncapturedProperties = 0;
            int ourFactories = 0;
            int enemyFactories = 0;

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction != ourFaction)
                    uncapturedProperties++;
                if (!b.isFactory)
                    continue;
                if ((int)b.ownerFaction == ourFaction)
                    ourFactories++;
                else if (b.ownerFaction != UnitFaction.None)
                    enemyFactories++;
            }

            bool prioritizeCapture = uncapturedProperties > 0 && (ourFactories < enemyFactories || ourFactories < 2);
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || (int)u.faction != ourFaction)
                    continue;

                s.RegisterUnitType(u.unitId);

                var caps = AIUnitRoleClassifier.GetCapabilitiesFromSnapshot(u);

                // Infantry：根据当前占领紧迫度选择占领队或辅助队（同一单位不能同时计入两个队，避免目标失真）
                bool hasCapture = (caps & AIUnitRoleCapabilities.CaptureTeam) != 0;
                bool hasSupport = (caps & AIUnitRoleCapabilities.AssaultSupport) != 0;
                if (hasCapture && hasSupport)
                {
                    bool hasCaptureTargetNow = HasCaptureTargetForRoster(board, u, ourFaction, prioritizeCapture);
                    if (hasCaptureTargetNow)
                        s.CaptureTeam++;
                    else
                        s.AssaultSupport++;
                    continue;
                }

                if (hasCapture)
                {
                    s.CaptureTeam++;
                    continue;
                }
                if (hasSupport)
                {
                    s.AssaultSupport++;
                    continue;
                }

                // Logistics：APC 既可补给又能运输时，优先按“当前后勤需求”计入后勤编制，
                // 避免同时拥有 vehicle 主战能力的单位被统计进主战队。
                bool hasLogistics =
                    (caps & (AIUnitRoleCapabilities.TransportLogistics | AIUnitRoleCapabilities.SupplyLogistics)) != 0;
                if (hasLogistics)
                {
                    bool supplyNeeded = IsSupplyNeeded(board, u, ourFaction);
                    bool transportNeeded = IsTransportNeeded(board, u, i, ourFaction);
                    if (supplyNeeded)
                    {
                        s.Supply++;
                        continue;
                    }
                    if (transportNeeded)
                    {
                        s.Transport++;
                        continue;
                    }
                }

                if ((caps & AIUnitRoleCapabilities.AssaultMain) != 0)
                {
                    s.AssaultMain++;
                    continue;
                }
                if ((caps & AIUnitRoleCapabilities.RangedStrike) != 0)
                {
                    s.RangedStrike++;
                    continue;
                }
            }

            return s;
        }

        public void RegisterIfProduced(UnitData data, AIUnitProductionRole filledRole)
        {
            if (data == null)
                return;

            RegisterUnitType(data.id);
            switch (filledRole)
            {
                case AIUnitProductionRole.AssaultMain:
                    AssaultMain++;
                    break;
                case AIUnitProductionRole.AssaultSupport:
                    AssaultSupport++;
                    break;
                case AIUnitProductionRole.CaptureTeam:
                    CaptureTeam++;
                    break;
                case AIUnitProductionRole.RangedStrike:
                    RangedStrike++;
                    break;
                case AIUnitProductionRole.TransportLogistics:
                    Transport++;
                    break;
                case AIUnitProductionRole.SupplyLogistics:
                    Supply++;
                    break;
                default:
                    // fallback: use capabilities
                    var caps = AIUnitRoleClassifier.GetCapabilitiesForProduction(data);
                    if ((caps & AIUnitRoleCapabilities.AssaultMain) != 0) AssaultMain++;
                    else if ((caps & AIUnitRoleCapabilities.AssaultSupport) != 0) AssaultSupport++;
                    break;
            }
        }

        public int GetUnitTypeCount(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                return 0;
            return UnitTypeCounts.TryGetValue(unitId, out int count) ? count : 0;
        }

        private void RegisterUnitType(string unitId)
        {
            if (string.IsNullOrEmpty(unitId))
                return;
            UnitTypeCounts.TryGetValue(unitId, out int count);
            UnitTypeCounts[unitId] = count + 1;
        }

        private static bool HasCaptureTargetForRoster(AIBoardState board, AIUnitSnapshot unit, int ourFaction, bool prioritizeFactoryCapture)
        {
            int bestFactoryDist = int.MaxValue;
            int bestGeneralDist = int.MaxValue;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction == ourFaction)
                    continue;
                int dist = board.ManhattanDistance(unit.gridCoord, b.gridCoord);
                if (b.isFactory)
                    bestFactoryDist = Mathf.Min(bestFactoryDist, dist);
                bestGeneralDist = Mathf.Min(bestGeneralDist, dist);
            }

            if (prioritizeFactoryCapture && bestFactoryDist != int.MaxValue &&
                bestFactoryDist <= unit.movementRange + 4)
                return true;

            return bestGeneralDist != int.MaxValue && bestGeneralDist <= unit.movementRange + 2;
        }

        private static bool IsSupplyNeeded(AIBoardState board, AIUnitSnapshot unit, int ourFaction)
        {
            if (!unit.canSupply)
                return false;

            var dirs = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int d = 0; d < dirs.Length; d++)
            {
                var n = unit.gridCoord + dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                int ui = board.grid[n.x, n.y].unitIndex;
                if (ui < 0 || ui >= board.units.Count)
                    continue;
                var ally = board.units[ui];
                if (!ally.alive || !ally.IsOnMap || (int)ally.faction != ourFaction)
                    continue;
                if (ally.embarkedOnUnitIndex >= 0)
                    continue;
                if (ally.fuel < ally.maxFuel * 0.72f)
                    return true;
                if (ally.hasPrimaryWeapon && ally.maxAmmo > 0 && ally.ammo < ally.maxAmmo * 0.65f)
                    return true;
            }

            return false;
        }

        private static bool IsTransportNeeded(AIBoardState board, AIUnitSnapshot unit, int unitIdx, int ourFaction)
        {
            if (unit.transportCapacity <= 0)
                return false;

            if (board.CountEmbarkedCargo(unitIdx) > 0)
                return true;

            var dirs = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };

            for (int d = 0; d < dirs.Length; d++)
            {
                var n = unit.gridCoord + dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                int ui = board.grid[n.x, n.y].unitIndex;
                if (ui < 0 || ui >= board.units.Count)
                    continue;
                var ally = board.units[ui];
                if (!ally.alive || (int)ally.faction != ourFaction)
                    continue;
                if (ally.hasActed || !ally.IsOnMap)
                    continue;
                if (!ally.IsLoadableInfantry)
                    continue;
                return true;
            }

            return false;
        }
    }
}
