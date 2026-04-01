using UnityEngine;

namespace OperationMarigold.AI.Simulation
{
    public enum AITerrainKind
    {
        Unknown = -1,
        Plains = 0,
        Woods = 1,
        Mountain = 2,
        River = 3,
        Sea = 4,
        Road = 5,
        Bridge = 6,
        Building = 7
    }

    /// <summary>
    /// 格子轻量快照 (struct)，供 Minimax 搜索使用。
    /// </summary>
    public struct AICellSnapshot
    {
        public Vector2Int gridCoord;
        public int terrainKind;
        public int terrainStars;
        public int movementCost;

        /// <summary>占据此格子的单位索引，-1 表示无单位。</summary>
        public int unitIndex;

        /// <summary>此格子上的建筑索引，-1 表示无建筑。</summary>
        public int buildingIndex;
    }
}
