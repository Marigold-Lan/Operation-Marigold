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
    /// 处理步兵/机甲占领单位：per-unit Minimax 搜索最优路径 + 占领/攻击决策。
    /// </summary>
    public class ProcessCaptureUnitsNode : BTNode
    {
        private const string Tag = "[ProcessCaptureUnits]";
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

        public ProcessCaptureUnitsNode(int unitListKey = -1)
        {
            _unitListKey = unitListKey >= 0 ? unitListKey : BlackboardKeys.CaptureUnits;
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
            var simLogic = new AIGameLogic();
            simLogic.SetState(_boardState);

            if (TryCaptureAtCurrentCell(unitIdx, simLogic))
                return;

            Vector2Int target = ChooseCaptureFallbackTarget(unit.gridCoord);
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

                var move = AIAction.Rent();
                move.actionType = AIActionType.Move;
                move.unitIndex = unitIdx;
                move.targetCoord = bestDest;
                simLogic.ExecuteAction(move, _ourFaction);
                AIAction.Return(move);
            }

            if (TryCaptureAtCurrentCell(unitIdx, simLogic))
                return;

            _queue.Enqueue(new AIPlannedAction
            {
                type = AIPlannedActionType.Wait,
                snapshotUnitIndex = unitIdx
            });
            var wait = AIAction.Rent();
            wait.actionType = AIActionType.Wait;
            wait.unitIndex = unitIdx;
            wait.targetCoord = _boardState.units[unitIdx].gridCoord;
            simLogic.ExecuteAction(wait, _ourFaction);
            AIAction.Return(wait);
        }

        private bool TryCaptureAtCurrentCell(int unitIdx, AIGameLogic simLogic)
        {
            if (unitIdx < 0 || unitIdx >= _boardState.units.Count)
                return false;
            var unit = _boardState.units[unitIdx];
            if (!unit.alive || !unit.IsOnMap || unit.category != UnitCategory.Soldier || unit.hasActed)
                return false;
            if (!_boardState.IsInBounds(unit.gridCoord))
                return false;

            var cell = _boardState.GetCell(unit.gridCoord);
            if (cell.buildingIndex < 0 || cell.buildingIndex >= _boardState.buildings.Count)
                return false;
            var building = _boardState.buildings[cell.buildingIndex];
            if (building.ownerFaction == unit.faction)
                return false;

            _queue.Enqueue(new AIPlannedAction
            {
                type = AIPlannedActionType.Capture,
                targetCoord = unit.gridCoord,
                snapshotUnitIndex = unitIdx
            });

            var capture = AIAction.Rent();
            capture.actionType = AIActionType.Capture;
            capture.unitIndex = unitIdx;
            capture.targetCoord = unit.gridCoord;
            simLogic.ExecuteAction(capture, _ourFaction);
            AIAction.Return(capture);
            return true;
        }

        private Vector2Int ChooseCaptureFallbackTarget(Vector2Int from)
        {
            int enemyFaction = _boardState.GetOpponentPlayerId(_ourFaction);
            Vector2Int best = new Vector2Int(_boardState.width / 2, _boardState.height / 2);
            float bestScore = float.MinValue;

            for (int i = 0; i < _boardState.buildings.Count; i++)
            {
                var b = _boardState.buildings[i];
                if ((int)b.ownerFaction == _ourFaction)
                    continue;

                float ownerPriority = 0f;
                if ((int)b.ownerFaction == enemyFaction)
                    ownerPriority = 2.2f;
                else if (b.ownerFaction == UnitFaction.None)
                    ownerPriority = 1.2f;
                else
                    ownerPriority = 0.5f;

                int dist = _boardState.ManhattanDistance(from, b.gridCoord);
                float score = ownerPriority * 1000f - dist * 4f;
                if (b.isHq)
                    score += 300f;
                else if (b.isFactory)
                    score += 70f;

                int nearbyEnemies = CountEnemyUnitsAround(b.gridCoord, 3);
                if (b.isHq && nearbyEnemies == 0)
                    score += 180f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = b.gridCoord;
                }
            }

            return best;
        }

        private int CountEnemyUnitsAround(Vector2Int center, int radius)
        {
            int enemyFaction = _boardState.GetOpponentPlayerId(_ourFaction);
            int count = 0;
            for (int i = 0; i < _boardState.units.Count; i++)
            {
                var u = _boardState.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != enemyFaction)
                    continue;
                if (_boardState.ManhattanDistance(center, u.gridCoord) <= radius)
                    count++;
            }

            return count;
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

                simLogic.ExecuteAction(action, _ourFaction);
                node = node.best_child;
            }
        }
    }
}
