using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.MinimaxFramework;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.Minimax
{
    /// <summary>
    /// 动作生成器。
    /// 
    /// 回合制战棋中一个单位的回合 = [可选 Move] + [必选 Action(Attack/Capture/Wait)]。
    /// 利用 MinimaxEngine 的 actions_per_turn 特性，拆为两步：
    ///   第1步: 生成 Move(dest) 到各可达格 + 在原地的 Post-Move 动作
    ///   第2步: Move 执行后 hasMovedThisTurn=true，再调用 GetPossibleActions
    ///          此时只生成 Post-Move 动作(Attack/Capture/Wait)
    ///
    /// focusUnitIndex >= 0 时，只为该单位生成动作（per-unit 搜索模式）。
    /// </summary>
    public class AIActionGenerator : IActionGenerator
    {
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        /// <summary>
        /// 设为 >= 0 时仅为该单位生成动作；-1 = 全部单位（默认）。
        /// </summary>
        public int focusUnitIndex = -1;
        public AIStrategyContext strategyContext;

        private readonly Dictionary<Vector2Int, int> _costMap = new Dictionary<Vector2Int, int>(128);
        private readonly Queue<Vector2Int> _bfsQueue = new Queue<Vector2Int>(64);

        public void GetPossibleActions(IGameState state, SearchNode node, List<IAction> result)
        {
            var board = (AIBoardState)state;
            int playerId = board.currentPlayerId;

            int unitIdx = FindTargetUnit(board, playerId);
            if (unitIdx < 0)
            {
                AddEndTurn(result);
                return;
            }

            var unit = board.units[unitIdx];

            if (unit.hasMovedThisTurn)
            {
                // 已移动 —— 只生成落地后的动作
                GeneratePostMoveActions(board, unit, unitIdx, unit.gridCoord, true, result);
                return;
            }

            // 未移动 —— BFS 可达格，为每个格子生成 Move 动作
            var reachable = ComputeReachableCells(board, unit, unitIdx);

            foreach (var kvp in reachable)
            {
                Vector2Int dest = kvp.Key;
                if (dest == unit.gridCoord)
                    continue;

                ref var destCell = ref board.grid[dest.x, dest.y];
                if (destCell.unitIndex >= 0 && destCell.unitIndex != unitIdx)
                    continue;

                if (ShouldSkipUnsafeAdvance(board, unit, dest))
                    continue;

                var move = AIAction.Rent();
                move.actionType = AIActionType.Move;
                move.unitIndex = unitIdx;
                move.targetCoord = dest;
                result.Add(move);
            }

            // 在原地不移动时的动作（Attack/Capture/Wait）
            GeneratePostMoveActions(board, unit, unitIdx, unit.gridCoord, false, result);
        }

        private int FindTargetUnit(AIBoardState board, int playerId)
        {
            if (focusUnitIndex >= 0)
            {
                if (focusUnitIndex >= board.units.Count) return -1;
                var u = board.units[focusUnitIndex];
                if (!u.alive || u.hasActed || (int)u.faction != playerId)
                    return -1;
                return focusUnitIndex;
            }
            return board.FindFirstIdleUnit(playerId);
        }

        private void GeneratePostMoveActions(
            AIBoardState board, AIUnitSnapshot unit, int unitIdx,
            Vector2Int position, bool hasMoved, List<IAction> result)
        {
            ref var cell = ref board.grid[position.x, position.y];

            GenerateAttackActions(board, unit, unitIdx, position, hasMoved, result);
            GenerateCaptureAction(board, unit, unitIdx, position, cell, result);
            GenerateSupplyActions(board, unit, unitIdx, position, result);
            GenerateLoadActions(board, unit, unitIdx, position, result);
            GenerateDropActions(board, unit, unitIdx, position, result);

            var wait = AIAction.Rent();
            wait.actionType = AIActionType.Wait;
            wait.unitIndex = unitIdx;
            wait.targetCoord = position;
            result.Add(wait);
        }

        private static void AddEndTurn(List<IAction> result)
        {
            var endAction = AIAction.Rent();
            endAction.actionType = AIActionType.EndTurn;
            result.Add(endAction);
        }

        // ─── BFS ────────────────────────────────────────────

        private Dictionary<Vector2Int, int> ComputeReachableCells(AIBoardState board, AIUnitSnapshot unit, int unitIdx)
        {
            _costMap.Clear();
            _bfsQueue.Clear();

            int maxRange = Mathf.Min(unit.movementRange, unit.fuel);
            _costMap[unit.gridCoord] = 0;
            _bfsQueue.Enqueue(unit.gridCoord);

            while (_bfsQueue.Count > 0)
            {
                var current = _bfsQueue.Dequeue();
                int currentCost = _costMap[current];

                for (int d = 0; d < 4; d++)
                {
                    var next = current + Dirs[d];
                    if (!board.IsInBounds(next))
                        continue;

                    if (!MovementRulesShared.TryGetSnapshotTraversalCost(board, unit, unitIdx, next, unit.gridCoord, out int moveCost))
                        continue;

                    int totalCost = currentCost + moveCost;
                    if (totalCost > maxRange)
                        continue;

                    if (_costMap.TryGetValue(next, out int existing) && existing <= totalCost)
                        continue;

                    _costMap[next] = totalCost;
                    _bfsQueue.Enqueue(next);
                }
            }

            return _costMap;
        }

        // ─── 攻击 ───────────────────────────────────────────

        private void GenerateAttackActions(
            AIBoardState board, AIUnitSnapshot unit, int unitIdx,
            Vector2Int fromCoord, bool hasMoved, List<IAction> result)
        {
            var unitCopy = unit;
            unitCopy.hasMovedThisTurn = hasMoved;

            int maxRange = 0;
            if (unitCopy.hasPrimaryWeapon && unitCopy.ammo > 0 && (!hasMoved || !unitCopy.primaryRequiresStationary))
                maxRange = Mathf.Max(maxRange, Mathf.Max(1, unitCopy.primaryRangeMax));
            if (unitCopy.hasSecondaryWeapon && (!hasMoved || !unitCopy.secondaryRequiresStationary))
                maxRange = Mathf.Max(maxRange, Mathf.Max(1, unitCopy.secondaryRangeMax));

            if (maxRange <= 0) return;

            for (int i = 0; i < board.units.Count; i++)
            {
                var target = board.units[i];
                if (!target.alive || !target.IsOnMap || target.faction == unit.faction)
                    continue;

                int dist = board.ManhattanDistance(fromCoord, target.gridCoord);
                if (dist < 1 || dist > maxRange)
                    continue;

                if (unitCopy.TrySelectWeapon(target.category, dist, out bool usePrimary, out _))
                {
                    var atk = AIAction.Rent();
                    atk.actionType = AIActionType.Attack;
                    atk.unitIndex = unitIdx;
                    atk.targetCoord = fromCoord;
                    atk.targetUnitIndex = i;
                    atk.weaponSlot = usePrimary ? 0 : 1;
                    result.Add(atk);
                }
            }
        }

        // ─── 占领 ───────────────────────────────────────────

        private void GenerateCaptureAction(
            AIBoardState board, AIUnitSnapshot unit, int unitIdx,
            Vector2Int dest, AICellSnapshot destCell, List<IAction> result)
        {
            if (destCell.buildingIndex < 0)
                return;
            if (destCell.buildingIndex >= board.buildings.Count)
                return;

            var building = board.buildings[destCell.buildingIndex];
            if (!CaptureRulesShared.CanSnapshotCapture(unit, building))
                return;

            var cap = AIAction.Rent();
            cap.actionType = AIActionType.Capture;
            cap.unitIndex = unitIdx;
            cap.targetCoord = dest;
            result.Add(cap);
        }

        private static void GenerateSupplyActions(
            AIBoardState board, AIUnitSnapshot unit, int unitIdx, Vector2Int position, List<IAction> result)
        {
            bool hasAnyValidTarget = false;
            for (int d = 0; d < 4; d++)
            {
                var n = position + Dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                ref var nc = ref board.grid[n.x, n.y];
                if (nc.unitIndex < 0 || nc.unitIndex >= board.units.Count)
                    continue;
                var ally = board.units[nc.unitIndex];
                if (!ally.alive || !ally.IsOnMap || ally.faction != unit.faction)
                    continue;
                int dist = board.ManhattanDistance(position, n);
                if (!SupplyRulesShared.CanSnapshotSupply(unit, ally, dist))
                    continue;
                hasAnyValidTarget = true;
                break;
            }

            if (!hasAnyValidTarget)
                return;

            var sup = AIAction.Rent();
            sup.actionType = AIActionType.Supply;
            sup.unitIndex = unitIdx;
            sup.targetCoord = position;
            result.Add(sup);
        }

        private void GenerateLoadActions(
            AIBoardState board, AIUnitSnapshot unit, int unitIdx, Vector2Int position, List<IAction> result)
        {
            for (int d = 0; d < 4; d++)
            {
                var n = position + Dirs[d];
                if (!board.IsInBounds(n))
                    continue;
                ref var nc = ref board.grid[n.x, n.y];
                if (nc.unitIndex < 0 || nc.unitIndex == unitIdx)
                    continue;
                if (!TransportRulesShared.CanSnapshotLoad(board, unitIdx, nc.unitIndex))
                    continue;

                var load = AIAction.Rent();
                load.actionType = AIActionType.Load;
                load.unitIndex = unitIdx;
                load.targetUnitIndex = nc.unitIndex;
                load.targetCoord = position;
                result.Add(load);
            }
        }

        private void GenerateDropActions(
            AIBoardState board, AIUnitSnapshot unit, int unitIdx, Vector2Int position, List<IAction> result)
        {
            if (unit.transportCapacity <= 0 || unit.hasActed)
                return;
            if (board.CountEmbarkedCargo(unitIdx) <= 0)
                return;

            int cargoIdx = FindFirstEmbarkedCargoIndex(board, unitIdx);
            if (cargoIdx < 0)
                return;

            for (int d = 0; d < 4; d++)
            {
                var drop = position + Dirs[d];
                if (!TransportRulesShared.CanSnapshotDrop(board, unitIdx, cargoIdx, drop))
                    continue;

                var act = AIAction.Rent();
                act.actionType = AIActionType.Drop;
                act.unitIndex = unitIdx;
                act.targetUnitIndex = cargoIdx;
                act.targetCoord = drop;
                result.Add(act);
            }
        }

        private static int FindFirstEmbarkedCargoIndex(AIBoardState board, int transporterIdx)
        {
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (u.alive && u.embarkedOnUnitIndex == transporterIdx)
                    return i;
            }

            return -1;
        }

        private bool ShouldSkipUnsafeAdvance(AIBoardState board, AIUnitSnapshot unit, Vector2Int dest)
        {
            if (!(unit.primaryRequiresStationary || unit.primaryRangeMax >= 3))
                return false;

            int safetyDistance = Mathf.Max(2, strategyContext != null ? strategyContext.RangedSafetyDistance : 3);
            int nearestEnemyDist = int.MaxValue;
            for (int i = 0; i < board.units.Count; i++)
            {
                var e = board.units[i];
                if (!e.alive || !e.IsOnMap || e.faction == unit.faction)
                    continue;
                int dist = board.ManhattanDistance(dest, e.gridCoord);
                if (dist < nearestEnemyDist)
                    nearestEnemyDist = dist;
            }

            return nearestEnemyDist <= safetyDistance;
        }
    }
}
