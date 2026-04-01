using System.Collections.Generic;

namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 动作生成器接口：从状态生成合法动作
    /// </summary>
    public interface IActionGenerator
    {
        /// <summary>
        /// 获取当前状态下所有可能的合法动作，填入 result 列表
        /// </summary>
        /// <param name="state">游戏状态</param>
        /// <param name="node">当前搜索节点</param>
        /// <param name="result">输出列表，用于填充动作</param>
        void GetPossibleActions(IGameState state, SearchNode node, List<IAction> result);
    }
}
