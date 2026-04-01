using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.Minimax
{
    /// <summary>
    /// 生产资金预留策略：在补齐核心编制前，尽量保留足够资金用于中高价单位。
    /// </summary>
    public static class AIProductionReservePolicy
    {
        public static int ComputeReserveFunds(
            AIBoardState board,
            int ourFaction,
            UnitFaction faction,
            List<FactorySpawner> factories,
            AIProductionRosterState roster,
            AIProductionSquadTargets targets,
            AIStrategyContext strategy)
        {
            int cheapest = int.MaxValue;
            int eliteRefCost = 0;
            int buildableCount = 0;
            int ownedFactories = 0;

            for (int i = 0; i < factories.Count; i++)
            {
                var spawner = factories[i];
                if (spawner == null || !spawner.CanSpawn(faction))
                    continue;

                ownedFactories++;
                var building = spawner.Building;
                if (building == null || building.Data == null || building.Data.factoryBuildCatalog == null)
                    continue;

                var catalog = building.Data.factoryBuildCatalog.GetBuildableUnits(faction);
                for (int j = 0; j < catalog.Count; j++)
                {
                    var data = catalog[j];
                    if (data == null || data.cost <= 0)
                        continue;
                    buildableCount++;
                    cheapest = Mathf.Min(cheapest, data.cost);
                    if (data.cost > eliteRefCost)
                        eliteRefCost = data.cost;
                }
            }

            if (buildableCount == 0)
                return 0;

            if (cheapest == int.MaxValue)
                cheapest = 1000;
            eliteRefCost = Mathf.Max(eliteRefCost, cheapest * 2);

            int reserve = 0;
            bool coreMissing = roster.AssaultMain < targets.AssaultMain || roster.RangedStrike < targets.RangedStrike;
            bool captureUrgent = roster.CaptureTeam < targets.CaptureTeam;
            bool transportUrgent = roster.Transport < targets.Transports;

            if (coreMissing)
                reserve = Mathf.Max(reserve, Mathf.RoundToInt(eliteRefCost * 0.7f));

            if (captureUrgent && (ownedFactories < 2 || transportUrgent))
                reserve = Mathf.Max(reserve, Mathf.RoundToInt(cheapest * 1.2f));
            else
                reserve = Mathf.Max(reserve, cheapest);

            var strat = strategy ?? AIStrategyContext.Neutral;
            reserve = Mathf.RoundToInt(reserve * Mathf.Clamp(strat.ProductionReserveMul, 0.75f, 1.6f));

            int ourFunds = ourFaction >= 0 && ourFaction < board.funds.Length ? board.funds[ourFaction] : 0;
            reserve = Mathf.Clamp(reserve, 0, Mathf.Max(0, ourFunds - cheapest));
            return reserve;
        }
    }
}
