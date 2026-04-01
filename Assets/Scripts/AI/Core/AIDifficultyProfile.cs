using System;
using UnityEngine;
using OperationMarigold.MinimaxFramework;

namespace OperationMarigold.AI.Core
{
    /// <summary>
    /// AI 难度配置，ScriptableObject。
    /// 控制搜索深度、评估权重、随机扰动等。
    /// </summary>
    [CreateAssetMenu(fileName = "NewAIDifficulty", menuName = "Operation Marigold/AI/Difficulty Profile")]
    public class AIDifficultyProfile : ScriptableObject
    {
        [Header("搜索参数")]
        [Tooltip("Minimax 搜索深度（回合数）")]
        public int searchDepth = 3;

        [Tooltip("前几层采用宽搜索")]
        public int depthWide = 2;

        [Tooltip("每回合最多连续动作数")]
        public int actionsPerTurn = 5;

        [Tooltip("宽搜索层每回合最多连续动作数（<=0 时回退 actionsPerTurn）")]
        public int actionsPerTurnWide = 0;

        [Tooltip("每个动作保留的子节点数")]
        public int nodesPerAction = 8;

        [Tooltip("宽搜索层每个动作保留子节点数（<=0 时回退 nodesPerAction）")]
        public int nodesPerActionWide = 0;

        [Tooltip("搜索超时（秒）")]
        public float searchTimeoutSeconds = 1.0f;

        [Tooltip("仅当单节点处理单位数 <= 该阈值时启用 Minimax；超出走轻量规则")]
        public int minimaxUnitThreshold = int.MaxValue;

        [Header("评估权重")]
        public Weights weights = Weights.Default;

        [Header("随机扰动")]
        [Tooltip("对评分施加的随机扰动百分比 (0-100)，值越大决策越不精确")]
        [Range(0, 100)]
        public int randomNoisePercent = 5;

        [Header("生产效率")]
        [Tooltip("生产评估折扣系数 (0-1)，低难度会故意选次优兵种")]
        [Range(0f, 1f)]
        public float productionEfficiency = 0.9f;

        public SearchConfig ToSearchConfig()
        {
            int wideActions = actionsPerTurnWide > 0 ? actionsPerTurnWide : actionsPerTurn;
            int wideNodes = nodesPerActionWide > 0 ? nodesPerActionWide : nodesPerAction;
            return new SearchConfig
            {
                depth = searchDepth,
                depth_wide = depthWide,
                actions_per_turn = actionsPerTurn,
                actions_per_turn_wide = wideActions,
                nodes_per_action = nodesPerAction,
                nodes_per_action_wide = wideNodes,
                end_turn_action_type = (ushort)Minimax.AIActionType.EndTurn,
                search_timeout_seconds = searchTimeoutSeconds
            };
        }

        [Serializable]
        public struct Weights
        {
            [Tooltip("单位价值权重")]   public float unitValue;
            [Tooltip("建筑控制权重")]   public float building;
            [Tooltip("占领进度权重")]   public float captureProgress;
            [Tooltip("位置逼近权重")]   public float position;
            [Tooltip("经济差距权重")]   public float funds;

            public static Weights Default => new Weights
            {
                unitValue = 1f,
                building = 100f,
                captureProgress = 50f,
                position = 0.8f,
                funds = 1f
            };
        }
    }
}
