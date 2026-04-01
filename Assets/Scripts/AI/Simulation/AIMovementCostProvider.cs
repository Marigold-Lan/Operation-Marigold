using UnityEngine;

namespace OperationMarigold.AI.Simulation
{
    /// <summary>
    /// AI 侧的移动消耗计算：与 runtime 的 MovementCostProvider 保持等价。
    /// </summary>
    public static class AIMovementCostProvider
    {
        public static int GetCost(AITerrainKind kind, MovementType movementType)
        {
            if (kind == AITerrainKind.Unknown)
                return -1;

            var runtimeTerrain = (MovementCostProvider.MovementTerrainKind)kind;
            return MovementCostProvider.GetCostForTerrainKind(runtimeTerrain, movementType);
        }
    }
}

