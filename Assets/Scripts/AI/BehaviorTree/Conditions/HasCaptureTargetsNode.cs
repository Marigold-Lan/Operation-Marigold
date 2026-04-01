using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 条件节点：是否存在可被占领的敌方/中立建筑。
    /// </summary>
    public class HasCaptureTargetsNode : BTNode
    {
        protected override NodeState OnUpdate()
        {
            var board = Board.GetRef<AIBoardState>(BlackboardKeys.BoardState);
            if (board == null) return NodeState.Failure;

            int ourFaction = Board.GetInt(BlackboardKeys.OurFaction);

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction != ourFaction)
                    return NodeState.Success;
            }
            return NodeState.Failure;
        }
    }
}
