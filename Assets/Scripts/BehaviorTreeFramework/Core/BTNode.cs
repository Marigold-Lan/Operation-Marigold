namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 行为树节点抽象基类
    /// 管理 OnEnter / OnUpdate / OnExit 生命周期，持有 Blackboard 引用
    /// </summary>
    public abstract class BTNode
    {
        private NodeState current_state = NodeState.Failure;
        private Blackboard blackboard;

        /// <summary>
        /// 当前节点的执行状态
        /// </summary>
        public NodeState CurrentState
        {
            get { return current_state; }
        }

        /// <summary>
        /// 该节点所使用的黑板引用
        /// </summary>
        public Blackboard Board
        {
            get { return blackboard; }
        }

        /// <summary>
        /// 设置黑板引用，由父节点或 BehaviorTreeRunner 向下传递
        /// 子类可重写此方法以递归传递给子节点
        /// </summary>
        public virtual void SetBlackboard(Blackboard board)
        {
            blackboard = board;
        }

        /// <summary>
        /// 外部调用入口：驱动一次节点评估
        /// 自动管理 OnEnter / OnUpdate / OnExit 的调用时机
        /// </summary>
        public NodeState Evaluate()
        {
            if (current_state != NodeState.Running)
                OnEnter();

            current_state = OnUpdate();

            if (current_state != NodeState.Running)
                OnExit();

            return current_state;
        }

        /// <summary>
        /// 从外部强制中断处于 Running 状态的节点
        /// </summary>
        public void Abort()
        {
            if (current_state == NodeState.Running)
            {
                current_state = NodeState.Failure;
                OnExit();
            }
        }

        /// <summary>
        /// 节点开始执行时调用（每次从非 Running 状态进入时触发）
        /// </summary>
        protected virtual void OnEnter() { }

        /// <summary>
        /// 节点每次 Tick 执行的核心逻辑，返回当前状态
        /// </summary>
        protected abstract NodeState OnUpdate();

        /// <summary>
        /// 节点结束执行时调用（从 Running 变为 Success/Failure 时触发）
        /// </summary>
        protected virtual void OnExit() { }
    }
}
