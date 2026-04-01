using System;
using System.Collections.Generic;
using UnityEngine;
using OperationMarigold.MinimaxFramework;

namespace OperationMarigold.AI.Simulation
{
    /// <summary>
    /// 纯数据棋盘快照，实现 IGameState 供 MinimaxEngine 搜索。
    /// 所有字段均为值类型数组或简单集合，Clone() 高效。
    /// </summary>
    public class AIBoardState : IGameState
    {
        public int width;
        public int height;

        public AICellSnapshot[,] grid;
        public List<AIUnitSnapshot> units;
        public List<AIBuildingSnapshot> buildings;

        /// <summary>资金数组，索引 = (int)UnitFaction。</summary>
        public int[] funds;

        /// <summary>当前行动玩家 ID（== (int)UnitFaction）。</summary>
        public int currentPlayerId;

        /// <summary>
        /// 伤害矩阵查找表（所有克隆体共享同一只读实例）。
        /// </summary>
        public DamageMatrixLookup damageMatrix;

        // ─── IGameState ────────────────────────────────────

        public IGameState Clone()
        {
            var clone = new AIBoardState();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(AIBoardState source)
        {
            if (source == null)
                return;

            width = source.width;
            height = source.height;
            currentPlayerId = source.currentPlayerId;
            damageMatrix = source.damageMatrix; // 共享只读

            if (grid == null || grid.GetLength(0) != width || grid.GetLength(1) != height)
                grid = new AICellSnapshot[width, height];
            Array.Copy(source.grid, grid, source.grid.Length);

            if (units == null)
                units = new List<AIUnitSnapshot>(source.units.Count);
            else
                units.Clear();
            if (units.Capacity < source.units.Count)
                units.Capacity = source.units.Count;
            for (int i = 0; i < source.units.Count; i++)
                units.Add(source.units[i]);

            if (buildings == null)
                buildings = new List<AIBuildingSnapshot>(source.buildings.Count);
            else
                buildings.Clear();
            if (buildings.Capacity < source.buildings.Count)
                buildings.Capacity = source.buildings.Count;
            for (int i = 0; i < source.buildings.Count; i++)
                buildings.Add(source.buildings[i]);

            if (funds == null || funds.Length != source.funds.Length)
                funds = new int[source.funds.Length];
            Array.Copy(source.funds, funds, source.funds.Length);
        }

        public int GetCurrentPlayerId() => currentPlayerId;

        public bool HasEnded()
        {
            bool marigoldHasHq = false;
            bool lancelHasHq = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (!buildings[i].isHq) continue;
                if (buildings[i].ownerFaction == UnitFaction.Marigold) marigoldHasHq = true;
                if (buildings[i].ownerFaction == UnitFaction.Lancel) lancelHasHq = true;
            }
            return !marigoldHasHq || !lancelHasHq;
        }

        public int GetOpponentPlayerId(int playerId)
        {
            return playerId == (int)UnitFaction.Marigold
                ? (int)UnitFaction.Lancel
                : (int)UnitFaction.Marigold;
        }

        // ─── 辅助查询 ──────────────────────────────────────

        public ref AICellSnapshot GetCell(int x, int y) => ref grid[x, y];
        public ref AICellSnapshot GetCell(Vector2Int coord) => ref grid[coord.x, coord.y];

        public bool IsInBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        public bool IsInBounds(Vector2Int c) => IsInBounds(c.x, c.y);

        public bool IsValidDropCell(Vector2Int coord, AIUnitSnapshot cargo)
        {
            if (!IsInBounds(coord))
                return false;
            ref var cell = ref grid[coord.x, coord.y];
            if (cell.unitIndex >= 0)
                return false;
            int cost = AIMovementCostProvider.GetCost((AITerrainKind)cell.terrainKind, cargo.movementType);
            return cost >= 0;
        }

        public int ManhattanDistance(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>
        /// 查找指定阵营的第一个未行动单位索引。-1 表示全部已行动。
        /// </summary>
        public int FindFirstIdleUnit(int playerId)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.alive || !u.IsOnMap || (int)u.faction != playerId || u.hasActed)
                    continue;
                return i;
            }
            return -1;
        }

        public int CountEmbarkedCargo(int transporterIdx)
        {
            if (transporterIdx < 0 || transporterIdx >= units.Count)
                return 0;
            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.alive && u.embarkedOnUnitIndex == transporterIdx)
                    n++;
            }

            return n;
        }

        /// <summary>
        /// 更新格子上的 unitIndex 映射（移动后需调用）。
        /// </summary>
        public void MoveUnitOnGrid(int unitIdx, Vector2Int from, Vector2Int to)
        {
            if (IsInBounds(from))
            {
                ref var oldCell = ref grid[from.x, from.y];
                if (oldCell.unitIndex == unitIdx)
                    oldCell.unitIndex = -1;
            }
            if (IsInBounds(to))
                grid[to.x, to.y].unitIndex = unitIdx;
        }

        public void ResetCaptureProgressIfCapturerLeft(int unitIdx, Vector2Int fromCoord)
        {
            if (!IsInBounds(fromCoord))
                return;

            ref var oldCell = ref grid[fromCoord.x, fromCoord.y];
            int buildingIndex = oldCell.buildingIndex;
            if (buildingIndex < 0 || buildingIndex >= buildings.Count)
                return;

            var building = buildings[buildingIndex];
            if (building.captureActorUnitIndex != unitIdx)
                return;

            building.captureActorUnitIndex = -1;
            building.captureHp = building.maxCaptureHp;
            buildings[buildingIndex] = building;
        }

        /// <summary>
        /// 将指定单位标记为死亡，并从格子移除。
        /// </summary>
        public void KillUnit(int unitIdx)
        {
            if (unitIdx < 0 || unitIdx >= units.Count)
                return;

            var u = units[unitIdx];
            u.alive = false;
            u.hp = 0;
            units[unitIdx] = u;

            if (u.IsOnMap && IsInBounds(u.gridCoord))
            {
                ref var cell = ref grid[u.gridCoord.x, u.gridCoord.y];
                if (cell.unitIndex == unitIdx)
                    cell.unitIndex = -1;
            }

            for (int b = 0; b < buildings.Count; b++)
            {
                var building = buildings[b];
                if (building.captureActorUnitIndex != unitIdx)
                    continue;
                building.captureActorUnitIndex = -1;
                building.captureHp = building.maxCaptureHp;
                buildings[b] = building;
            }

            // 运输单位被消灭时，搭载的单位一并消灭（与 UnitTransport 行为一致）。
            for (int i = 0; i < units.Count; i++)
            {
                var c = units[i];
                if (!c.alive || c.embarkedOnUnitIndex != unitIdx)
                    continue;
                c.alive = false;
                c.hp = 0;
                c.embarkedOnUnitIndex = -1;
                units[i] = c;
            }
        }
    }
}
