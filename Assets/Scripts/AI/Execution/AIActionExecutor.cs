using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using OperationMarigold.GameplayEvents;
using UnityEngine;

namespace OperationMarigold.AI.Execution
{
    /// <summary>
    /// MonoBehaviour 组件：用协程从 AIActionQueue 逐个取出动作，
    /// 在真实游戏对象上执行并播放动画。每个动作之间加短延迟提升观感。
    /// </summary>
    public class AIActionExecutor : MonoBehaviour
    {
        private const string Tag = "[AIActionExecutor]";
        [SerializeField] private float delayBetweenActions = 0.3f;
        [SerializeField] private bool _verboseMoveLogs = false;

        /// <summary>所有动作执行完毕后触发。</summary>
        public event Action OnAllActionsComplete;

        private Coroutine _executionCoroutine;
        private bool _moveComplete;

        public bool IsExecuting => _executionCoroutine != null;

        /// <summary>当前正在播放的 AI 动作对应的世界对象（供棋盘相机死区跟随）。动作间隙为 null。</summary>
        public Transform ActingSubject { get; private set; }

        /// <summary>
        /// 需要从快照 unitIndex 映射到真实 UnitController 的查找表。
        /// 在 AITurnController 启动执行前设置。
        /// </summary>
        public List<UnitController> UnitLookup { get; set; }

        public void SetVerboseLogging(bool verbose)
        {
            _verboseMoveLogs = verbose;
        }

        public void ExecuteQueue(AIActionQueue queue)
        {
            if (_executionCoroutine != null)
                StopCoroutine(_executionCoroutine);
            _executionCoroutine = StartCoroutine(ExecuteQueueCoroutine(queue));
        }

        public void Cancel()
        {
            if (_executionCoroutine != null)
            {
                StopCoroutine(_executionCoroutine);
                _executionCoroutine = null;
            }

            ActingSubject = null;
        }

        private IEnumerator ExecuteQueueCoroutine(AIActionQueue queue)
        {
            try
            {
                while (queue.HasActions)
                {
                    var action = queue.Dequeue();
                    ActingSubject = ResolveActingSubject(action);
                    try
                    {
                        yield return ExecuteAction(action);
                    }
                    finally
                    {
                        ActingSubject = null;
                    }

                    yield return new WaitForSeconds(delayBetweenActions);
                }
            }
            finally
            {
                _executionCoroutine = null;
                ActingSubject = null;
                OnAllActionsComplete?.Invoke();
            }
        }

        private static Transform ResolveActingSubject(AIPlannedAction action)
        {
            if (action == null) return null;
            if (action.type == AIPlannedActionType.Produce)
                return action.factory != null ? action.factory.transform : null;
            // 攻击时镜头对准目标（敌方），避免结束后焦点仍留在己方格再被光标拉回。
            if (action.type == AIPlannedActionType.Attack && action.targetUnit != null)
                return action.targetUnit.transform;
            return action.unit != null ? action.unit.transform : null;
        }

        private IEnumerator ExecuteAction(AIPlannedAction action)
        {
            switch (action.type)
            {
                case AIPlannedActionType.Move:
                    yield return ExecuteMove(action);
                    break;

                case AIPlannedActionType.Attack:
                    yield return ExecuteAttack(action);
                    break;

                case AIPlannedActionType.Capture:
                    yield return ExecuteCapture(action);
                    break;

                case AIPlannedActionType.Wait:
                    ExecuteWait(action);
                    break;

                case AIPlannedActionType.Produce:
                    ExecuteProduce(action);
                    break;

                case AIPlannedActionType.Load:
                    yield return ExecuteLoad(action);
                    break;

                case AIPlannedActionType.Drop:
                    yield return ExecuteDrop(action);
                    break;

                case AIPlannedActionType.Supply:
                    ExecuteSupply(action);
                    break;
            }
        }

        private IEnumerator ExecuteMove(AIPlannedAction action)
        {
            var unit = action.unit;
            if (unit == null || unit.Movement == null)
                yield break;

            var sw = Stopwatch.StartNew();
            _moveComplete = false;

            var context = BuildAIContext(action, hasTargetCoord: true);
            var command = new MoveCommand(unit, action.targetCoord, () => _moveComplete = true);
            var executed = CommandExecutor.Execute(command, context);

            sw.Stop();
            if (_verboseMoveLogs)
                UnityEngine.Debug.Log($"{Tag} Move {unit.name}: executed={executed}, pathMs={sw.Elapsed.TotalMilliseconds:F2}");

            if (!executed)
            {
                UnityEngine.Debug.LogWarning($"{Tag} Move command rejected for {unit.name} -> {action.targetCoord}, mark acted");
                unit.HasActed = true;
                yield break;
            }

            yield return new WaitUntil(() => _moveComplete);
        }

