namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 动作接口：可序列化、可比较
    /// 用于 Minimax 搜索的动作表示
    /// </summary>
    public interface IAction
    {
        /// <summary>
        /// 动作类型
        /// </summary>
        ushort Type { get; }

        /// <summary>
        /// 动作评分（用于启发式过滤）
        /// </summary>
        int Score { get; set; }

        /// <summary>
        /// 动作排序值（用于避免重复搜索）
        /// </summary>
        int Sort { get; set; }

        /// <summary>
        /// 动作是否有效（过滤后保留）
        /// </summary>
        bool Valid { get; set; }

        /// <summary>
        /// 清除数据（对象池复用）
        /// </summary>
        void Clear();
    }
}
