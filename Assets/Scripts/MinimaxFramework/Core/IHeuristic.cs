namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 启发式接口：状态评分 + 动作排序/过滤
    /// </summary>
    public interface IHeuristic
    {
        /// <summary>
        /// 计算局面状态评分（分数高有利于 AI 玩家）
        /// </summary>
        /// <param name="state">游戏状态</param>
        /// <param name="node">当前搜索节点</param>
        /// <returns>启发式分数，一般 -10000 到 10000</returns>
        int CalculateStateScore(IGameState state, SearchNode node);

        /// <summary>
        /// 计算单个动作的评分（用于过滤低价值动作）
        /// </summary>
        /// <param name="state">游戏状态</param>
        /// <param name="action">动作</param>
        /// <returns>动作得分，高分优先</returns>
        int CalculateActionScore(IGameState state, IAction action);

        /// <summary>
        /// 计算动作排序值（用于避免同结果不同顺序的重复搜索）
        /// </summary>
        /// <param name="state">游戏状态</param>
        /// <param name="action">动作</param>
        /// <returns>排序值，0 表示不参与排序</returns>
        int CalculateActionSort(IGameState state, IAction action);
    }
}
