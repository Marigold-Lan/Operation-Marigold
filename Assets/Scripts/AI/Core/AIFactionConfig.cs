using System;
using UnityEngine;

namespace OperationMarigold.AI.Core
{
    /// <summary>
    /// 配置每个阵营是 AI 还是玩家，以及对应的难度。
    /// 支持 AI vs AI 调试模式（两个阵营都设为 AI）。
    /// </summary>
    [CreateAssetMenu(fileName = "AIFactionConfig", menuName = "Operation Marigold/AI/Faction Config")]
    public class AIFactionConfig : ScriptableObject
    {
        [Header("Marigold 阵营")]
        public bool marigoldIsAI = false;
        public AIDifficultyProfile marigoldDifficulty;

        [Header("Lancel 阵营")]
        public bool lancelIsAI = true;
        public AIDifficultyProfile lancelDifficulty;

        public bool IsAI(UnitFaction faction)
        {
            switch (faction)
            {
                case UnitFaction.Marigold: return marigoldIsAI;
                case UnitFaction.Lancel:   return lancelIsAI;
                default: return false;
            }
        }

        public AIDifficultyProfile GetDifficulty(UnitFaction faction)
        {
            switch (faction)
            {
                case UnitFaction.Marigold: return marigoldDifficulty;
                case UnitFaction.Lancel:   return lancelDifficulty;
                default: return null;
            }
        }
    }
}
