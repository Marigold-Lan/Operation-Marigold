using System;
using System.Collections;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using OperationMarigold.BehaviorTreeFramework;
using OperationMarigold.AI.BehaviorTree;
using OperationMarigold.AI.Execution;
using OperationMarigold.AI.Simulation;
using OperationMarigold.MinimaxFramework;

namespace OperationMarigold.AI.Core
{
    /// <summary>
    /// AI 中枢控制器 — 双阶段架构：
    ///   OnTurnMainPhase   → 启动规划（快照 + BT tick，Minimax 在后台线程）
    ///   OnTurnIntroAnimationComplete → 启动执行（动画播放 + EndTurn）
    /// 规划与回合过渡动画并行，消除玩家等待。
    /// </summary>
    public class AITurnController : MonoBehaviour
    {
        private const string Tag = "[AITurnController]";

        [Header("配置")]
        [SerializeField] private AIFactionConfig _factionConfig;

        [Header("引用")]
        [SerializeField] private AIActionExecutor _executor;
        [Header("性能")]
        [SerializeField, Min(16)] private int _snapshotCellsPerFrame = 64;
        [SerializeField, Min(1f)] private float _planningBudgetSeconds = 12f;
        [SerializeField] private bool _verboseAILogs = false;

        private enum Phase { Idle, Planning, WaitingForAnimation, Executing }

        private Phase _phase = Phase.Idle;
        private Coroutine _planCoroutine;
        private bool _executionDone;
        private bool _isPrewarmed;
        private MapRoot _factoryCacheMapRoot;
        private FactorySpawner[] _factoryCacheAll;

        // 规划阶段产出，供执行阶段使用
        private AIActionQueue _pendingQueue;
        private List<UnitController> _pendingUnitLookup;
        private UnitFaction _pendingFaction;

        // ─── 生命周期 ──────────────────────────────────────

        private void OnEnable()
        {
            TurnManager.OnTurnMainPhase += HandleTurnMainPhase;
            TurnManager.OnTurnIntroAnimationComplete += HandleIntroAnimationComplete;
            AITrace.Verbose = _verboseAILogs;
            if (_executor != null)
                _executor.SetVerboseLogging(_verboseAILogs);
            StartCoroutine(PrewarmIfNeeded());
        }

        private void OnDisable()
        {
            TurnManager.OnTurnMainPhase -= HandleTurnMainPhase;
            TurnManager.OnTurnIntroAnimationComplete -= HandleIntroAnimationComplete;
            AITrace.Verbose = false;
            if (_executor != null)
                _executor.OnAllActionsComplete -= HandleAllActionsComplete;
        }

        // ─── 阶段 1：规划（与过渡动画并行）──────────────────

        private void HandleTurnMainPhase(TurnContext ctx)
        {
            if (_factionConfig == null || !_factionConfig.IsAI(ctx.Faction))
                return;

            if (_planCoroutine != null)
                StopCoroutine(_planCoroutine);

            _phase = Phase.Planning;
            _pendingQueue = null;
            _pendingUnitLookup = null;
            _pendingFaction = ctx.Faction;

            Debug.Log($"{Tag} Planning started for {ctx.Faction} (Day {ctx.Day})");
            _planCoroutine = StartCoroutine(RunPlanning(ctx));
        }

