using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;
using OperationMarigold.AI.Minimax;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 评估战场态势，将单位按角色分类写入 Blackboard。
    /// </summary>
    public class EvaluateBattlefieldNode : BTNode
    {
        protected override NodeState OnUpdate()
        {
            var board = Board.GetRef<AIBoardState>(BlackboardKeys.BoardState);
            if (board == null) return NodeState.Failure;

            int ourFaction = Board.GetInt(BlackboardKeys.OurFaction);
            int enemyFaction = Board.GetInt(BlackboardKeys.EnemyFaction);

            var combatUnits = new List<int>();
            var captureUnits = new List<int>();
            var idleUnits = new List<int>();
            var assaultMainUnits = new List<int>();
            var assaultSupportUnits = new List<int>();
            var captureTeamUnits = new List<int>();
            var logisticsUnits = new List<int>();
            var rangedStrikeUnits = new List<int>();

            float ourForce = 0f;
            float enemyForce = 0f;
            int ourCount = 0;
            int enemyCount = 0;
            int ourFactories = 0;
            int enemyFactories = 0;
            int uncapturedFactories = 0;

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if (!b.isFactory)
                    continue;
                if ((int)b.ownerFaction == ourFaction)
                    ourFactories++;
                else if ((int)b.ownerFaction == enemyFaction)
                    enemyFactories++;
                if ((int)b.ownerFaction != ourFaction)
                    uncapturedFactories++;
            }

            bool prioritizeFactoryCapture = uncapturedFactories > 0 && (ourFactories < 2 || ourFactories < enemyFactories);

            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive) continue;

                float force = (u.hp / (float)Mathf.Max(1, u.maxHp)) * u.cost;

                if ((int)u.faction == ourFaction)
                {
                    ourForce += force;
                    ourCount++;

                    if (u.hasActed || !u.IsOnMap)
                        continue;

                    bool canFight = u.hasPrimaryWeapon || u.hasSecondaryWeapon;
                    var caps = AIUnitRoleClassifier.GetCapabilitiesFromSnapshot(u);

                    bool canCaptureNow =
                        (caps & (AIUnitRoleCapabilities.CaptureTeam | AIUnitRoleCapabilities.AssaultSupport)) != 0 &&
                        u.category == UnitCategory.Soldier &&
                        (HasCaptureTarget(board, u, ourFaction, prioritizeFactoryCapture) ||
                         HasAdjacentTransportWithRoom(board, u, ourFaction));

                    bool logisticsNeeded = NeedsLogisticsSearch(board, u, i, ourFaction);
                    bool canLogistics = (caps & (AIUnitRoleCapabilities.TransportLogistics | AIUnitRoleCapabilities.SupplyLogistics)) != 0;

                    bool nearbyEnemy = canFight && HasNearbyEnemy(board, u, ourFaction);
                    bool nearEnemyBuilding = HasEnemyBuildingNearby(board, u, ourFaction);

                    // 角色归属优先级：
                    // 占领/辅助互转（步兵能力多重）→ 远程 → 后勤（补给/运输）→ 主力 → 其他空闲
                    bool assigned = false;

                    if (!assigned && canCaptureNow &&
                        (caps & AIUnitRoleCapabilities.CaptureTeam) != 0)
                    {
                        captureTeamUnits.Add(i);
                        captureUnits.Add(i);
                        assigned = true;
                    }

                    if (!assigned && (caps & AIUnitRoleCapabilities.AssaultSupport) != 0)
                    {
                        // 没有占领任务时，步兵回到辅助进攻；若附近没有交战信号，则交给空闲模块重定位
                        if (canFight && (nearbyEnemy || nearEnemyBuilding))
                        {
                            assaultSupportUnits.Add(i);
                            combatUnits.Add(i);
                        }
                        else
                        {
                            idleUnits.Add(i);
                        }
                        assigned = true;
                    }

                    if (!assigned && (caps & AIUnitRoleCapabilities.RangedStrike) != 0)
                    {
                        if (canFight && nearbyEnemy)
                        {
                            rangedStrikeUnits.Add(i);
                            combatUnits.Add(i);
                        }
                        else
                        {
                            idleUnits.Add(i);
                        }
                        assigned = true;
                    }

                    if (!assigned && canLogistics)
                    {
                        if (logisticsNeeded)
                        {
                            logisticsUnits.Add(i);
                            combatUnits.Add(i);
                        }
                        else
                        {
                            idleUnits.Add(i);
                        }
                        assigned = true;
                    }

                    if (!assigned && (caps & AIUnitRoleCapabilities.AssaultMain) != 0)
                    {
                        if (canFight && (nearbyEnemy || nearEnemyBuilding))
                        {
                            assaultMainUnits.Add(i);
                            combatUnits.Add(i);
                        }
                        else
                        {
                            idleUnits.Add(i);
                        }
                        assigned = true;
                    }
                }
                else if ((int)u.faction == enemyFaction)
                {
                    if (u.IsOnMap)
                    {
                        enemyForce += force;
                        enemyCount++;
                    }
                }
            }

            AITrace.LogVerbose($"[EvaluateBattlefield] Our units: {ourCount} (combat={combatUnits.Count}, capture={captureUnits.Count}, idle={idleUnits.Count}), enemy: {enemyCount}");

            Board.SetRef(BlackboardKeys.CombatUnits, combatUnits);
            Board.SetRef(BlackboardKeys.CaptureUnits, captureUnits);
            Board.SetRef(BlackboardKeys.IdleUnits, idleUnits);
            Board.SetRef(BlackboardKeys.AssaultMainUnits, assaultMainUnits);
            Board.SetRef(BlackboardKeys.AssaultSupportUnits, assaultSupportUnits);
            Board.SetRef(BlackboardKeys.CaptureTeamUnits, captureTeamUnits);
            Board.SetRef(BlackboardKeys.LogisticsUnits, logisticsUnits);
            Board.SetRef(BlackboardKeys.RangedStrikeUnits, rangedStrikeUnits);
            Board.SetFloat(BlackboardKeys.BattlefieldAdvantage, ourForce - enemyForce);
            Board.SetInt(BlackboardKeys.OurUnitCount, ourCount);
            Board.SetInt(BlackboardKeys.EnemyUnitCount, enemyCount);

            // 建筑统计
            int ourBuildings = 0, enemyBuildings = 0;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                if ((int)board.buildings[i].ownerFaction == ourFaction) ourBuildings++;
                else if (board.buildings[i].ownerFaction != UnitFaction.None) enemyBuildings++;
            }
            Board.SetInt(BlackboardKeys.OurBuildingCount, ourBuildings);
            Board.SetInt(BlackboardKeys.EnemyBuildingCount, enemyBuildings);

            return NodeState.Success;
        }

        private static bool HasCaptureTarget(AIBoardState board, AIUnitSnapshot unit, int ourFaction, bool prioritizeFactoryCapture)
        {
            int bestFactoryDist = int.MaxValue;
            int bestGeneralDist = int.MaxValue;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction == ourFaction) continue;
                int dist = board.ManhattanDistance(unit.gridCoord, b.gridCoord);
                if (b.isFactory)
                    bestFactoryDist = Mathf.Min(bestFactoryDist, dist);
                bestGeneralDist = Mathf.Min(bestGeneralDist, dist);
            }

            if (prioritizeFactoryCapture && bestFactoryDist <= unit.movementRange + 4)
                return true;
            return bestGeneralDist <= unit.movementRange + 2;
        }

        private static bool HasNearbyEnemy(AIBoardState board, AIUnitSnapshot unit, int ourFaction)
        {
            int scanRange = unit.movementRange + Mathf.Max(unit.primaryRangeMax, unit.secondaryRangeMax) + 2;
            for (int i = 0; i < board.units.Count; i++)
            {
                var e = board.units[i];
                if (!e.alive || !e.IsOnMap || (int)e.faction == ourFaction) continue;
                int dist = board.ManhattanDistance(unit.gridCoord, e.gridCoord);
                if (dist <= scanRange) return true;
            }
            return false;
        }

        private static bool HasEnemyBuildingNearby(AIBoardState board, AIUnitSnapshot unit, int ourFaction)
        {
            int maxDistance = unit.movementRange + 2;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction == ourFaction)
                    continue;
                if (board.ManhattanDistance(unit.gridCoord, b.gridCoord) <= maxDistance)
                    return true;
            }

            return false;
        }

        private static bool HasAdjacentTransportWithRoom(AIBoardState board, AIUnitSnapshot unit, int ourFaction)
        {
            if (!unit.IsLoadableInfantry)
                return false;
            return TryGetAdjacentTransporter(board, unit.gridCoord, ourFaction, out _, out _);
        }

        private static bool NeedsLogisticsSearch(AIBoardState board, AIUnitSnapshot u, int unitIdx, int ourFaction)
        {
            if (u.transportCapacity > 0)
            {
                if (board.CountEmbarkedCargo(unitIdx) > 0)
                    return true;
                if (HasAdjacentLoadableSoldier(board, u.gridCoord, ourFaction))
                    return true;
            }

            if (u.canSupply && HasNeighborNeedingSupply(board, u.gridCoord, ourFaction))
                return true;

            return false;
        }

        private static bool HasAdjacentLoadableSoldier(AIBoardState board, Vector2Int p, int ourFaction)
        {
            var dirs = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };
            for (int d = 0; d < dirs.Length; d++)
            {
                var n = p + dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                int ui = board.grid[n.x, n.y].unitIndex;
                if (ui < 0 || ui >= board.units.Count)
                    continue;
                var v = board.units[ui];
                if (!v.alive || (int)v.faction != ourFaction || v.hasActed || !v.IsOnMap)
                    continue;
                if (!v.IsLoadableInfantry)
                    continue;
                return true;
            }

            return false;
        }

        private static bool TryGetAdjacentTransporter(
            AIBoardState board, Vector2Int p, int ourFaction,
            out int transporterIdx, out AIUnitSnapshot transporter)
        {
            transporterIdx = -1;
            transporter = default;
            var dirs = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };
            for (int d = 0; d < dirs.Length; d++)
            {
                var n = p + dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                int ti = board.grid[n.x, n.y].unitIndex;
                if (ti < 0 || ti >= board.units.Count)
                    continue;
                var t = board.units[ti];
                if (!t.alive || (int)t.faction != ourFaction || t.transportCapacity <= 0)
                    continue;
                if (board.CountEmbarkedCargo(ti) >= t.transportCapacity)
                    continue;
                transporterIdx = ti;
                transporter = t;
                return true;
            }

            return false;
        }

        private static bool HasNeighborNeedingSupply(AIBoardState board, Vector2Int p, int ourFaction)
        {
            var dirs = new Vector2Int[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1)
            };
            for (int d = 0; d < dirs.Length; d++)
            {
                var n = p + dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                int ui = board.grid[n.x, n.y].unitIndex;
                if (ui < 0 || ui >= board.units.Count)
                    continue;
                var ally = board.units[ui];
                if (!ally.alive || !ally.IsOnMap || (int)ally.faction != ourFaction)
                    continue;
                if (ally.fuel < ally.maxFuel * 0.72f)
                    return true;
                if (ally.hasPrimaryWeapon && ally.maxAmmo > 0 && ally.ammo < ally.maxAmmo * 0.65f)
                    return true;
            }

            return false;
        }
    }
}
