namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 修饰节点基类：持有单个子节点，对其结果进行变换
    /// </summary>
    public abstract class BTDecorator : BTNode
    {
        private BTNode child;

        /// <summary>
        /// 被修饰的子节点
        /// </summary>
        public BTNode Child
        {
            get { return child; }
        }

        /// <summary>
        /// 设置被修饰的子节点（构建阶段调用）
        /// </summary>
        public BTDecorator SetChild(BTNode node)
        {
            child = node;
            return this;
        }

        /// <summary>
        /// 递归向子节点传递黑板引用
        /// </summary>
        public override void SetBlackboard(Blackboard board)
        {
            base.SetBlackboard(board);
            if (child != null)
                child.SetBlackboard(board);
        }

        /// <summary>
        /// 退出时中断仍在 Running 的子节点
        /// </summary>
        protected override void OnExit()
        {
            if (child != null && child.CurrentState == NodeState.Running)
                child.Abort();
        }
    }
}
