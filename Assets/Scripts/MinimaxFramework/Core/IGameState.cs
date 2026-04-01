namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 游戏状态接口：可克隆、可查询
    /// 用于 Minimax 搜索时的状态拷贝与查询
    /// </summary>
    public interface IGameState
    {
        /// <summary>
        /// 深拷贝当前状态（用于搜索分支）
        /// </summary>
        IGameState Clone();

        /// <summary>
        /// 当前行动玩家 ID
        /// </summary>
        int GetCurrentPlayerId();

        /// <summary>
        /// 游戏是否已结束
        /// </summary>
        bool HasEnded();

        /// <summary>
        /// 获取指定玩家的对手 ID
        /// </summary>
        int GetOpponentPlayerId(int playerId);
    }
}
