using OperationMarigold.AI;
using OperationMarigold.AI.Core;
using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 当前姿态与给定集合之一匹配时返回 Success，供 Selector 分支管线。
    /// </summary>
    public sealed class MatchStrategicPostureNode : BTNode
    {
        private readonly AIStrategicPosture[] _anyOf;

        public MatchStrategicPostureNode(params AIStrategicPosture[] anyOf)
        {
            _anyOf = anyOf != null && anyOf.Length > 0
                ? anyOf
                : new[] { AIStrategicPosture.Balanced };
        }

        protected override NodeState OnUpdate()
        {
            var current = (AIStrategicPosture)Board.GetInt(BlackboardKeys.StrategicPosture);
            for (int i = 0; i < _anyOf.Length; i++)
            {
                if (current == _anyOf[i])
                    return NodeState.Success;
            }

            return NodeState.Failure;
        }
    }
}
