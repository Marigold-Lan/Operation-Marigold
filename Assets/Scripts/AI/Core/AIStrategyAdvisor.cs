using OperationMarigold.AI.Simulation;
using UnityEngine;

namespace OperationMarigold.AI.Core
{
    /// <summary>
    /// 根据棋盘快照推导本回合战略姿态与修饰系数。
    /// </summary>
    public static class AIStrategyAdvisor
    {
        private const float ForceWinRatio = 1.18f;
        private const float ForceCrushRatio = 0.38f;
        private const int HqThreatRadius = 9;
        private const float FundsRichRatio = 1.35f;
        private const int BuildingBehindRaid = 2;

        public static AIStrategyContext Compute(AIBoardState board, int ourFaction, int enemyFaction)
        {
            float ourForce = 0f, enemyForce = 0f;
            int ourUnits = 0, enemyUnits = 0;
            int ourBuildings = 0, enemyBuildings = 0;

            Vector2Int myHq = default;
            bool hasMyHq = false;
            Vector2Int enemyHq = default;
            bool hasEnemyHq = false;

            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive) continue;
                float f = (u.hp / (float)Mathf.Max(1, u.maxHp)) * u.cost;
                if ((int)u.faction == ourFaction)
                {
                    ourForce += f;
                    ourUnits++;
                }
                else if ((int)u.faction == enemyFaction)
                {
                    enemyForce += f;
                    enemyUnits++;
                }
            }

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if (b.ownerFaction == UnitFaction.None) continue;
                float w = b.isHq ? 3f : 1f;
                if ((int)b.ownerFaction == ourFaction)
                    ourBuildings += Mathf.RoundToInt(w);
                else if ((int)b.ownerFaction == enemyFaction)
                    enemyBuildings += Mathf.RoundToInt(w);

