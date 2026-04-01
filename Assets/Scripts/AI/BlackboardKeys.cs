using OperationMarigold.BehaviorTreeFramework;

namespace OperationMarigold.AI
{
    /// <summary>
    /// AI 行为树黑板键常量。所有键均通过 Blackboard.Key() 预计算哈希，避免运行时开销。
    /// </summary>
    public static class BlackboardKeys
    {
        public static readonly int BoardState        = Blackboard.Key("BoardState");
        public static readonly int OurFaction         = Blackboard.Key("OurFaction");
        public static readonly int EnemyFaction       = Blackboard.Key("EnemyFaction");
        public static readonly int DifficultyProfile  = Blackboard.Key("DifficultyProfile");
        public static readonly int ActionQueue        = Blackboard.Key("ActionQueue");

        public static readonly int CombatUnits        = Blackboard.Key("CombatUnits");
        public static readonly int CaptureUnits       = Blackboard.Key("CaptureUnits");
        public static readonly int IdleUnits          = Blackboard.Key("IdleUnits");
        public static readonly int Factories          = Blackboard.Key("Factories");

        public static readonly int BattlefieldAdvantage = Blackboard.Key("BattlefieldAdvantage");
        public static readonly int OurFunds           = Blackboard.Key("OurFunds");
        public static readonly int EnemyFunds         = Blackboard.Key("EnemyFunds");
        public static readonly int OurUnitCount       = Blackboard.Key("OurUnitCount");
        public static readonly int EnemyUnitCount     = Blackboard.Key("EnemyUnitCount");
        public static readonly int OurBuildingCount   = Blackboard.Key("OurBuildingCount");
        public static readonly int EnemyBuildingCount = Blackboard.Key("EnemyBuildingCount");

        public static readonly int MapRoot            = Blackboard.Key("MapRoot");
        public static readonly int CheapestUnitCost   = Blackboard.Key("CheapestUnitCost");
        public static readonly int ReserveFunds       = Blackboard.Key("ReserveFunds");

        public static readonly int AssaultMainUnits   = Blackboard.Key("AssaultMainUnits");
        public static readonly int AssaultSupportUnits = Blackboard.Key("AssaultSupportUnits");
        public static readonly int CaptureTeamUnits   = Blackboard.Key("CaptureTeamUnits");
        public static readonly int LogisticsUnits     = Blackboard.Key("LogisticsUnits");
        public static readonly int RangedStrikeUnits  = Blackboard.Key("RangedStrikeUnits");

        /// <summary>当前回合 <see cref="OperationMarigold.AI.Core.AIStrategicPosture"/> 整型值。</summary>
        public static readonly int StrategicPosture     = Blackboard.Key("StrategicPosture");
        public static readonly int StrategyContext      = Blackboard.Key("StrategyContext");
    }
}
