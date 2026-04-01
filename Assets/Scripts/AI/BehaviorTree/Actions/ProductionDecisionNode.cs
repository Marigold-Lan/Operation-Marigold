using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.AI;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;
using OperationMarigold.AI.Minimax;
using OperationMarigold.AI.Execution;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 工厂生产决策节点：遍历己方可用工厂，用 AIProductionEvaluator 选最优兵种。
    /// </summary>
    public class ProductionDecisionNode : BTNode
    {
        protected override NodeState OnUpdate()
        {
            var boardState = Board.GetRef<AIBoardState>(BlackboardKeys.BoardState);
            var queue = Board.GetRef<AIActionQueue>(BlackboardKeys.ActionQueue);
            var profile = Board.GetRef<AIDifficultyProfile>(BlackboardKeys.DifficultyProfile);
            var factories = Board.GetRef<List<FactorySpawner>>(BlackboardKeys.Factories);

            if (boardState == null || queue == null || factories == null)
                return NodeState.Failure;

            int ourFaction = Board.GetInt(BlackboardKeys.OurFaction);
            int enemyFaction = Board.GetInt(BlackboardKeys.EnemyFaction);
            var faction = (UnitFaction)ourFaction;
            var strategy = Board.GetRef<AIStrategyContext>(BlackboardKeys.StrategyContext);

            int uncaptured = 0;
            for (int b = 0; b < boardState.buildings.Count; b++)
            {
                if ((int)boardState.buildings[b].ownerFaction != ourFaction)
                    uncaptured++;
            }

            var roster = AIProductionRosterState.FromBoard(boardState, ourFaction);
            var squadTargets = AIProductionSquadTargets.Compute(boardState, ourFaction, enemyFaction, uncaptured);
            var evaluator = new AIProductionEvaluator();
            int reserveFunds = AIProductionReservePolicy.ComputeReserveFunds(
                boardState, ourFaction, faction, factories, roster, squadTargets, strategy);
            Board.SetInt(BlackboardKeys.ReserveFunds, reserveFunds);

            for (int i = 0; i < factories.Count; i++)
            {
                var factory = factories[i];
                if (factory == null || !factory.CanSpawn(faction))
                    continue;

                int funds = boardState.funds.Length > ourFaction ? boardState.funds[ourFaction] : 0;
                if (funds - reserveFunds <= 0)
                    continue;

                UnitData bestUnit = evaluator.EvaluateBestUnit(
                    boardState, factory, faction, funds, reserveFunds, profile, strategy, roster, squadTargets);

                if (bestUnit == null)
                    continue;

                if (funds < bestUnit.cost)
                    continue;

                var choice = evaluator.EvaluateBestChoice(
                    boardState, factory, faction, funds, reserveFunds, profile, strategy, roster, squadTargets);
                if (choice == null || choice.unit == null)
                    continue;
                bestUnit = choice.unit;
                var filledRole = choice.filledRole;

                bool canBreakReserveForUrgentUnit =
                    (filledRole == AIUnitProductionRole.CaptureTeam && roster.CaptureTeam < squadTargets.CaptureTeam) ||
                    (filledRole == AIUnitProductionRole.TransportLogistics && roster.Transport < squadTargets.Transports) ||
                    (filledRole == AIUnitProductionRole.SupplyLogistics && roster.Supply < squadTargets.Suppliers);
                if (funds - bestUnit.cost < reserveFunds && !canBreakReserveForUrgentUnit)
                    continue;

                var planned = new AIPlannedAction
                {
                    type = AIPlannedActionType.Produce,
                    produceUnitData = bestUnit,
                    factory = factory
                };
                queue.Enqueue(planned);
                roster.RegisterIfProduced(bestUnit, filledRole);

                // 在快照中扣款并标记工厂
                boardState.funds[ourFaction] -= bestUnit.cost;
                reserveFunds = AIProductionReservePolicy.ComputeReserveFunds(
                    boardState, ourFaction, faction, factories, roster, squadTargets, strategy);
                Board.SetInt(BlackboardKeys.ReserveFunds, reserveFunds);

                // 标记工厂已造兵（找到对应建筑快照）
                var factoryBuilding = factory.Building;
                var factoryCell = factoryBuilding != null ? factoryBuilding.Cell : null;
                if (factoryCell != null)
                {
                    var coord = factoryCell.gridCoord;
                    if (boardState.IsInBounds(coord))
                    {
                        int bIdx = boardState.GetCell(coord).buildingIndex;
                        if (bIdx >= 0)
                        {
                            var b = boardState.buildings[bIdx];
                            b.hasSpawnedThisTurn = true;
                            boardState.buildings[bIdx] = b;
                        }
                    }
                }
            }

            Board.SetInt(BlackboardKeys.OurFunds, boardState.funds.Length > ourFaction ? boardState.funds[ourFaction] : 0);
            return NodeState.Success;
        }
    }
}
