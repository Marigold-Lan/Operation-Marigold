using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.Minimax
{
    internal struct AIRoleTypeBalanceStats
    {
        public float totalCount;
        public int options;
    }

    public sealed class AIProductionChoice
    {
        public UnitData unit;
        public AIUnitProductionRole filledRole;
    }

    /// <summary>
    /// 工厂生产评估：敌方反制、地产需求、小队编制平衡、存钱出高级兵种。
    /// </summary>
    public class AIProductionEvaluator
    {
        public UnitData EvaluateBestUnit(
            AIBoardState board,
            FactorySpawner factory,
            UnitFaction faction,
            int availableFunds,
            int reserveFunds,
            AIDifficultyProfile profile,
            AIStrategyContext strategy,
            AIProductionRosterState roster,
            AIProductionSquadTargets squadTargets)
        {
            var choice = EvaluateBestChoice(
                board, factory, faction, availableFunds, reserveFunds,
                profile, strategy, roster, squadTargets);
            return choice?.unit;
        }

        public AIProductionChoice EvaluateBestChoice(
            AIBoardState board,
            FactorySpawner factory,
            UnitFaction faction,
            int availableFunds,
            int reserveFunds,
            AIDifficultyProfile profile,
            AIStrategyContext strategy,
            AIProductionRosterState roster,
            AIProductionSquadTargets squadTargets)
        {
            var building = factory.Building;
            if (building == null || building.Data == null || building.Data.factoryBuildCatalog == null)
                return null;

            var catalog = building.Data.factoryBuildCatalog.GetBuildableUnits(faction);
            if (catalog == null || catalog.Count == 0)
                return null;

            int ourFaction = (int)faction;
            int enemyFaction = board.GetOpponentPlayerId(ourFaction);

            int enemyVehicles = 0, enemySoldiers = 0;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != enemyFaction) continue;
                if (u.category == UnitCategory.Vehicle) enemyVehicles++;
                else enemySoldiers++;
            }

            int uncapturedBuildings = 0;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                if ((int)board.buildings[i].ownerFaction != ourFaction)
                    uncapturedBuildings++;
            }

            int cheapestInCatalog = int.MaxValue;
            for (int i = 0; i < catalog.Count; i++)
            {
                var ud = catalog[i];
                if (ud != null && ud.cost > 0 && ud.cost < cheapestInCatalog)
                    cheapestInCatalog = ud.cost;
            }

            if (cheapestInCatalog == int.MaxValue)
                cheapestInCatalog = 1000;

            UnitData bestUnit = null;
            AIUnitProductionRole bestRole = AIUnitProductionRole.Generalist;
            float bestScore = float.MinValue;
            float efficiency = profile != null ? profile.productionEfficiency : 1f;
            var roleTypeStats = BuildRoleTypeStats(catalog, roster);

            for (int i = 0; i < catalog.Count; i++)
            {
                var unitData = catalog[i];
                if (unitData == null || unitData.cost > availableFunds)
                    continue;

                var caps = AIUnitRoleClassifier.GetCapabilitiesForProduction(unitData);
                bool hasAnyCap = false;
                float localBestScore = float.MinValue;
                AIUnitProductionRole localBestRole = AIUnitProductionRole.Generalist;

                foreach (var role in AIUnitRoleClassifier.EnumerateRoles(caps))
                {
                    hasAnyCap = true;
                    float score = ScoreUnitForRole(
                        unitData,
                        role,
                        enemyVehicles,
                        enemySoldiers,
                        uncapturedBuildings,
                        availableFunds,
                        reserveFunds,
                        cheapestInCatalog,
                        efficiency,
                        strategy,
                        roster,
                        squadTargets,
                        roster.GetUnitTypeCount(unitData.id),
                        GetRoleTypeAverageCount(roleTypeStats, role));

                    if (score > localBestScore)
                    {
                        localBestScore = score;
                        localBestRole = role;
                    }
                }

                if (!hasAnyCap)
                    continue;

                if (localBestScore > bestScore)
                {
                    bestScore = localBestScore;
                    bestUnit = unitData;
                    bestRole = localBestRole;
                }
            }

            if (bestUnit == null)
                return null;

            return new AIProductionChoice
            {
                unit = bestUnit,
                filledRole = bestRole
            };
        }

        private static Dictionary<AIUnitProductionRole, AIRoleTypeBalanceStats> BuildRoleTypeStats(
            List<UnitData> catalog, AIProductionRosterState roster)
        {
            var stats = new Dictionary<AIUnitProductionRole, AIRoleTypeBalanceStats>();
            if (catalog == null || roster == null)
                return stats;

            for (int i = 0; i < catalog.Count; i++)
            {
                var data = catalog[i];
                if (data == null)
                    continue;

                var caps = AIUnitRoleClassifier.GetCapabilitiesForProduction(data);
                foreach (var role in AIUnitRoleClassifier.EnumerateRoles(caps))
                {
                    stats.TryGetValue(role, out AIRoleTypeBalanceStats current);
                    current.options += 1;
                    current.totalCount += roster.GetUnitTypeCount(data.id);
                    stats[role] = current;
                }
            }

            return stats;
        }

        private static float GetRoleTypeAverageCount(
            Dictionary<AIUnitProductionRole, AIRoleTypeBalanceStats> stats, AIUnitProductionRole role)
        {
            if (stats == null || !stats.TryGetValue(role, out AIRoleTypeBalanceStats v))
                return 0f;
            if (v.options <= 0)
                return 0f;
            return v.totalCount / v.options;
        }

        private static float ScoreUnitForRole(
            UnitData data,
            AIUnitProductionRole role,
            int enemyVehicles,
            int enemySoldiers,
            int uncapturedBuildings,
            int availableFunds,
            int reserveFunds,
            int cheapestInCatalog,
            float efficiency,
            AIStrategyContext strategy,
            AIProductionRosterState roster,
            AIProductionSquadTargets targets,
            int unitTypeCount,
            float roleAverageTypeCount)
        {
            roster ??= new AIProductionRosterState();
            var strat = strategy ?? AIStrategyContext.Neutral;
            float score = 0f;

            score += data.maxHp * 10f;

            if (data.HasAnyWeapon)
            {
                bool canAntiVehicle = (data.HasPrimaryWeapon && data.primaryWeapon.canAttackVehicle) ||
                                      (data.HasSecondaryWeapon && data.secondaryWeapon.canAttackVehicle);
                bool canAntiSoldier = (data.HasPrimaryWeapon && data.primaryWeapon.canAttackSoldier) ||
                                      (data.HasSecondaryWeapon && data.secondaryWeapon.canAttackSoldier);

                if (canAntiVehicle && enemyVehicles > 0)
                    score += enemyVehicles * 32f;
                if (canAntiSoldier && enemySoldiers > 0)
                    score += enemySoldiers * 18f;

                int maxDamage = 0;
                if (data.HasPrimaryWeapon) maxDamage = Mathf.Max(maxDamage, data.primaryWeapon.baseDamage);
                if (data.HasSecondaryWeapon) maxDamage = Mathf.Max(maxDamage, data.secondaryWeapon.baseDamage);
                score += maxDamage * 2.2f;
            }

            if (role == AIUnitProductionRole.CaptureTeam && uncapturedBuildings > 0)
            {
                // 占领队的核心目标是“补编制缺口”，
                // 但地图建筑越多，线性叠加会导致持续过量产出。这里对地图紧迫度做封顶，
                // 让角色产线更均衡。
                int captureDeficit = Mathf.Max(0, targets.CaptureTeam - roster.CaptureTeam);
                if (captureDeficit > 0)
                    score += captureDeficit * 95f;

                float cappedUncaptured = Mathf.Min(uncapturedBuildings, 20);
                score += cappedUncaptured * 10f * strat.ProductionSoldierMul;

                if (roster.CaptureTeam >= targets.CaptureTeam)
                {
                    int captureOver = roster.CaptureTeam - targets.CaptureTeam;
                    // 超出目标后指数衰减，避免步兵在后续回合长期领先其他角色。
                    float overMul = Mathf.Max(0.22f, Mathf.Pow(0.65f, captureOver));
                    score *= overMul;
                }
            }

            if (role == AIUnitProductionRole.AssaultSupport)
            {
                // 占领/辅助互转能力较强，这里同样避免“未占领建筑数量”线性拉偏。
                int supportDeficit = Mathf.Max(0, targets.AssaultSupport - roster.AssaultSupport);
                if (supportDeficit > 0)
                    score += supportDeficit * 60f;

                float cappedUncaptured = Mathf.Min(uncapturedBuildings, 20);
                score += cappedUncaptured * 5f * strat.ProductionSoldierMul;

                if (roster.AssaultSupport > targets.AssaultSupport + 1)
                {
                    int over = roster.AssaultSupport - targets.AssaultSupport;
                    float overMul = Mathf.Max(0.44f, Mathf.Pow(0.7f, over));
                    score *= overMul;
                }
            }

            if (role == AIUnitProductionRole.AssaultMain || role == AIUnitProductionRole.RangedStrike)
            {
                score *= strat.ProductionVehicleMul;
                int have = role == AIUnitProductionRole.RangedStrike ? roster.RangedStrike : roster.AssaultMain;
                int need = role == AIUnitProductionRole.RangedStrike ? targets.RangedStrike : targets.AssaultMain;
                if (have < need)
                    score += (need - have) * 70f;
                else if (have > need)
                    score *= 0.5f;
                score += data.cost * 0.026f;
            }

            if (role == AIUnitProductionRole.TransportLogistics)
            {
                if (roster.Transport < targets.Transports)
                    score += (targets.Transports - roster.Transport) * 120f + uncapturedBuildings * 6f;
                else
                    score *= 0.45f;
            }

            if (role == AIUnitProductionRole.SupplyLogistics)
            {
                if (roster.Supply < targets.Suppliers)
                    score += (targets.Suppliers - roster.Supply) * 100f;
                else
                    score *= 0.5f;
            }

            score += data.movementRange * 5f;

            bool savingForElite = availableFunds >= cheapestInCatalog * 4 &&
                                  (roster.AssaultMain < targets.AssaultMain || roster.RangedStrike < targets.RangedStrike);
            if (savingForElite && data.cost <= cheapestInCatalog * 1.15f &&
                (role == AIUnitProductionRole.CaptureTeam || role == AIUnitProductionRole.AssaultSupport || data.cost < 2000))
                score *= 0.42f;

            int fundsAfterPurchase = availableFunds - data.cost;
            if (fundsAfterPurchase < reserveFunds)
            {
                bool emergencyRole = (role == AIUnitProductionRole.CaptureTeam && roster.CaptureTeam < targets.CaptureTeam) ||
                                     (role == AIUnitProductionRole.TransportLogistics && roster.Transport < targets.Transports) ||
                                     (role == AIUnitProductionRole.SupplyLogistics && roster.Supply < targets.Suppliers);
                score *= emergencyRole ? 0.68f : 0.2f;
            }

            int surplus = Mathf.Max(0, availableFunds - reserveFunds);
            if (surplus > 0 &&
                (role == AIUnitProductionRole.AssaultMain || role == AIUnitProductionRole.RangedStrike))
            {
                float expensiveBias = Mathf.Clamp(surplus / 4000f, 0f, 1.2f);
                score += data.cost * (0.03f + expensiveBias * 0.012f);
            }

            float costEfficiency = (score / Mathf.Max(1, data.cost)) * 1000f;
            float cheapWeight = Mathf.Clamp01(0.16f + strat.ProductionCheapBias * 0.35f);
            score = score * (1f - cheapWeight) + costEfficiency * cheapWeight;

            score *= efficiency;

            // 若该单位本身具备补给/运输能力，但当前后勤编制仍缺口，
            // 则压低其作为主战/远程打击角色的价值，避免 APC/补给车被误当成主力。
            var caps = AIUnitRoleClassifier.GetCapabilitiesForProduction(data);
            bool hasLogistics =
                (caps & (AIUnitRoleCapabilities.TransportLogistics | AIUnitRoleCapabilities.SupplyLogistics)) != 0;
            bool logisticsDeficit = roster.Transport < targets.Transports || roster.Supply < targets.Suppliers;
            if (hasLogistics && logisticsDeficit &&
                (role == AIUnitProductionRole.AssaultMain || role == AIUnitProductionRole.RangedStrike))
            {
                score *= 0.6f;
            }

            // 限制 mech 在步兵角色的过量产出：步兵角色能赢也要更均衡。
            if ((role == AIUnitProductionRole.CaptureTeam || role == AIUnitProductionRole.AssaultSupport) &&
                data.movementType == MovementType.Mech)
            {
                score *= 0.82f;
            }

            // 角色内兵种软均衡：同角色中数量过多的 unitId 略降权，稀缺类型小幅补偿。
            float typeDelta = unitTypeCount - roleAverageTypeCount;
            if (typeDelta > 0.5f)
            {
                float mul = Mathf.Clamp(1f - typeDelta * 0.12f, 0.62f, 1f);
                score *= mul;
            }
            else if (typeDelta < -0.5f)
            {
                float bonus = Mathf.Min(24f, Mathf.Abs(typeDelta) * 10f);
                score += bonus;
            }

            return score;
        }
    }
}
