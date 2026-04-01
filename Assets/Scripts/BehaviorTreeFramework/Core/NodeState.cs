namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 行为树节点的执行状态
    /// </summary>
    public enum NodeState : byte
    {
        /// <summary>节点仍在执行中，下次 Tick 将继续</summary>
        Running,

        /// <summary>节点执行成功</summary>
        Success,

        /// <summary>节点执行失败</summary>
        Failure
    }
}
