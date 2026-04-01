using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.MinimaxFramework;
using OperationMarigold.AI;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;
using OperationMarigold.AI.Minimax;
using OperationMarigold.AI.Execution;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 遍历 combatUnits，对每个单位用 Minimax 搜索最优动作（per-unit 模式），
    /// 结果加入 AIActionQueue，搜索后在快照上模拟执行全部动作链。
    /// </summary>
    public class ProcessCombatUnitsNode : BTNode
    {
        private const string Tag = "[ProcessCombatUnits]";
        private static readonly Vector2Int[] Dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1)
        };
        private readonly int _unitListKey;
        private List<int> _unitList;
        private int _currentIdx;
        private MinimaxEngine _engine;
        private AIBoardState _boardState;
        private AIActionQueue _queue;
        private AIDifficultyProfile _profile;
        private int _ourFaction;
        private float _searchStartTime;
        private float _searchHardTimeoutSeconds;
        private readonly Dictionary<Vector2Int, int> _costMap = new Dictionary<Vector2Int, int>(64);
        private readonly Queue<Vector2Int> _bfsQueue = new Queue<Vector2Int>(32);

        public ProcessCombatUnitsNode(int unitListKey = -1)
        {
            _unitListKey = unitListKey >= 0 ? unitListKey : BlackboardKeys.CombatUnits;
        }

        protected override void OnEnter()
        {
            _unitList = Board.GetRef<List<int>>(_unitListKey);
            _currentIdx = 0;
            _engine = null;
            _boardState = Board.GetRef<AIBoardState>(BlackboardKeys.BoardState);
            _queue = Board.GetRef<AIActionQueue>(BlackboardKeys.ActionQueue);
            _profile = Board.GetRef<AIDifficultyProfile>(BlackboardKeys.DifficultyProfile);
            _ourFaction = Board.GetInt(BlackboardKeys.OurFaction);
        }

        protected override NodeState OnUpdate()
        {
            if (_unitList == null || _unitList.Count == 0)
                return NodeState.Success;

            // 等待当前搜索完成
            if (_engine != null)
            {
                if (_engine.IsRunning())
                {
                    if (Time.realtimeSinceStartup - _searchStartTime > _searchHardTimeoutSeconds)
                    {
                        int timedOutUnit = _unitList[_currentIdx];
                        Debug.LogWarning($"{Tag} Search timeout for unit {timedOutUnit}, forcing stop.");
                        _engine.Stop();
                        _engine.ClearMemory();
                        _engine = null;
                        _currentIdx++;
                        Debug.LogWarning($"{Tag} Skipping timed out unit {timedOutUnit}, progress={_currentIdx}/{_unitList.Count}");
                    }
                    return NodeState.Running;
                }

                CollectAndSimulateActions();
                _engine.ClearMemory();
                _engine = null;
                _currentIdx++;
            }

            // 启动下一个单位的搜索
            while (_currentIdx < _unitList.Count)
            {
                int unitIdx = _unitList[_currentIdx];
                if (unitIdx < 0 ||
                    unitIdx >= _boardState.units.Count ||
                    _boardState.units[unitIdx].IsDead ||
                    _boardState.units[unitIdx].hasActed)
                {
                    _currentIdx++;
                    continue;
                }

                if (!ShouldUseMinimax())
                {
                    ExecuteFallbackAction(unitIdx);
                    _currentIdx++;
                    return _currentIdx < _unitList.Count ? NodeState.Running : NodeState.Success;
                }

                StartSearch(unitIdx);
                return NodeState.Running;
            }

            return NodeState.Success;
        }

        private void StartSearch(int unitIdx)
        {
            var logic = new AIGameLogic();
            var strategy = Board.GetRef<AIStrategyContext>(BlackboardKeys.StrategyContext);
            var heuristic = new AIHeuristic(_ourFaction, _profile, strategy);
            var generator = new AIActionGenerator
            {
                focusUnitIndex = unitIdx,
                strategyContext = strategy
            };
            var config = _profile != null ? _profile.ToSearchConfig() : new SearchConfig();
            _searchHardTimeoutSeconds = Mathf.Max(3f, config.search_timeout_seconds + 2f);
            _searchStartTime = Time.realtimeSinceStartup;

            Debug.Log(
                $"{Tag} Starting search for unit {unitIdx} " +
                $"(depth={config.depth}, depthWide={config.depth_wide}, " +
                $"actions={config.actions_per_turn}, actionsWide={config.actions_per_turn_wide}, " +
                $"nodes={config.nodes_per_action}, nodesWide={config.nodes_per_action_wide}, " +
                $"timeout={config.search_timeout_seconds}s)");
            _engine = MinimaxEngine.Create(_ourFaction, logic, heuristic, generator, config);
            _engine.RunAI(_boardState);
        }

        private bool ShouldUseMinimax()
        {
            int threshold = _profile != null ? Mathf.Max(1, _profile.minimaxUnitThreshold) : 8;
            return _unitList != null && _unitList.Count <= threshold;
        }

        private void ExecuteFallbackAction(int unitIdx)
        {
            var unit = _boardState.units[unitIdx];
            Vector2Int target = ChooseFallbackTarget(unitIdx, unit);
            Vector2Int bestDest = FindBestMoveToward(unit, unitIdx, target);

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

            unit.hasActed = true;
            if (bestDest != unit.gridCoord)
            {
                _boardState.MoveUnitOnGrid(unitIdx, unit.gridCoord, bestDest);
                unit.gridCoord = bestDest;
            }
            _boardState.units[unitIdx] = unit;
        }

        private Vector2Int ChooseFallbackTarget(int unitIdx, AIUnitSnapshot unit)
        {
            if (unit.transportCapacity > 0 && _boardState.CountEmbarkedCargo(unitIdx) > 0 &&
                TryFindBestPropertyTarget(unit.gridCoord, true, true, out Vector2Int transportTarget))
            {
                return transportTarget;
            }

            if (TryFindEnemyMassAnchor(out Vector2Int mass))
                return mass;

            if (TryFindBestPropertyTarget(unit.gridCoord, true, true, out Vector2Int buildingTarget))
                return buildingTarget;

            return new Vector2Int(_boardState.width / 2, _boardState.height / 2);
        }

        private bool TryFindEnemyMassAnchor(out Vector2Int massCenter)
        {
            int enemyFaction = _boardState.GetOpponentPlayerId(_ourFaction);
            long sx = 0, sy = 0, n = 0;
            for (int i = 0; i < _boardState.units.Count; i++)
            {
                var u = _boardState.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != enemyFaction)
                    continue;
                sx += u.gridCoord.x;
                sy += u.gridCoord.y;
                n++;
            }

            if (n <= 0)
            {
                massCenter = default;
                return false;
            }

            massCenter = new Vector2Int((int)(sx / n), (int)(sy / n));
            return true;
        }

        private bool TryFindBestPropertyTarget(
            Vector2Int from, bool preferEnemyFirst, bool includeNeutral, out Vector2Int best)
        {
            int enemyFaction = _boardState.GetOpponentPlayerId(_ourFaction);
            best = default;
            float bestScore = float.MinValue;
            bool found = false;

            for (int i = 0; i < _boardState.buildings.Count; i++)
            {
                var b = _boardState.buildings[i];
                if ((int)b.ownerFaction == _ourFaction)
                    continue;
                if (!includeNeutral && b.ownerFaction == UnitFaction.None)
                    continue;

                float ownerPriority = 0f;
                if ((int)b.ownerFaction == enemyFaction)
                    ownerPriority = preferEnemyFirst ? 2f : 1f;
                else if (b.ownerFaction == UnitFaction.None)
                    ownerPriority = preferEnemyFirst ? 1f : 2f;
                else
                    ownerPriority = 0.5f;

                int dist = _boardState.ManhattanDistance(from, b.gridCoord);
                float score = ownerPriority * 1000f - dist * 4f;
                if (b.isHq) score += 220f;
                if (b.isFactory) score += 40f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = b.gridCoord;
                    found = true;
                }
            }

            return found;
        }

        private Vector2Int FindBestMoveToward(AIUnitSnapshot unit, int unitIdx, Vector2Int target)
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

                for (int d = 0; d < Dirs.Length; d++)
                {
                    var next = current + Dirs[d];
                    if (!_boardState.IsInBounds(next))
                        continue;
                    ref var nextCell = ref _boardState.grid[next.x, next.y];
                    int moveCost = AIMovementCostProvider.GetCost((AITerrainKind)nextCell.terrainKind, unit.movementType);
                    if (moveCost < 0)
                        continue;
                    if (nextCell.unitIndex >= 0 && next != unit.gridCoord)
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

            Vector2Int bestDest = unit.gridCoord;
            int bestDist = _boardState.ManhattanDistance(unit.gridCoord, target);
            foreach (var kvp in _costMap)
            {
                ref var cell = ref _boardState.grid[kvp.Key.x, kvp.Key.y];
                if (cell.unitIndex >= 0 && cell.unitIndex != unitIdx)
                    continue;
                int dist = _boardState.ManhattanDistance(kvp.Key, target);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestDest = kvp.Key;
                }
            }

            return bestDest;
        }

        /// <summary>
        /// 从搜索结果中收集整条动作链（Move → Attack/Capture/Wait），
        /// 全部加入队列并在快照上模拟执行。
        /// </summary>
        private void CollectAndSimulateActions()
        {
            if (OperationMarigold.AI.Core.AITrace.Verbose)
            {
                Debug.Log($"{Tag} CollectAndSimulate enter tid={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            }

            var bestNode = _engine.GetBest();
            Debug.Log($"{Tag} Search done: nodes={_engine.GetNbNodesCalculated()}, depth={_engine.GetDepthReached()}, hasBest={bestNode != null}");

            if (bestNode == null) return;

            var simLogic = new AIGameLogic();
            simLogic.SetState(_boardState);

            var node = bestNode;
            while (node != null && node.last_action != null)
            {
                var action = node.last_action as AIAction;
                if (action == null || action.actionType == AIActionType.EndTurn)
                    break;

                var planned = AIPlanTranslator.ToPlanned(action);
                if (planned != null)
                    _queue.Enqueue(planned);

                // 在快照上模拟，确保后续单位看到的是最新状态
                simLogic.ExecuteAction(action, _ourFaction);

                node = node.best_child;
            }
        }

    }
}
