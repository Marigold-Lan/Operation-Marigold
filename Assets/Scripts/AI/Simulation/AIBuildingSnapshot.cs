using UnityEngine;

namespace OperationMarigold.AI.Simulation
{
    /// <summary>
    /// 建筑轻量快照 (struct)，供 Minimax 搜索使用。
    /// </summary>
    public struct AIBuildingSnapshot
    {
        public Vector2Int gridCoord;
        public UnitFaction ownerFaction;
        public bool isHq;
        public int incomePerTurn;
        public bool isFactory;
        public bool hasSpawnedThisTurn;
        public int captureHp;
        public int maxCaptureHp;
        public int captureDamagePerStep;
        public int captureActorUnitIndex;
    }
}
