using OperationMarigold.AI;
using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;
using UnityEngine;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 根据当前棋盘快照计算宏观姿态，写入黑板（每回合规划开始及阶段间刷新时调用）。
    /// </summary>
    public sealed class EvaluateStrategicPostureNode : BTNode
    {
        protected override NodeState OnUpdate()
        {
            var board = Board.GetRef<AIBoardState>(BlackboardKeys.BoardState);
            if (board == null)
                return NodeState.Failure;

            int our = Board.GetInt(BlackboardKeys.OurFaction);
            int enemy = Board.GetInt(BlackboardKeys.EnemyFaction);
            var ctx = AIStrategyAdvisor.Compute(board, our, enemy);
            int cheapest = Board.GetInt(BlackboardKeys.CheapestUnitCost, 1000);
            int funds = Board.GetInt(BlackboardKeys.OurFunds, 0);
            int reserve = Mathf.RoundToInt(Mathf.Max(0, cheapest) * Mathf.Max(0.8f, ctx.ProductionReserveMul));
            reserve = Mathf.Clamp(reserve, 0, Mathf.Max(0, funds - cheapest));

            Board.SetRef(BlackboardKeys.StrategyContext, ctx);
            Board.SetInt(BlackboardKeys.StrategicPosture, (int)ctx.Posture);
            Board.SetInt(BlackboardKeys.ReserveFunds, reserve);

            AITrace.LogVerbose(
                $"[EvaluateStrategicPosture] posture={ctx.Posture}, idle={ctx.IdleFocus}, " +
                $"hqGuard={ctx.HqGuardWeight:F2}");
            return NodeState.Success;
        }
    }
}
