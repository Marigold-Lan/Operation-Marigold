using System.Collections.Generic;

namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 搜索节点（用于 Minimax 搜索树）
    /// </summary>
    public class SearchNode
    {
        /// <summary>当前节点所在回合深度</summary>
        public int tdepth;

        /// <summary>当前回合已执行的动作数</summary>
        public int taction;

        /// <summary>行为排序下限，低于该值的行为不再计算</summary>
        public int sort_min;

        /// <summary>启发值（AI 最大化，对手最小化）</summary>
        public int hvalue;

        /// <summary>Alpha：AI 已知最大收益</summary>
        public int alpha;

        /// <summary>Beta：对手已知最小收益</summary>
        public int beta;

        /// <summary>导致到达该节点的动作</summary>
        public IAction last_action;

        /// <summary>当前行动玩家 ID</summary>
        public int current_player;

        /// <summary>父节点</summary>
        public SearchNode parent;

        /// <summary>最佳子节点</summary>
        public SearchNode best_child;

        /// <summary>所有子节点</summary>
        public List<SearchNode> childs = new List<SearchNode>();

        public SearchNode() { }

        /// <summary>
        /// 清理节点（用于对象池复用）
        /// </summary>
        public void Clear()
        {
            last_action = null;
            best_child = null;
            parent = null;
            childs.Clear();
        }
    }
}
