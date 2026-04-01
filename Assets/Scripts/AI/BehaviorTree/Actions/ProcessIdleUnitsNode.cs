using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.AI;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;
using OperationMarigold.AI.Execution;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 处理剩余空闲单位：向战略目标移动。
    /// 每次 OnUpdate 只处理一个单位后返回 Running，下一帧处理下一个，避免单帧卡顿。
    /// </summary>
    public class ProcessIdleUnitsNode : BTNode
    {
        private const string Tag = "[ProcessIdleUnits]";
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

        private List<int> _unitList;
        private int _currentIdx;
        private AIBoardState _boardState;
        private AIActionQueue _queue;
        private int _ourFaction;
        private Vector2Int _strategicTarget;

        // BFS 复用缓存，避免每次分配
        private readonly Dictionary<Vector2Int, int> _costMap = new Dictionary<Vector2Int, int>(64);
        private readonly Queue<Vector2Int> _bfsQueue = new Queue<Vector2Int>(32);

        protected override void OnEnter()
        {
            _unitList = Board.GetRef<List<int>>(BlackboardKeys.IdleUnits);
            _currentIdx = 0;
            _boardState = Board.GetRef<AIBoardState>(BlackboardKeys.BoardState);
            _queue = Board.GetRef<AIActionQueue>(BlackboardKeys.ActionQueue);
            _ourFaction = Board.GetInt(BlackboardKeys.OurFaction);

            if (_boardState != null)
            {
                var strategy = Board.GetRef<AIStrategyContext>(BlackboardKeys.StrategyContext);
                _strategicTarget = FindStrategicTarget(_boardState, _ourFaction, strategy);
            }

            if (_unitList != null)
                AITrace.LogVerbose($"{Tag} Processing {_unitList.Count} idle units");
        }

        protected override NodeState OnUpdate()
        {
            if (_unitList == null || _unitList.Count == 0)
                return NodeState.Success;
            if (_boardState == null || _queue == null)
                return NodeState.Failure;

            // 跳过无效单位
            while (_currentIdx < _unitList.Count)
            {
                int unitIdx = _unitList[_currentIdx];
                if (unitIdx < 0 || unitIdx >= _boardState.units.Count ||
                    _boardState.units[unitIdx].IsDead || _boardState.units[unitIdx].hasActed)
                {
                    _currentIdx++;
                    continue;
                }
                break;
            }

            if (_currentIdx >= _unitList.Count)
                return NodeState.Success;

            // 处理当前单位
            int idx = _unitList[_currentIdx];
            ProcessOneUnit(idx);
            _currentIdx++;

            // 还有更多单位：返回 Running，下一帧继续
            return _currentIdx < _unitList.Count ? NodeState.Running : NodeState.Success;
        }

        private void ProcessOneUnit(int unitIdx)
        {
            var unit = _boardState.units[unitIdx];
            Vector2Int bestDest = FindBestMoveToward(_boardState, unit, unitIdx, _strategicTarget);

            if (bestDest != unit.gridCoord)
            {
                _queue.Enqueue(new AIPlannedAction
                {
                    type = AIPlannedActionType.Move,
                    targetCoord = bestDest,
                    movePath = new List<Vector2Int> { bestDest },
                    snapshotUnitIndex = unitIdx
                });
            }

            _queue.Enqueue(new AIPlannedAction
            {
                type = AIPlannedActionType.Wait,
                snapshotUnitIndex = unitIdx
            });

            var u = _boardState.units[unitIdx];
            u.hasActed = true;
            if (bestDest != u.gridCoord)
            {
                _boardState.MoveUnitOnGrid(unitIdx, u.gridCoord, bestDest);
                u.gridCoord = bestDest;
            }
            _boardState.units[unitIdx] = u;
        }

        private static Vector2Int FindStrategicTarget(AIBoardState board, int ourFaction, AIStrategyContext strategy)
        {
            var ctx = strategy ?? AIStrategyContext.Neutral;
            switch (ctx.IdleFocus)
            {
                case IdleStrategicFocus.DefendMyHq:
                    if (ctx.HasMyHq)
                        return ctx.MyHqCoord;
                    break;
                case IdleStrategicFocus.SecureIncome:
                    return FindIncomePriorityTarget(board, ourFaction) ?? DefaultOffensiveBuildingTarget(board, ourFaction);
                case IdleStrategicFocus.HarassFrontline:
                    return FindEnemyMassAnchor(board, ourFaction) ?? DefaultOffensiveBuildingTarget(board, ourFaction);
            }

            return DefaultOffensiveBuildingTarget(board, ourFaction);
        }

        private static Vector2Int DefaultOffensiveBuildingTarget(AIBoardState board, int ourFaction)
        {
            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if (b.isHq && (int)b.ownerFaction == board.GetOpponentPlayerId(ourFaction))
                    return b.gridCoord;
            }

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if (b.isFactory && (int)b.ownerFaction == board.GetOpponentPlayerId(ourFaction))
                    return b.gridCoord;
            }

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction == board.GetOpponentPlayerId(ourFaction))
                    return b.gridCoord;
            }

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction != ourFaction)
                    return b.gridCoord;
            }

            return new Vector2Int(board.width / 2, board.height / 2);
        }

        private static Vector2Int? FindIncomePriorityTarget(AIBoardState board, int ourFaction)
        {
            Vector2Int from = OurForceCentroid(board, ourFaction);
            Vector2Int? best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction == ourFaction)
                    continue;
                if (b.ownerFaction == UnitFaction.None && b.maxCaptureHp <= 0)
                    continue;

                float income = Mathf.Max(1, b.incomePerTurn);
                int d = board.ManhattanDistance(from, b.gridCoord);
                float score = income * 10f / (1f + d * 0.18f);
                if (b.isHq)
                    score *= 0.35f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = b.gridCoord;
                }
            }

            return best;
        }

        private static Vector2Int? FindEnemyMassAnchor(AIBoardState board, int ourFaction)
        {
            long sx = 0, sy = 0, n = 0;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || (int)u.faction == ourFaction)
                    continue;
                sx += u.gridCoord.x;
                sy += u.gridCoord.y;
                n++;
            }

            if (n == 0)
                return null;
            return new Vector2Int((int)(sx / n), (int)(sy / n));
        }

        private static Vector2Int OurForceCentroid(AIBoardState board, int ourFaction)
        {
            long sx = 0, sy = 0, n = 0;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || (int)u.faction != ourFaction)
                    continue;
                sx += u.gridCoord.x;
                sy += u.gridCoord.y;
                n++;
            }

            if (n == 0)
                return new Vector2Int(board.width / 2, board.height / 2);
            return new Vector2Int((int)(sx / n), (int)(sy / n));
        }

        private Vector2Int FindBestMoveToward(AIBoardState board, AIUnitSnapshot unit, int unitIdx, Vector2Int target)
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
                    if (!board.IsInBounds(next)) continue;

                    ref var nextCell = ref board.grid[next.x, next.y];
                    int moveCost = AIMovementCostProvider.GetCost(
                        (AITerrainKind)nextCell.terrainKind,
                        unit.movementType);
                    if (moveCost < 0) continue;

                    // 与 runtime Pathfinding/MovementRules 对齐：除起点外任何占据格都不可通行。
                    if (nextCell.unitIndex >= 0 && next != unit.gridCoord) continue;

                    int totalCost = currentCost + moveCost;
                    if (totalCost > maxRange) continue;
                    if (_costMap.TryGetValue(next, out int existing) && existing <= totalCost) continue;

                    _costMap[next] = totalCost;
                    _bfsQueue.Enqueue(next);
                }
            }

            Vector2Int bestDest = unit.gridCoord;
            int bestDist = board.ManhattanDistance(unit.gridCoord, target);

            foreach (var kvp in _costMap)
            {
                ref var cell = ref board.grid[kvp.Key.x, kvp.Key.y];
                if (cell.unitIndex >= 0 && cell.unitIndex != unitIdx) continue;

                int dist = board.ManhattanDistance(kvp.Key, target);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestDest = kvp.Key;
                }
            }

            return bestDest;
        }
    }
}
