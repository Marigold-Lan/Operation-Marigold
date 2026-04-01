using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 条件节点：己方资金是否 >= 最便宜兵种 cost。
    /// </summary>
    public class CanAffordProductionNode : BTNode
    {
        protected override NodeState OnUpdate()
        {
            int funds = Board.GetInt(BlackboardKeys.OurFunds);
            int cheapest = Board.GetInt(BlackboardKeys.CheapestUnitCost, int.MaxValue);
            int reserveFunds = Board.GetInt(BlackboardKeys.ReserveFunds, 0);
            return funds - reserveFunds >= cheapest ? NodeState.Success : NodeState.Failure;
        }
    }
}