        private IEnumerator RunPlanning(TurnContext ctx)
        {
            // 把重工作延迟到回合动画启动后一帧，规避动画首帧尖峰。
            yield return null;
            var planningSw = Stopwatch.StartNew();
            var snapshotSw = Stopwatch.StartNew();
            int snapshotFrames = 0;

            var faction = ctx.Faction;
            var profile = _factionConfig.GetDifficulty(faction);
            int ourFaction = (int)faction;
            int enemyFaction = faction == UnitFaction.Marigold
                ? (int)UnitFaction.Lancel
                : (int)UnitFaction.Marigold;

            var mapRoot = MapRoot.Instance;
            if (mapRoot == null)
            {
                Debug.LogWarning($"{Tag} MapRoot.Instance is null, skipping AI turn.");
                _phase = Phase.Idle;
                yield break;
            }

            // 分帧快照：避免主线程单帧尖峰。
            var snapshotBuilder = BoardSnapshotFactory.CreateBuilder(mapRoot, FactionFundsLedger.Instance, ourFaction);
            while (!snapshotBuilder.ProcessNext(_snapshotCellsPerFrame))
            {
                snapshotFrames++;
                yield return null;
            }
            snapshotFrames++;
            var boardState = snapshotBuilder.State;
            snapshotSw.Stop();

            EnsureFactoryCache(mapRoot);
            var factories = CollectFactoriesFromCache(faction);
            var unitLookup = CollectUnitLookup(mapRoot, boardState);
            int cheapestCost = GetCheapestUnitCost(factories, faction);

            AITrace.LogVerbose($"{Tag} Snapshot: {boardState.units.Count} units, {boardState.buildings.Count} buildings");

            // 快照完成后再让一帧，错开 UI 动画起始放大与首个 BT/Minimax 启动峰值。
            yield return null;

            // 组装黑板（纯数据，不再访问 Unity 对象）
            var bbSw = Stopwatch.StartNew();
            var blackboard = new Blackboard();
            blackboard.SetRef(BlackboardKeys.BoardState, boardState);
            blackboard.SetInt(BlackboardKeys.OurFaction, ourFaction);
            blackboard.SetInt(BlackboardKeys.EnemyFaction, enemyFaction);
            blackboard.SetRef(BlackboardKeys.DifficultyProfile, profile);
            blackboard.SetInt(BlackboardKeys.OurFunds, FactionFundsLedger.Instance.GetFunds(faction));
            blackboard.SetInt(BlackboardKeys.EnemyFunds, FactionFundsLedger.Instance.GetFunds((UnitFaction)enemyFaction));
            blackboard.SetInt(BlackboardKeys.CheapestUnitCost, cheapestCost);
            blackboard.SetInt(BlackboardKeys.ReserveFunds, 0);
            blackboard.SetRef(BlackboardKeys.Factories, factories);

            var actionQueue = new AIActionQueue();
            blackboard.SetRef(BlackboardKeys.ActionQueue, actionQueue);
            bbSw.Stop();

            // BT tick 循环 — 每帧一次，Minimax 在后台线程
            var btSw = Stopwatch.StartNew();
            var root = AIBehaviorTreeBuilder.Build();
            var runner = BehaviorTreeRunner.Create(root, blackboard);

            int tick = 0;
            bool planningTimedOut = false;
            NodeState runnerFinalState = NodeState.Running;
            while (true)
            {
                runnerFinalState = runner.Tick();
                if (runnerFinalState != NodeState.Running)
                {
                    Debug.Log($"{Tag} BT finished: {tick + 1} ticks, state={runnerFinalState}");
                    break;
                }

                tick++;
                if (planningSw.Elapsed.TotalSeconds >= _planningBudgetSeconds)
                {
                    planningTimedOut = true;
                    Debug.LogWarning(
                        $"{Tag} Planning timed out after {planningSw.Elapsed.TotalMilliseconds:F2}ms " +
                        $"(budget={_planningBudgetSeconds:F1}s, ticks={tick}, queue={actionQueue.Count})");
                    break;
                }

                yield return null;
            }
            btSw.Stop();

            // 规划完成 — 缓存结果
            _pendingQueue = actionQueue;
            _pendingUnitLookup = unitLookup;
            planningSw.Stop();
            Debug.Log(
                $"{Tag} Planning done: actions={actionQueue.Count}, " +
                $"snapshotMs={snapshotSw.Elapsed.TotalMilliseconds:F2}, snapshotFrames={snapshotFrames}, " +
                $"blackboardMs={bbSw.Elapsed.TotalMilliseconds:F2}, btMs={btSw.Elapsed.TotalMilliseconds:F2}, " +
                $"planningMs={planningSw.Elapsed.TotalMilliseconds:F2}, ticks={tick}, " +
                $"runnerFinalState={runnerFinalState}, timedOut={planningTimedOut}");

            if (actionQueue.Count == 0)
            {
                string reason = runnerFinalState == NodeState.Failure
                    ? "BehaviorTree returned Failure"
                    : planningTimedOut
                        ? "Planning budget timeout before nodes produced actions"
                        : "No valid action found in current board state";
                Debug.LogWarning($"{Tag} Planning produced 0 actions. reason={reason}");
            }

            // 如果动画已结束在等我们，直接进入执行
            if (_phase == Phase.WaitingForAnimation)
            {
                Debug.Log($"{Tag} Animation already done, entering execution immediately");
                StartExecution(mapRoot);
            }
            else
            {
                _phase = Phase.WaitingForAnimation;
            }
            _planCoroutine = null;
        }

        // ─── 阶段 2：执行（动画结束后）─────────────────────

        private void HandleIntroAnimationComplete(TurnContext ctx)
        {
            if (_factionConfig == null || !_factionConfig.IsAI(ctx.Faction))
                return;

            if (_phase == Phase.WaitingForAnimation && _pendingQueue != null)
            {
                Debug.Log($"{Tag} Animation done, executing actions");
                StartExecution(MapRoot.Instance);
            }
            else if (_phase == Phase.Planning)
            {
                // 规划还没完成，标记"一完成就执行"
                _phase = Phase.WaitingForAnimation;
                Debug.Log($"{Tag} Animation done but planning in progress, will execute when ready");
            }
        }

