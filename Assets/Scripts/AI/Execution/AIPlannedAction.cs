using System.Collections.Generic;
using UnityEngine;

namespace OperationMarigold.AI.Execution
{
    public enum AIPlannedActionType
    {
        Move,
        Attack,
        Capture,
        Wait,
        Load,
        Drop,
        Supply,
        Produce
    }

    /// <summary>
    /// AI 规划的单个动作，包含真实游戏对象引用，供 AIActionExecutor 播放动画。
    /// </summary>
    public class AIPlannedAction
    {
        public AIPlannedActionType type;
        public UnitController unit;
        public Vector2Int targetCoord;
        public UnitController targetUnit;
        public List<Vector2Int> movePath;
        public UnitData produceUnitData;
        public FactorySpawner factory;

        /// <summary>是否使用主武器（攻击时）。</summary>
        public bool usePrimaryWeapon;

        /// <summary>快照单位索引（执行前由 AITurnController 解析为真实引用）。</summary>
        public int snapshotUnitIndex = -1;

        /// <summary>快照目标单位索引（攻击/装载等）。</summary>
        public int snapshotTargetUnitIndex = -1;
    }
}
