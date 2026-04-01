namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// Minimax 搜索配置参数
    /// </summary>
    public class SearchConfig
    {
        /// <summary>预测回合数（越大越智能，计算量指数增长）</summary>
        public int depth = 5;

        /// <summary>前多少层采用宽搜索（更多分支）</summary>
        public int depth_wide = 2;

        /// <summary>每回合最多连续预测动作数</summary>
        public int actions_per_turn = 5;

        /// <summary>宽搜索下的动作限制</summary>
        public int actions_per_turn_wide = 5;

        /// <summary>每个动作最多保留子节点数（超过剪枝）</summary>
        public int nodes_per_action = 8;

        /// <summary>宽搜索下的子节点数</summary>
        public int nodes_per_action_wide = 8;

        /// <summary>结束回合的动作类型（用于深度计算）</summary>
        public ushort end_turn_action_type = 1;

        /// <summary>搜索超时秒数（超过则强制结束）</summary>
        public float search_timeout_seconds = 1.5f;
    }
}
