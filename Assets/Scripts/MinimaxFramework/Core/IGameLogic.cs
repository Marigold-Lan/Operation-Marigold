namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 逻辑执行器接口：纯逻辑、无动画
    /// 用于 AI 搜索时模拟执行动作
    /// </summary>
    public interface IGameLogic
    {
        /// <summary>
        /// 设置要模拟的游戏状态
        /// </summary>
        void SetState(IGameState state);

        /// <summary>
        /// 执行指定动作（模拟，无动画）
        /// </summary>
        /// <param name="action">要执行的动作</param>
        /// <param name="playerId">执行该动作的玩家 ID</param>
        void ExecuteAction(IAction action, int playerId);

        /// <summary>
        /// 清理解析队列等中间状态
        /// </summary>
        void ClearResolve();
    }
}
