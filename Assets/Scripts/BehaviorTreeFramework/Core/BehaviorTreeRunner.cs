namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 行为树驱动引擎：负责在每个逻辑 Tick 驱动根节点
    /// 纯 C# 类，不依赖 MonoBehaviour，可由任意外部系统调用 Tick
    /// </summary>
    public class BehaviorTreeRunner
    {
        private BTNode root;
        private Blackboard blackboard;
        private NodeState last_state;

        /// <summary>
        /// 上一次 Tick 的返回状态
        /// </summary>
        public NodeState LastState
        {
            get { return last_state; }
        }

        /// <summary>
        /// 该引擎使用的黑板
        /// </summary>
        public Blackboard Board
        {
            get { return blackboard; }
        }

        /// <summary>
        /// 该引擎的根节点
        /// </summary>
        public BTNode Root
        {
            get { return root; }
        }

        /// <summary>
        /// 行为树是否仍在执行中（上次 Tick 返回 Running）
        /// </summary>
        public bool IsRunning()
        {
            return last_state == NodeState.Running;
        }

        /// <summary>
        /// 创建行为树驱动引擎（静态工厂方法，与 MinimaxEngine.Create 风格一致）
        /// </summary>
        /// <param name="root">行为树根节点</param>
        /// <param name="blackboard">共享黑板，为 null 时自动创建</param>
        public static BehaviorTreeRunner Create(BTNode root, Blackboard blackboard = null)
        {
            var runner = new BehaviorTreeRunner();
            runner.root = root;
            runner.blackboard = blackboard ?? new Blackboard();
            runner.last_state = NodeState.Failure;
            runner.root.SetBlackboard(runner.blackboard);
            return runner;
        }

        /// <summary>
        /// 驱动一次行为树评估（每帧或每个逻辑 Tick 调用）
        /// </summary>
        public NodeState Tick()
        {
            last_state = root.Evaluate();
            return last_state;
        }

        /// <summary>
        /// 强制中断当前正在执行的行为树
        /// </summary>
        public void Abort()
        {
            if (last_state == NodeState.Running)
            {
                root.Abort();
                last_state = NodeState.Failure;
            }
        }

        /// <summary>
        /// 重置引擎状态（中断执行并清空黑板）
        /// </summary>
        public void Reset()
        {
            Abort();
            blackboard.Clear();
        }
    }
}
