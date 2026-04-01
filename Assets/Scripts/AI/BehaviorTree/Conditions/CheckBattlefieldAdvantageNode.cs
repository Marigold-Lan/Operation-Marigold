using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 条件节点：检查战场态势是否为优势 (advantage > 0)。
    /// BattlefieldAdvantage 由 EvaluateBattlefieldNode 写入。
    /// </summary>
    public class CheckBattlefieldAdvantageNode : BTNode
    {
        protected override NodeState OnUpdate()
        {
            float advantage = Board.GetFloat(BlackboardKeys.BattlefieldAdvantage, 0f);
            return advantage > 0f ? NodeState.Success : NodeState.Failure;
        }
    }
}
