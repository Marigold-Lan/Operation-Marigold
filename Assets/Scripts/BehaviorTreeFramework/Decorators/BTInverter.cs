namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 反转修饰器：将子节点的 Success 变为 Failure，Failure 变为 Success
    /// Running 状态透传不变
    /// </summary>
    public class BTInverter : BTDecorator
    {
        protected override NodeState OnUpdate()
        {
            NodeState child_state = Child.Evaluate();

            if (child_state == NodeState.Success)
                return NodeState.Failure;

            if (child_state == NodeState.Failure)
                return NodeState.Success;

            return NodeState.Running;
        }
    }
}