        private IEnumerator ExecuteAttack(AIPlannedAction action)
        {
            var unit = action.unit;
            var target = action.targetUnit;
            if (unit == null || unit.Combat == null || target == null)
                yield break;

            bool completed = false;
            void OnCompleted(UnitController attacker, UnitController defender, bool usePrimary)
            {
                if (attacker == unit && defender == target)
                    completed = true;
            }

            UnitCombat.OnAttackSequenceCompleted += OnCompleted;
            try
            {
                var context = BuildAIContext(action, hasTargetCoord: true);
                var command = new AttackCommand(unit, target, action.targetCoord);
                if (!CommandExecutor.Execute(command, context))
                    completed = true;

                float timeout = 8f;
                float elapsed = 0f;
                while (!completed && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                UnitCombat.OnAttackSequenceCompleted -= OnCompleted;
            }
        }

        private IEnumerator ExecuteCapture(AIPlannedAction action)
        {
            var unit = action.unit;
            if (unit == null) yield break;

            var cell = unit.CurrentCell;
            var building = cell != null ? cell.Building : null;
            if (building != null)
            {
                bool completed = false;

                void OnApplied(UnitController capturer, BuildingController b, int capturePower, int oldHp, int newHp, bool captured)
                {
                    if (capturer == unit && b == building)
                        completed = true;
                }

                void OnInterrupted(UnitController capturer, BuildingController b, CaptureInterruptReason reason)
                {
                    if (capturer == unit && b == building)
                        completed = true;
                }

                UnitCapture.OnCaptureApplied += OnApplied;
                UnitCapture.OnCaptureInterrupted += OnInterrupted;
                try
                {
                    var context = BuildAIContext(action, hasTargetCoord: false);
                    context.CurrentCell = cell;
                    if (!CommandExecutor.Execute(new CaptureCommand(), context))
                    {
                        unit.HasActed = true;
                        completed = true;
                    }

                    float timeout = 10f;
                    float elapsed = 0f;
                    while (!completed && elapsed < timeout)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                }
                finally
                {
                    UnitCapture.OnCaptureApplied -= OnApplied;
                    UnitCapture.OnCaptureInterrupted -= OnInterrupted;
                }
            }
            else
            {
                unit.HasActed = true;
            }
        }

        private void ExecuteWait(AIPlannedAction action)
        {
            if (action.unit == null)
                return;

            var context = BuildAIContext(action, hasTargetCoord: false);
            if (!CommandExecutor.Execute(new WaitCommand(), context))
                action.unit.HasActed = true;
        }

        private IEnumerator ExecuteLoad(AIPlannedAction action)
        {
            var cargo = action.unit;
            var transporter = action.targetUnit;
            if (cargo == null || transporter == null)
                yield break;

            var context = BuildAIContext(action, hasTargetCoord: false);
            if (!CommandExecutor.Execute(new LoadCommand(), context))
                cargo.HasActed = true;
            yield return null;
        }

        private IEnumerator ExecuteDrop(AIPlannedAction action)
        {
            var apc = action.unit;
            var cargo = action.targetUnit;
            if (apc == null || cargo == null)
                yield break;

            var context = BuildAIContext(action, hasTargetCoord: true);
            if (!CommandExecutor.Execute(new DropCommand(), context))
                apc.HasActed = true;
            yield return null;
        }

        private void ExecuteSupply(AIPlannedAction action)
        {
            var supplierUnit = action.unit;
            if (supplierUnit == null)
                return;

            var context = BuildAIContext(action, hasTargetCoord: false);
            if (!CommandExecutor.Execute(new SupplyCommand(), context))
                supplierUnit.HasActed = true;
        }

        private void ExecuteProduce(AIPlannedAction action)
        {
            if (action.factory == null || action.produceUnitData == null)
                return;

            var building = action.factory.Building;
            if (building == null) return;

            var cell = building.Cell;
            if (cell == null) return;

            var context = BuildAIContext(action, hasTargetCoord: false);
            context.FactorySpawner = action.factory;
            context.ProduceUnitData = action.produceUnitData;
            context.SpawnCell = cell;

            CommandExecutor.Execute(new ProduceCommand(), context);
        }

        private static CommandContext BuildAIContext(AIPlannedAction action, bool hasTargetCoord)
        {
            var unit = action != null ? action.unit : null;
            var mapRoot = unit != null
                ? (unit.MapRoot != null ? unit.MapRoot : MapRoot.Instance)
                : MapRoot.Instance;

            return new CommandContext
            {
                Mode = CommandContext.ExecutionMode.AIImmediate,
                Unit = unit,
                TargetUnit = action != null ? action.targetUnit : null,
                CurrentCell = unit != null ? unit.CurrentCell : null,
                MapRoot = mapRoot,
                GridCoord = unit != null ? unit.GridCoord : default,
                TargetCoord = action != null ? action.targetCoord : default,
                HasTargetCoord = hasTargetCoord
            };
        }
    }
}