        private void StartExecution(MapRoot mapRoot)
        {
            _phase = Phase.Executing;
            StartCoroutine(RunExecution(mapRoot));
        }

        private IEnumerator RunExecution(MapRoot mapRoot)
        {
            // 解析真实引用（O(1) GetCellAt）
            if (_pendingQueue != null && mapRoot != null)
                ResolveUnitReferences(_pendingQueue, _pendingUnitLookup, mapRoot);

            int count = _pendingQueue != null ? _pendingQueue.Count : 0;
            Debug.Log($"{Tag} Executing {count} actions");

            if (_executor != null && _pendingQueue != null && _pendingQueue.HasActions)
            {
                _executionDone = false;
                _executor.OnAllActionsComplete += HandleAllActionsComplete;
                _executor.UnitLookup = _pendingUnitLookup;
                _executor.ExecuteQueue(_pendingQueue);

                float timeout = 60f;
                float elapsed = 0f;
                while (!_executionDone && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                _executor.OnAllActionsComplete -= HandleAllActionsComplete;
            }

            yield return new WaitForSeconds(0.5f);
            Debug.Log($"{Tag} Turn ending for {_pendingFaction}");
            _pendingQueue = null;
            _pendingUnitLookup = null;
            _phase = Phase.Idle;
            EndTurn();
        }

        // ─── 辅助 ──────────────────────────────────────────

        private void HandleAllActionsComplete() => _executionDone = true;

        private void EndTurn()
        {
            var tm = TurnManager.Instance;
            if (tm != null)
                tm.PlayerClickEndTurn();
        }

        private void ResolveUnitReferences(AIActionQueue queue, List<UnitController> unitLookup, MapRoot mapRoot)
        {
            var tempList = new List<AIPlannedAction>();
            while (queue.HasActions)
                tempList.Add(queue.Dequeue());

            int fallbackLookups = 0;
            int droppedActions = 0;
            for (int i = 0; i < tempList.Count; i++)
            {
                var action = tempList[i];

                if (action.unit == null && action.snapshotUnitIndex >= 0 && action.snapshotUnitIndex < unitLookup.Count)
                    action.unit = unitLookup[action.snapshotUnitIndex];

                if (action.targetUnit == null && action.snapshotTargetUnitIndex >= 0 && action.snapshotTargetUnitIndex < unitLookup.Count)
                    action.targetUnit = unitLookup[action.snapshotTargetUnitIndex];

                if (action.unit == null && mapRoot != null)
                {
                    // 兜底查找仅在索引映射失败时触发，尽量减少主线程波动。
                    var cell = mapRoot.GetCellAt(action.targetCoord);
                    if (cell != null && cell.UnitController != null)
                        action.unit = cell.UnitController;
                    fallbackLookups++;
                }

                if (action.unit == null && action.type != AIPlannedActionType.Produce)
                {
                    droppedActions++;
                    continue;
                }

                queue.Enqueue(action);
            }

            if (fallbackLookups > 0 || droppedActions > 0)
                AITrace.LogVerbose($"{Tag} ResolveUnitReferences fallback={fallbackLookups}, dropped={droppedActions}, kept={queue.Count}");
        }

        private void EnsureFactoryCache(MapRoot mapRoot)
        {
            if (mapRoot == null)
                return;

            if (_factoryCacheAll == null || _factoryCacheMapRoot != mapRoot)
            {
                _factoryCacheMapRoot = mapRoot;
                _factoryCacheAll = mapRoot.GetComponentsInChildren<FactorySpawner>(true);
            }
        }

        private List<FactorySpawner> CollectFactoriesFromCache(UnitFaction faction)
        {
            var result = new List<FactorySpawner>();
            if (_factoryCacheAll == null)
                return result;

            for (int i = 0; i < _factoryCacheAll.Length; i++)
            {
                var spawner = _factoryCacheAll[i];
                if (spawner != null && spawner.CanSpawn(faction))
                    result.Add(spawner);
            }

            return result;
        }

        private static List<UnitController> CollectUnitLookup(MapRoot mapRoot, AIBoardState boardState)
        {
            var list = new List<UnitController>(boardState.units.Count);
            for (int i = 0; i < boardState.units.Count; i++)
                list.Add(null);

            var used = new HashSet<UnitController>();

            for (int i = 0; i < boardState.units.Count; i++)
            {
                var s = boardState.units[i];
                if (!s.alive || !s.IsOnMap)
                    continue;

                var cell = mapRoot.GetCellAt(s.gridCoord);
                var uc = cell != null ? cell.UnitController : null;
                if (uc != null && uc.Data != null &&
                    string.Equals(uc.Data.id, s.unitId, StringComparison.OrdinalIgnoreCase) &&
                    !used.Contains(uc))
                {
                    list[i] = uc;
                    used.Add(uc);
                }
            }

            for (int i = 0; i < boardState.units.Count; i++)
            {
                if (list[i] != null)
                    continue;
                var s = boardState.units[i];
                if (!s.alive || s.embarkedOnUnitIndex < 0)
                    continue;

                int ti = s.embarkedOnUnitIndex;
                if (ti < 0 || ti >= list.Count || list[ti] == null)
                    continue;

                var trans = list[ti].GetComponent<ITransporter>();
                if (trans == null || trans.LoadedUnits == null)
                    continue;

                for (int c = 0; c < trans.LoadedUnits.Count; c++)
                {
                    var cargo = trans.LoadedUnits[c];
                    if (cargo == null || cargo.Data == null)
                        continue;
                    if (!string.Equals(cargo.Data.id, s.unitId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (used.Contains(cargo))
                        continue;
                    list[i] = cargo;
                    used.Add(cargo);
                    break;
                }
            }

            return list;
        }

        private static int GetCheapestUnitCost(List<FactorySpawner> factories, UnitFaction faction)
        {
            int cheapest = int.MaxValue;
            for (int i = 0; i < factories.Count; i++)
            {
                var building = factories[i].Building;
                if (building == null || building.Data == null || building.Data.factoryBuildCatalog == null)
                    continue;
                var catalog = building.Data.factoryBuildCatalog.GetBuildableUnits(faction);
                for (int j = 0; j < catalog.Count; j++)
                {
                    if (catalog[j] != null && catalog[j].cost < cheapest)
                        cheapest = catalog[j].cost;
                }
            }
            return cheapest;
        }

        private IEnumerator PrewarmIfNeeded()
        {
            if (_isPrewarmed)
                yield break;
            _isPrewarmed = true;

            // 等一帧确保场景对象完成 Awake/OnEnable。
            yield return null;

            var mapRoot = MapRoot.Instance;
            if (mapRoot != null)
                mapRoot.RebuildCellCache();

            // 预热行为树运行时（空树一次 Tick，触发 JIT）。
            var warmBb = new Blackboard();
            var warmRunner = BehaviorTreeRunner.Create(new BTSequence(), warmBb);
            warmRunner.Tick();

            // 预热 Minimax 线程创建与基础搜索路径（最小空实现，不触碰真实游戏数据）。
            var warmConfig = new SearchConfig
            {
                depth = 1,
                depth_wide = 1,
                actions_per_turn = 1,
                actions_per_turn_wide = 1,
                nodes_per_action = 1,
                nodes_per_action_wide = 1,
                search_timeout_seconds = 0.05f,
                end_turn_action_type = 1
            };

            var warmEngine = MinimaxEngine.Create(
                0,
                new WarmupLogic(),
                new WarmupHeuristic(),
                new WarmupGenerator(),
                warmConfig);

            warmEngine.RunAI(new WarmupState());
            float timeout = 0.25f;
            float elapsed = 0f;
            while (warmEngine.IsRunning() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (warmEngine.IsRunning())
                warmEngine.Stop();
            warmEngine.ClearMemory();

            Debug.Log($"{Tag} Prewarm complete");
        }

        // ─── 预热用最小实现（纯逻辑，无 Unity 对象访问）────────────────
        private sealed class WarmupState : IGameState
        {
            public IGameState Clone() => new WarmupState();
            public int GetCurrentPlayerId() => 0;
            public bool HasEnded() => false;
            public int GetOpponentPlayerId(int playerId) => 1;
        }

        private sealed class WarmupAction : IAction
        {
            public ushort Type => 1;
            public int Score { get; set; }
            public int Sort { get; set; }
            public bool Valid { get; set; } = true;
            public void Clear() { }
        }

        private sealed class WarmupLogic : IGameLogic
        {
            public void SetState(IGameState state) { }
            public void ExecuteAction(IAction action, int playerId) { }
            public void ClearResolve() { }
        }

        private sealed class WarmupHeuristic : IHeuristic
        {
            public int CalculateStateScore(IGameState state, SearchNode node) => 0;
            public int CalculateActionScore(IGameState state, IAction action) => 0;
            public int CalculateActionSort(IGameState state, IAction action) => 0;
        }

        private sealed class WarmupGenerator : IActionGenerator
        {
            public void GetPossibleActions(IGameState state, SearchNode node, List<IAction> result)
            {
                result.Add(new WarmupAction { Valid = true });
            }
        }
    }
}
