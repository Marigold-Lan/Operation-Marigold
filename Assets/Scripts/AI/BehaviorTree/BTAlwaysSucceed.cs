using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 装饰器：若子节点返回 Failure 则改为 Success。
    /// 用于确保 BT 中某阶段失败不影响后续阶段执行。
    /// Running 状态透传。
    /// </summary>
    public class BTAlwaysSucceed : BTDecorator
    {
        protected override NodeState OnUpdate()
        {
            var state = Child.Evaluate();
            return state == NodeState.Running ? NodeState.Running : NodeState.Success;
        }
    }
}