                if (b.isHq)
                {
                    if ((int)b.ownerFaction == ourFaction)
                    {
                        myHq = b.gridCoord;
                        hasMyHq = true;
                    }
                    else if ((int)b.ownerFaction == enemyFaction)
                    {
                        enemyHq = b.gridCoord;
                        hasEnemyHq = true;
                    }
                }
            }

            int ourFunds = ourFaction >= 0 && ourFaction < board.funds.Length ? board.funds[ourFaction] : 0;
            int enemyFunds = enemyFaction >= 0 && enemyFaction < board.funds.Length ? board.funds[enemyFaction] : 0;

            int enemyNearMyHq = 0;
            if (hasMyHq)
            {
                for (int i = 0; i < board.units.Count; i++)
                {
                    var u = board.units[i];
                    if (!u.alive || (int)u.faction != enemyFaction) continue;
                    if (board.ManhattanDistance(u.gridCoord, myHq) <= HqThreatRadius)
                        enemyNearMyHq++;
                }
            }

            float adv = ourForce - enemyForce;
            bool lowUnitCount = ourUnits <= 2 && enemyUnits >= 3;
            bool crushed = enemyForce > 1f && ourForce < enemyForce * ForceCrushRatio;
            bool winning = ourForce > enemyForce * ForceWinRatio;
            bool hqPanic = hasMyHq && enemyNearMyHq >= 2;
            bool hqPressure = hasMyHq && enemyNearMyHq >= 1;
            bool propertyBehind = enemyBuildings >= ourBuildings + BuildingBehindRaid;
            bool fundsRich = enemyFunds > 0 && ourFunds > enemyFunds * FundsRichRatio;
            bool fundsTight = ourFunds < 1500 && ourFunds < enemyFunds * 0.65f;

            AIStrategicPosture posture;
            if (crushed || lowUnitCount || hqPanic)
                posture = AIStrategicPosture.DesperateRecovery;
            else if (hqPressure && !winning)
                posture = AIStrategicPosture.DefensiveHold;
            else if ((propertyBehind || fundsTight) && !winning)
                posture = AIStrategicPosture.RaidCapture;
            else if (winning && !hqPressure)
                posture = AIStrategicPosture.AggressivePush;
            else if (fundsRich && adv >= -enemyForce * 0.15f)
                posture = AIStrategicPosture.EconomicExpansion;
            else
                posture = AIStrategicPosture.Balanced;

            return BuildContext(
                posture,
                hasMyHq,
                myHq,
                enemyNearMyHq,
                hasEnemyHq,
                enemyHq,
                ourFunds,
                fundsTight);
        }

        private static AIStrategyContext BuildContext(
            AIStrategicPosture posture,
            bool hasMyHq,
            Vector2Int myHq,
            int enemyNearMyHq,
            bool hasEnemyHq,
            Vector2Int enemyHq,
            int ourFunds,
            bool fundsTight)
        {
            float uv = 1f, bld = 1f, cap = 1f, pos = 1f, funds = 1f;
            float hqGuard = 0f;
            int atkBonus = 0, capBonus = 0;
            int loadBonus = 40, dropBonus = 40, supplyBonus = 50;
            float solMul = 1f, vehMul = 1f;
            float cheapBias = 0f;
            float reserveMul = 1f;
            float factoryCaptureUrgency = 1f;
            float logisticsPressure = 0.25f;
            int rangedSafetyDistance = 3;
            IdleStrategicFocus focus = IdleStrategicFocus.PushEnemyHq;

            switch (posture)
            {
                case AIStrategicPosture.AggressivePush:
                    uv = 1.05f;
                    pos = 1.45f;
                    cap = 0.85f;
                    atkBonus = 120;
                    capBonus = 40;
                    loadBonus = 140;
                    dropBonus = 220;
                    solMul = 0.92f;
                    vehMul = 1.12f;
                    reserveMul = 1.18f;
                    logisticsPressure = 0.4f;
                    rangedSafetyDistance = 4;
                    focus = hasEnemyHq ? IdleStrategicFocus.PushEnemyHq : IdleStrategicFocus.HarassFrontline;
                    break;

                case AIStrategicPosture.DefensiveHold:
                    uv = 1.12f;
                    bld = 1.25f;
                    pos = 0.55f;
                    cap = 1.05f;
                    hqGuard = enemyNearMyHq > 0 ? 2.2f : 1.4f;
                    atkBonus = 180;
                    capBonus = 20;
                    supplyBonus = 200;
                    solMul = 0.85f;
                    vehMul = 1.2f;
                    reserveMul = 1.3f;
                    logisticsPressure = 0.8f;
                    rangedSafetyDistance = 4;
                    focus = hasMyHq ? IdleStrategicFocus.DefendMyHq : IdleStrategicFocus.HarassFrontline;
                    break;

                case AIStrategicPosture.EconomicExpansion:
                    bld = 1.35f;
                    cap = 1.25f;
                    funds = 1.4f;
                    pos = 0.9f;
                    capBonus = 100;
                    atkBonus = 60;
                    loadBonus = 180;
                    dropBonus = 200;
                    supplyBonus = 180;
                    solMul = 1.08f;
                    vehMul = 1.05f;
                    reserveMul = 1.08f;
                    factoryCaptureUrgency = 1.35f;
                    logisticsPressure = 0.6f;
                    focus = IdleStrategicFocus.SecureIncome;
                    break;

                case AIStrategicPosture.DesperateRecovery:
                    uv = 1.18f;
                    bld = 1.15f;
                    cap = 1.5f;
                    pos = 0.65f;
                    funds = 1.15f;
                    hqGuard = 2.8f;
                    capBonus = 220;
                    atkBonus = 90;
                    loadBonus = 260;
                    dropBonus = 280;
                    supplyBonus = 160;
                    solMul = 1.25f;
                    vehMul = 0.95f;
                    cheapBias = fundsTight ? 0.35f : 0.15f;
                    reserveMul = 0.92f;
                    factoryCaptureUrgency = 1.6f;
                    logisticsPressure = 0.85f;
                    focus = hasMyHq ? IdleStrategicFocus.DefendMyHq : IdleStrategicFocus.SecureIncome;
                    break;

                case AIStrategicPosture.RaidCapture:
                    cap = 1.55f;
                    bld = 1.2f;
                    pos = 0.85f;
                    capBonus = 160;
                    atkBonus = 70;
                    loadBonus = 240;
                    dropBonus = 300;
                    solMul = 1.22f;
                    vehMul = 0.92f;
                    cheapBias = fundsTight ? 0.25f : 0f;
                    reserveMul = 0.95f;
                    factoryCaptureUrgency = 1.85f;
                    logisticsPressure = 0.75f;
                    focus = IdleStrategicFocus.SecureIncome;
                    break;

                default:
                    atkBonus = 40;
                    capBonus = 60;
                    loadBonus = 80;
                    dropBonus = 100;
                    supplyBonus = 80;
                    break;
            }

            return new AIStrategyContext(
                posture,
                uv, bld, cap, pos, funds,
                hqGuard,
                hasMyHq, myHq,
                atkBonus, capBonus,
                loadBonus, dropBonus, supplyBonus,
                solMul, vehMul, cheapBias,
                reserveMul, factoryCaptureUrgency, logisticsPressure, rangedSafetyDistance,
                focus);
        }
    }
}
