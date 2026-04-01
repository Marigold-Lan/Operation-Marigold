namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 序列节点（AND 逻辑）：依次评估子节点
    /// 任一子节点返回 Failure 则立即返回 Failure
    /// 任一子节点返回 Running 则暂停并返回 Running
    /// 全部子节点返回 Success 则返回 Success
    /// </summary>
    public class BTSequence : BTComposite
    {
        protected override NodeState OnUpdate()
        {
            for (int i = running_child; i < ChildCount; i++)
            {
                NodeState child_state = GetChild(i).Evaluate();

                if (child_state == NodeState.Failure)
                    return NodeState.Failure;

                if (child_state == NodeState.Running)
                {
                    running_child = i;
                    return NodeState.Running;
                }
            }

            return NodeState.Success;
        }
    }
}
