using System.Collections.Generic;

namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 复合节点基类：管理有序子节点列表
    /// 子类（Selector / Sequence）实现具体的遍历策略
    /// </summary>
    public abstract class BTComposite : BTNode
    {
        private List<BTNode> children = new List<BTNode>();

        /// <summary>当前正在执行的子节点索引，用于 Running 状态恢复</summary>
        protected int running_child;

        /// <summary>
        /// 子节点数量
        /// </summary>
        public int ChildCount
        {
            get { return children.Count; }
        }

        /// <summary>
        /// 添加子节点（构建阶段调用）
        /// </summary>
        public BTComposite AddChild(BTNode child)
        {
            children.Add(child);
            return this;
        }

        /// <summary>
        /// 通过索引获取子节点
        /// </summary>
        public BTNode GetChild(int index)
        {
            return children[index];
        }

        /// <summary>
        /// 递归向所有子节点传递黑板引用
        /// </summary>
        public override void SetBlackboard(Blackboard board)
        {
            base.SetBlackboard(board);
            for (int i = 0; i < children.Count; i++)
                children[i].SetBlackboard(board);
        }

        /// <summary>
        /// 进入时重置子节点遍历索引
        /// </summary>
        protected override void OnEnter()
        {
            running_child = 0;
        }

        /// <summary>
        /// 退出时中断仍在 Running 的子节点
        /// </summary>
        protected override void OnExit()
        {
            for (int i = running_child; i < children.Count; i++)
            {
                if (children[i].CurrentState == NodeState.Running)
                    children[i].Abort();
            }
        }
    }
}
