using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.AI.Minimax;

namespace OperationMarigold.AI.Execution
{
    /// <summary>
    /// 将 Minimax 的 <see cref="AIAction"/> 转为可执行的 <see cref="AIPlannedAction"/>。
    /// </summary>
    public static class AIPlanTranslator
    {
        public static AIPlannedAction ToPlanned(AIAction action)
        {
            if (action == null)
                return null;

            var planned = new AIPlannedAction
            {
                snapshotUnitIndex = action.unitIndex,
                snapshotTargetUnitIndex = action.targetUnitIndex
            };

            switch (action.actionType)
            {
                case AIActionType.Move:
                    planned.type = AIPlannedActionType.Move;
                    planned.targetCoord = action.targetCoord;
                    planned.movePath = new List<Vector2Int> { action.targetCoord };
                    return planned;
                case AIActionType.Attack:
                    planned.type = AIPlannedActionType.Attack;
                    planned.targetCoord = action.targetCoord;
                    planned.usePrimaryWeapon = action.weaponSlot == 0;
                    return planned;
                case AIActionType.Capture:
                    planned.type = AIPlannedActionType.Capture;
                    planned.targetCoord = action.targetCoord;
                    return planned;
                case AIActionType.Wait:
                    planned.type = AIPlannedActionType.Wait;
                    return planned;
                case AIActionType.Load:
                    planned.type = AIPlannedActionType.Load;
                    planned.targetCoord = action.targetCoord;
                    return planned;
                case AIActionType.Drop:
                    planned.type = AIPlannedActionType.Drop;
                    planned.targetCoord = action.targetCoord;
                    return planned;
                case AIActionType.Supply:
                    planned.type = AIPlannedActionType.Supply;
                    planned.targetCoord = action.targetCoord;
                    return planned;
                default:
                    return null;
            }
        }
    }
}
