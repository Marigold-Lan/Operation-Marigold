using System.Collections.Generic;
using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 条件节点：检查黑板中指定键对应的 List(int) 是否非空。
    /// </summary>
    public class HasUnprocessedUnitsNode : BTNode
    {
        private readonly int _listKey;

        public HasUnprocessedUnitsNode(int blackboardListKey)
        {
            _listKey = blackboardListKey;
        }

        protected override NodeState OnUpdate()
        {
            var list = Board.GetRef<List<int>>(_listKey);
            return list != null && list.Count > 0 ? NodeState.Success : NodeState.Failure;
        }
    }
}
