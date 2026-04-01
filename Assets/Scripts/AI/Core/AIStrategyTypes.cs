using UnityEngine;

namespace OperationMarigold.AI.Core
{
    /// <summary>
    /// 本回合 AI 宏观姿态（类似高级战争：根据兵力、经济、HQ 压力切换打法）。
    /// </summary>
    public enum AIStrategicPosture
    {
        Balanced,
        AggressivePush,
        DefensiveHold,
        EconomicExpansion,
        DesperateRecovery,
        RaidCapture
    }

    /// <summary>
    /// 空闲单位战略目标：影响向哪一格推进。
    /// </summary>
    public enum IdleStrategicFocus
    {
        PushEnemyHq,
        DefendMyHq,
        SecureIncome,
        HarassFrontline
    }

    /// <summary>
    /// 不可变策略上下文，供启发式 / 生产 / 空闲移动读取（可在后台线程读）。
    /// </summary>
    public sealed class AIStrategyContext
    {
        public AIStrategicPosture Posture { get; }
        public float UnitValueWeightMul { get; }
        public float BuildingWeightMul { get; }
        public float CaptureProgressWeightMul { get; }
        public float PositionWeightMul { get; }
        public float FundsWeightMul { get; }
        public float HqGuardWeight { get; }
        public bool HasMyHq { get; }
        public Vector2Int MyHqCoord { get; }
        public int ActionAttackSortBonus { get; }
        public int ActionCaptureSortBonus { get; }
        public int ActionLoadSortBonus { get; }
        public int ActionDropSortBonus { get; }
        public int ActionSupplySortBonus { get; }
        public float ProductionSoldierMul { get; }
        public float ProductionVehicleMul { get; }
        public float ProductionCheapBias { get; }
        public float ProductionReserveMul { get; }
        public float FactoryCaptureUrgency { get; }
        public float FrontlineLogisticsPressure { get; }
        public int RangedSafetyDistance { get; }
        public IdleStrategicFocus IdleFocus { get; }

        public AIStrategyContext(
            AIStrategicPosture posture,
            float unitValueWeightMul,
            float buildingWeightMul,
            float captureProgressWeightMul,
            float positionWeightMul,
            float fundsWeightMul,
            float hqGuardWeight,
            bool hasMyHq,
            Vector2Int myHqCoord,
            int actionAttackSortBonus,
            int actionCaptureSortBonus,
            int actionLoadSortBonus,
            int actionDropSortBonus,
            int actionSupplySortBonus,
            float productionSoldierMul,
            float productionVehicleMul,
            float productionCheapBias,
            float productionReserveMul,
            float factoryCaptureUrgency,
            float frontlineLogisticsPressure,
            int rangedSafetyDistance,
            IdleStrategicFocus idleFocus)
        {
            Posture = posture;
            UnitValueWeightMul = unitValueWeightMul;
            BuildingWeightMul = buildingWeightMul;
            CaptureProgressWeightMul = captureProgressWeightMul;
            PositionWeightMul = positionWeightMul;
            FundsWeightMul = fundsWeightMul;
            HqGuardWeight = hqGuardWeight;
            HasMyHq = hasMyHq;
            MyHqCoord = myHqCoord;
            ActionAttackSortBonus = actionAttackSortBonus;
            ActionCaptureSortBonus = actionCaptureSortBonus;
            ActionLoadSortBonus = actionLoadSortBonus;
            ActionDropSortBonus = actionDropSortBonus;
            ActionSupplySortBonus = actionSupplySortBonus;
            ProductionSoldierMul = productionSoldierMul;
            ProductionVehicleMul = productionVehicleMul;
            ProductionCheapBias = productionCheapBias;
            ProductionReserveMul = productionReserveMul;
            FactoryCaptureUrgency = factoryCaptureUrgency;
            FrontlineLogisticsPressure = frontlineLogisticsPressure;
            RangedSafetyDistance = rangedSafetyDistance;
            IdleFocus = idleFocus;
        }

        public static AIStrategyContext Neutral =>
            new AIStrategyContext(
                AIStrategicPosture.Balanced,
                1f, 1f, 1f, 1f, 1f,
                0f, false, default,
                0, 0, 0, 0, 0,
                1f, 1f, 0f,
                1f, 1f, 0f, 3,
                IdleStrategicFocus.PushEnemyHq);
    }
}
