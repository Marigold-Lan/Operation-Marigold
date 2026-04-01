using OperationMarigold.AI.Core;
using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI.BehaviorTree
{
    /// <summary>
    /// 组装完整的 AI 行为树结构。
    ///
    /// Root (Sequence)
    ///   1. EvaluateBattlefieldNode — 单位分桶 + 战场统计
    ///   2. EvaluateStrategicPostureNode — 宏观姿态（兵力/HQ 压力/经济/地产）
    ///   3. Selector
    ///        A. Sequence( Match(RaidCapture|DesperateRecovery), 占领优先管线 + 阶段间刷新姿态 )
    ///        B. Sequence( 标准：战斗 → 刷新 → 占领 → 再刷新 → 空闲 → 生产 )
    /// </summary>
    public static class AIBehaviorTreeBuilder
    {
        public static BTNode Build()
        {
            var root = new BTSequence();
            root.AddChild(new EvaluateBattlefieldNode());
            root.AddChild(new EvaluateStrategicPostureNode());

            var branch = new BTSelector();
            branch.AddChild(BuildCaptureFirstPipeline());
            branch.AddChild(BuildStandardPipeline());
            root.AddChild(branch);

            return root;
        }

        /// <summary>
        /// 劣势抢地产 / 绝境翻盘：先处理占领单位再处理战斗单位。
        /// </summary>
        private static BTSequence BuildCaptureFirstPipeline()
        {
            var seq = new BTSequence();
            seq.AddChild(new MatchStrategicPostureNode(
                AIStrategicPosture.RaidCapture,
                AIStrategicPosture.DesperateRecovery));
            seq.AddChild(AlwaysSucceed(BuildCaptureBlock()));
            seq.AddChild(new EvaluateStrategicPostureNode());
            // 在占领单位装载完成后，尽快执行后勤投送闭环
            seq.AddChild(AlwaysSucceed(BuildLogisticsBlock()));
            seq.AddChild(new EvaluateStrategicPostureNode());
            seq.AddChild(AlwaysSucceed(BuildCombatBlock()));
            seq.AddChild(AlwaysSucceed(BuildSupportCombatBlock()));
            seq.AddChild(AlwaysSucceed(BuildRangedStrikeBlock()));
            seq.AddChild(new EvaluateStrategicPostureNode());
            seq.AddChild(AlwaysSucceed(BuildIdleBlock()));
            seq.AddChild(AlwaysSucceed(BuildProductionBlock()));
            return seq;
        }

        private static BTSequence BuildStandardPipeline()
        {
            var seq = new BTSequence();
            seq.AddChild(AlwaysSucceed(BuildCombatBlock()));
            seq.AddChild(AlwaysSucceed(BuildSupportCombatBlock()));
            seq.AddChild(AlwaysSucceed(BuildRangedStrikeBlock()));
            seq.AddChild(AlwaysSucceed(BuildLogisticsBlock()));
            seq.AddChild(new EvaluateStrategicPostureNode());
            seq.AddChild(AlwaysSucceed(BuildCaptureBlock()));
            // 第二次后勤：在占领单位完成装载后，执行投送/补给闭环
            seq.AddChild(AlwaysSucceed(BuildLogisticsBlock()));
            seq.AddChild(new EvaluateStrategicPostureNode());
            seq.AddChild(AlwaysSucceed(BuildIdleBlock()));
            seq.AddChild(AlwaysSucceed(BuildProductionBlock()));
            return seq;
        }

        private static BTNode BuildCombatBlock()
        {
            var s = new BTSequence();
            s.AddChild(new HasUnprocessedUnitsNode(BlackboardKeys.AssaultMainUnits));
            s.AddChild(new ProcessCombatUnitsNode(BlackboardKeys.AssaultMainUnits));
            return s;
        }

        private static BTNode BuildSupportCombatBlock()
        {
            var s = new BTSequence();
            s.AddChild(new HasUnprocessedUnitsNode(BlackboardKeys.AssaultSupportUnits));
            s.AddChild(new ProcessCombatUnitsNode(BlackboardKeys.AssaultSupportUnits));
            return s;
        }

        private static BTNode BuildRangedStrikeBlock()
        {
            var s = new BTSequence();
            s.AddChild(new HasUnprocessedUnitsNode(BlackboardKeys.RangedStrikeUnits));
            s.AddChild(new ProcessCombatUnitsNode(BlackboardKeys.RangedStrikeUnits));
            return s;
        }

        private static BTNode BuildLogisticsBlock()
        {
            var s = new BTSequence();
            s.AddChild(new HasUnprocessedUnitsNode(BlackboardKeys.LogisticsUnits));
            s.AddChild(new ProcessCombatUnitsNode(BlackboardKeys.LogisticsUnits));
            return s;
        }

        private static BTNode BuildCaptureBlock()
        {
            var s = new BTSequence();
            s.AddChild(new HasCaptureTargetsNode());
            s.AddChild(new HasUnprocessedUnitsNode(BlackboardKeys.CaptureTeamUnits));
            s.AddChild(new ProcessCaptureUnitsNode(BlackboardKeys.CaptureTeamUnits));
            return s;
        }

        private static BTNode BuildIdleBlock()
        {
            var s = new BTSequence();
            s.AddChild(new HasUnprocessedUnitsNode(BlackboardKeys.IdleUnits));
            s.AddChild(new ProcessIdleUnitsNode());
            return s;
        }

        private static BTNode BuildProductionBlock()
        {
            var s = new BTSequence();
            s.AddChild(new CanAffordProductionNode());
            s.AddChild(new ProductionDecisionNode());
            return s;
        }

        private static BTNode AlwaysSucceed(BTNode child)
        {
            var decorator = new BTAlwaysSucceed();
            decorator.SetChild(child);
            return decorator;
        }
    }
}
