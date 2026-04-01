using UnityEngine;
using OperationMarigold.MinimaxFramework;
using OperationMarigold.AI.Core;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.Minimax
{
    /// <summary>
    /// 局面评估函数。权重从 AIDifficultyProfile 读取。
    /// 分数正值有利于 AI 玩家，负值有利于对手。
    /// </summary>
    public class AIHeuristic : IHeuristic
    {
        private readonly int _aiPlayerId;
        private readonly AIDifficultyProfile _profile;
        private readonly AIStrategyContext _strategy;
        private readonly System.Random _rng;

        public AIHeuristic(int aiPlayerId, AIDifficultyProfile profile, AIStrategyContext strategy = null)
        {
            _aiPlayerId = aiPlayerId;
            _profile = profile;
            _strategy = strategy;
            _rng = profile != null && profile.randomNoisePercent > 0
                ? new System.Random()
                : null;
        }

        public int CalculateStateScore(IGameState state, SearchNode node)
        {
            var board = (AIBoardState)state;
            int opponentId = board.GetOpponentPlayerId(_aiPlayerId);

            float score = 0f;
            var strat = _strategy ?? AIStrategyContext.Neutral;

            // 单位价值
            float myUnitValue = 0f, enemyUnitValue = 0f;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive) continue;
                float value = (u.hp / (float)Mathf.Max(1, u.maxHp)) * u.cost;
                if ((int)u.faction == _aiPlayerId)
                    myUnitValue += value;
                else
                    enemyUnitValue += value;
            }
            score += (myUnitValue - enemyUnitValue) * Weight.unitValue * strat.UnitValueWeightMul;

            // 建筑控制
            float myBuildings = 0f, enemyBuildings = 0f;
            Vector2Int enemyHqPos = Vector2Int.zero;
            bool hasEnemyHq = false;

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                float w = b.isHq ? 3f : 1f;
                if ((int)b.ownerFaction == _aiPlayerId)
                    myBuildings += w;
                else if (b.ownerFaction != UnitFaction.None)
                    enemyBuildings += w;

                if (b.isHq && (int)b.ownerFaction == opponentId)
                {
                    enemyHqPos = b.gridCoord;
                    hasEnemyHq = true;
                }
            }

            // 统计占领进度（用“最大值”为主，避免多个在途同时累加导致分散占领）
            float bestOurCaptureProgress = 0f;
            float bestEnemyCaptureProgress = 0f;
            int ourInProgressCaptures = 0;
            int enemyInProgressCaptures = 0;

            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if (b.maxCaptureHp <= 0 || (int)b.ownerFaction == _aiPlayerId)
                    continue;

                float captureProgress = 1f - (b.captureHp / (float)b.maxCaptureHp);
                captureProgress = Mathf.Clamp01(captureProgress);
                if (captureProgress <= 0.0001f)
                    continue;

                int sign = 0;
                if (b.ownerFaction == UnitFaction.None)
                {
                    if (board.IsInBounds(b.gridCoord))
                    {
                        var cell = board.grid[b.gridCoord.x, b.gridCoord.y];
                        int ui = cell.unitIndex;
                        if (ui >= 0 && ui < board.units.Count)
                        {
                            var u = board.units[ui];
                            if (u.alive && u.IsOnMap)
                            {
                                if ((int)u.faction == _aiPlayerId) sign = 1;
                                else if ((int)u.faction == opponentId) sign = -1;
                            }
                        }
                    }
                }
                else
                {
                    sign = 1;
                }

                if (sign > 0)
                {
                    ourInProgressCaptures++;
                    bestOurCaptureProgress = Mathf.Max(bestOurCaptureProgress, captureProgress);
                }
                else if (sign < 0)
                {
                    enemyInProgressCaptures++;
                    bestEnemyCaptureProgress = Mathf.Max(bestEnemyCaptureProgress, captureProgress);
                }
            }

            score += (bestOurCaptureProgress - bestEnemyCaptureProgress) * Weight.captureProgress * strat.CaptureProgressWeightMul;
            // 多目标轻惩罚：同时推多个建筑时收益会被压制，让“持续占同一建筑”更显著。
            if (ourInProgressCaptures > 1)
                score -= (ourInProgressCaptures - 1) * Weight.captureProgress * 0.18f * strat.CaptureProgressWeightMul;
            if (enemyInProgressCaptures > 1)
                score -= (enemyInProgressCaptures - 1) * Weight.captureProgress * 0.10f * strat.CaptureProgressWeightMul;

            score += (myBuildings - enemyBuildings) * Weight.building * strat.BuildingWeightMul;

            // 位置价值：己方单位到敌方 HQ 的距离
            if (hasEnemyHq)
            {
                float posScore = 0f;
                for (int i = 0; i < board.units.Count; i++)
                {
                    var u = board.units[i];
                    if (!u.alive || !u.IsOnMap || (int)u.faction != _aiPlayerId) continue;
                    int dist = board.ManhattanDistance(u.gridCoord, enemyHqPos);
                    posScore -= dist;
                }
                score += posScore * Weight.position * 0.1f * strat.PositionWeightMul;
            }

            score += EvaluateCombatCohesion(board, opponentId) * 0.12f * strat.PositionWeightMul;
            score += EvaluateEnemyDensityPressure(board, opponentId) * 0.1f * strat.PositionWeightMul;
            score += EvaluateInfantryCapturePressure(board, opponentId) * 0.18f * strat.CaptureProgressWeightMul;

            if (strat.HqGuardWeight > 0f && strat.HasMyHq)
            {
                float guard = 0f;
                for (int i = 0; i < board.units.Count; i++)
                {
                    var u = board.units[i];
                    if (!u.alive || !u.IsOnMap || (int)u.faction != _aiPlayerId) continue;
                    guard -= board.ManhattanDistance(u.gridCoord, strat.MyHqCoord);
                }

                score += guard * strat.HqGuardWeight * 0.12f;
            }

            // 经济
            int myFunds = _aiPlayerId >= 0 && _aiPlayerId < board.funds.Length ? board.funds[_aiPlayerId] : 0;
            int enemyFunds = opponentId >= 0 && opponentId < board.funds.Length ? board.funds[opponentId] : 0;
            score += (myFunds - enemyFunds) * Weight.funds * 0.01f * strat.FundsWeightMul;

            // HQ 被夺 = 绝对输赢
            if (!hasEnemyHq)
                score += 50000;
            bool hasMyHq = false;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                if (board.buildings[i].isHq && (int)board.buildings[i].ownerFaction == _aiPlayerId)
                {
                    hasMyHq = true;
                    break;
                }
            }
            if (!hasMyHq)
                score -= 50000;

            // 随机扰动
            if (_rng != null && _profile.randomNoisePercent > 0)
            {
                float noise = (_rng.Next(0, _profile.randomNoisePercent * 2 + 1) - _profile.randomNoisePercent);
                score += noise;
            }

            return Mathf.RoundToInt(score);
        }

        public int CalculateActionScore(IGameState state, IAction action)
        {
            var a = (AIAction)action;
            var strat = _strategy ?? AIStrategyContext.Neutral;
            var board = state as AIBoardState;
            int opponentId = board != null ? board.GetOpponentPlayerId(_aiPlayerId) : -1;
            int s;
            switch (a.actionType)
            {
                case AIActionType.Attack:  s = 1000; break;
                case AIActionType.Capture: s = 800; break;
                case AIActionType.Move:    s = 400; break;
                case AIActionType.Supply:  s = 300; break;
                case AIActionType.Load:    s = 200; break;
                case AIActionType.Drop:    s = 200; break;
                case AIActionType.Wait:    s = 100; break;
                case AIActionType.EndTurn: s = 50; break;
                default: return 0;
            }

            if (a.actionType == AIActionType.Attack)
                s += strat.ActionAttackSortBonus;
            if (a.actionType == AIActionType.Capture)
            {
                s += strat.ActionCaptureSortBonus + Mathf.RoundToInt(70f * strat.FactoryCaptureUrgency);

                // 粘性占领：对“已有占领进度的目标建筑”给额外 bonus，
                // 让 Minimax 在剪枝时更倾向保留“继续打同一个建筑”的分支。
                float captureProgress = 0f;
                if (board != null && board.IsInBounds(a.targetCoord))
                {
                    var cell = board.grid[a.targetCoord.x, a.targetCoord.y];
                    int bIdx = cell.buildingIndex;
                    if (bIdx >= 0 && bIdx < board.buildings.Count)
                    {
                        var b = board.buildings[bIdx];
                        if (b.maxCaptureHp > 0)
                        {
                            captureProgress = 1f - (b.captureHp / (float)b.maxCaptureHp);
                            captureProgress = Mathf.Clamp01(captureProgress);
                        }
                    }
                }

                s += Mathf.RoundToInt(220f * captureProgress);

                if (board != null && board.IsInBounds(a.targetCoord))
                {
                    var cell = board.grid[a.targetCoord.x, a.targetCoord.y];
                    int bIdx = cell.buildingIndex;
                    if (bIdx >= 0 && bIdx < board.buildings.Count)
                    {
                        var b = board.buildings[bIdx];
                        bool isEnemyBuilding = (int)b.ownerFaction == opponentId;
                        bool isNeutral = b.ownerFaction == UnitFaction.None;
                        if (b.isHq && isEnemyBuilding)
                        {
                            s += 420;
                            int hqEnemyRing = CountEnemyUnitsInRadius(board, a.targetCoord, 3, opponentId);
                            if (hqEnemyRing == 0) s += 220;
                            else if (hqEnemyRing <= 1) s += 110;
                        }
                        else if (isEnemyBuilding)
                        {
                            s += b.isFactory ? 180 : 130;
                        }
                        else if (isNeutral)
                        {
                            s += 70;
                        }
                    }
                }
            }
            if (a.actionType == AIActionType.Load)
                s += strat.ActionLoadSortBonus + Mathf.RoundToInt(80f * strat.FrontlineLogisticsPressure);
            if (a.actionType == AIActionType.Drop)
                s += strat.ActionDropSortBonus + Mathf.RoundToInt(95f * strat.FrontlineLogisticsPressure);
            if (a.actionType == AIActionType.Supply)
                s += strat.ActionSupplySortBonus + Mathf.RoundToInt(120f * strat.FrontlineLogisticsPressure);

            if (board != null && a.unitIndex >= 0 && a.unitIndex < board.units.Count)
            {
                var actor = board.units[a.unitIndex];
                bool loadedTransport = actor.transportCapacity > 0 && board.CountEmbarkedCargo(a.unitIndex) > 0;
                if (loadedTransport && (a.actionType == AIActionType.Move || a.actionType == AIActionType.Drop))
                {
                    s += EvaluateBuildingApproachActionBonus(board, a.targetCoord, opponentId);
                }
                if (loadedTransport && a.actionType == AIActionType.Wait)
                {
                    // APC 载员时应尽快投送，弱化“原地空转”分支。
                    s -= 90;
                }
            }
            return s;
        }

        public int CalculateActionSort(IGameState state, IAction action)
        {
            var a = (AIAction)action;
            return a.unitIndex * 100 + (int)a.actionType;
        }

        /// <summary>
        /// 权重辅助，从 profile 读取并缓存。
        /// </summary>
        private AIDifficultyProfile.Weights Weight =>
            _profile != null ? _profile.weights : AIDifficultyProfile.Weights.Default;

        private float EvaluateCombatCohesion(AIBoardState board, int opponentId)
        {
            long sx = 0, sy = 0, n = 0;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != _aiPlayerId)
                    continue;
                if (!(u.hasPrimaryWeapon || u.hasSecondaryWeapon))
                    continue;
                sx += u.gridCoord.x;
                sy += u.gridCoord.y;
                n++;
            }

            if (n <= 1)
                return 0f;

            var centroid = new Vector2Int((int)(sx / n), (int)(sy / n));
            float cohesion = 0f;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != _aiPlayerId)
                    continue;
                if (!(u.hasPrimaryWeapon || u.hasSecondaryWeapon))
                    continue;

                int d = board.ManhattanDistance(u.gridCoord, centroid);
                cohesion -= d * 0.8f;
                if (u.category == UnitCategory.Vehicle)
                    cohesion -= d * 0.4f;
            }

            return cohesion;
        }

        private float EvaluateEnemyDensityPressure(AIBoardState board, int opponentId)
        {
            float pressure = 0f;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != _aiPlayerId)
                    continue;
                if (!(u.hasPrimaryWeapon || u.hasSecondaryWeapon))
                    continue;

                int localEnemies = CountEnemyUnitsInRadius(board, u.gridCoord, 3, opponentId);
                if (localEnemies <= 0)
                    continue;

                float perUnit = Mathf.Min(4f, localEnemies) * 4.5f;
                if (u.category == UnitCategory.Vehicle)
                    perUnit *= 1.25f;
                pressure += perUnit;
            }

            return pressure;
        }

        private float EvaluateInfantryCapturePressure(AIBoardState board, int opponentId)
        {
            float total = 0f;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != _aiPlayerId || u.category != UnitCategory.Soldier)
                    continue;

                float best = float.MinValue;
                for (int bIdx = 0; bIdx < board.buildings.Count; bIdx++)
                {
                    var b = board.buildings[bIdx];
                    if ((int)b.ownerFaction == _aiPlayerId)
                        continue;

                    float ownerPriority = (int)b.ownerFaction == opponentId ? 2f : 1f;
                    int d = board.ManhattanDistance(u.gridCoord, b.gridCoord);
                    float local = ownerPriority * 45f - d * 6f;
                    if (b.isHq && (int)b.ownerFaction == opponentId)
                    {
                        local += 160f;
                        int ring = CountEnemyUnitsInRadius(board, b.gridCoord, 3, opponentId);
                        if (ring == 0) local += 120f;
                        else if (ring <= 1) local += 60f;
                    }
                    else if (b.isFactory && (int)b.ownerFaction == opponentId)
                    {
                        local += 45f;
                    }

                    if (local > best)
                        best = local;
                }

                if (best > float.MinValue / 2f)
                    total += best;
            }

            return total;
        }

        private int EvaluateBuildingApproachActionBonus(AIBoardState board, Vector2Int coord, int opponentId)
        {
            float best = float.MinValue;
            for (int i = 0; i < board.buildings.Count; i++)
            {
                var b = board.buildings[i];
                if ((int)b.ownerFaction == _aiPlayerId)
                    continue;

                float ownerPriority = (int)b.ownerFaction == opponentId ? 2f :
                    (b.ownerFaction == UnitFaction.None ? 1.2f : 0.6f);
                int dist = board.ManhattanDistance(coord, b.gridCoord);
                float score = ownerPriority * 110f - dist * 9f;
                if (b.isHq && (int)b.ownerFaction == opponentId)
                    score += 80f;
                else if (b.isFactory)
                    score += 25f;
                if (score > best)
                    best = score;
            }

            return best > float.MinValue / 2f ? Mathf.RoundToInt(best) : 0;
        }

        private static int CountEnemyUnitsInRadius(AIBoardState board, Vector2Int center, int radius, int enemyFaction)
        {
            int count = 0;
            for (int i = 0; i < board.units.Count; i++)
            {
                var u = board.units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != enemyFaction)
                    continue;
                if (board.ManhattanDistance(center, u.gridCoord) <= radius)
                    count++;
            }

            return count;
        }
    }
}
