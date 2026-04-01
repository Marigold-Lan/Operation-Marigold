using UnityEngine;
using OperationMarigold.MinimaxFramework;
using OperationMarigold.AI.Simulation;

namespace OperationMarigold.AI.Minimax
{
    /// <summary>
    /// 在 AIBoardState 上纯数据模拟动作执行，不涉及 MonoBehaviour / 动画。
    /// </summary>
    public class AIGameLogic : IGameLogic
    {
        private AIBoardState _state;

        public void SetState(IGameState state)
        {
            _state = (AIBoardState)state;
        }

        public void ExecuteAction(IAction action, int playerId)
        {
            var a = (AIAction)action;
            switch (a.actionType)
            {
                case AIActionType.Move:    ExecuteMove(a);    break;
                case AIActionType.Attack:  ExecuteAttack(a);  break;
                case AIActionType.Capture: ExecuteCapture(a); break;
                case AIActionType.Wait:    ExecuteWait(a);    break;
                case AIActionType.EndTurn: ExecuteEndTurn(playerId); break;
                case AIActionType.Load:    ExecuteLoad(a);    break;
                case AIActionType.Drop:    ExecuteDrop(a);    break;
                case AIActionType.Supply:  ExecuteSupply(a);  break;
            }
        }

        public void ClearResolve() { }

        // ─── 动作实现 ──────────────────────────────────────

        private void ExecuteMove(AIAction a)
        {
            var u = _state.units[a.unitIndex];
            var from = u.gridCoord;
            var to = a.targetCoord;

            if (from != to)
                _state.ResetCaptureProgressIfCapturerLeft(a.unitIndex, from);

            if (!MovementRulesShared.TryGetSnapshotMinTraversalCost(_state, u, a.unitIndex, from, to, out var fuelCost))
                fuelCost = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);

            u.fuel = Mathf.Max(0, u.fuel - Mathf.Max(0, fuelCost));
            u.gridCoord = to;
            u.hasMovedThisTurn = true;
            _state.units[a.unitIndex] = u;
            _state.MoveUnitOnGrid(a.unitIndex, from, to);
            SyncEmbarkedCargoCoords(a.unitIndex);
        }

        private void SyncEmbarkedCargoCoords(int transporterIdx)
        {
            var t = _state.units[transporterIdx];
            for (int i = 0; i < _state.units.Count; i++)
            {
                var c = _state.units[i];
                if (!c.alive || c.embarkedOnUnitIndex != transporterIdx)
                    continue;
                c.gridCoord = t.gridCoord;
                _state.units[i] = c;
            }
        }

        private void ExecuteAttack(AIAction a)
        {
            var attacker = _state.units[a.unitIndex];
            var defender = _state.units[a.targetUnitIndex];

            bool usePrimary = a.weaponSlot == 0;
            int distance = _state.ManhattanDistance(attacker.gridCoord, defender.gridCoord);

            int attackDamage = CombatRulesShared.CalculateSnapshotDamage(_state, attacker, defender, usePrimary);
            defender.hp = Mathf.Max(0, defender.hp - attackDamage);

            if (usePrimary)
                attacker.ammo = Mathf.Max(0, attacker.ammo - 1);

            attacker.hasActed = true;

            if (defender.hp <= 0)
            {
                _state.units[a.unitIndex] = attacker;
                _state.KillUnit(a.targetUnitIndex);
                return;
            }

            // 反击
            if (CombatRulesShared.CanSnapshotAttack(defender, attacker, distance, out bool defUsePrimary))
            {
                int counterDamage = CombatRulesShared.CalculateSnapshotDamage(_state, defender, attacker, defUsePrimary);
                attacker.hp = Mathf.Max(0, attacker.hp - counterDamage);
                if (defUsePrimary)
                    defender.ammo = Mathf.Max(0, defender.ammo - 1);
            }

            _state.units[a.unitIndex] = attacker;
            _state.units[a.targetUnitIndex] = defender;

            if (attacker.hp <= 0)
                _state.KillUnit(a.unitIndex);
            if (defender.hp <= 0)
                _state.KillUnit(a.targetUnitIndex);
        }

        private void ExecuteCapture(AIAction a)
        {
            var u = _state.units[a.unitIndex];
            var cell = _state.GetCell(u.gridCoord);
            if (cell.buildingIndex < 0) return;
            if (cell.buildingIndex >= _state.buildings.Count)
                return;

            var b = _state.buildings[cell.buildingIndex];
            if (!CaptureRulesShared.ApplySnapshotCapture(ref b, ref u, a.unitIndex))
                return;
            _state.buildings[cell.buildingIndex] = b;
            _state.units[a.unitIndex] = u;
        }

        private void ExecuteWait(AIAction a)
        {
            var u = _state.units[a.unitIndex];
            u.hasActed = true;
            _state.units[a.unitIndex] = u;
        }

        private void ExecuteEndTurn(int playerId)
        {
            TurnEconomyRulesShared.ApplySnapshotEndTurn(_state, playerId);
        }

        /// <summary>unitIndex = 步兵/机甲货物，targetUnitIndex = 运输单位。</summary>
        private void ExecuteLoad(AIAction a)
        {
            int cargoIdx = a.unitIndex;
            int transIdx = a.targetUnitIndex;
            if (cargoIdx < 0 || transIdx < 0 || cargoIdx >= _state.units.Count || transIdx >= _state.units.Count)
                return;

            var cargo = _state.units[cargoIdx];
            var trans = _state.units[transIdx];
            if (!TransportRulesShared.CanSnapshotLoad(_state, cargoIdx, transIdx))
                return;

            _state.ResetCaptureProgressIfCapturerLeft(cargoIdx, cargo.gridCoord);
            if (_state.IsInBounds(cargo.gridCoord))
            {
                ref var cell = ref _state.grid[cargo.gridCoord.x, cargo.gridCoord.y];
                if (cell.unitIndex == cargoIdx)
                    cell.unitIndex = -1;
            }

            cargo.embarkedOnUnitIndex = transIdx;
            cargo.gridCoord = trans.gridCoord;
            cargo.hasActed = true;
            _state.units[cargoIdx] = cargo;
            _state.units[transIdx] = trans;
        }

        /// <summary>unitIndex = 运输单位，targetUnitIndex = 货物，targetCoord = 卸载格。</summary>
        private void ExecuteDrop(AIAction a)
        {
            int transIdx = a.unitIndex;
            int cargoIdx = a.targetUnitIndex;
            Vector2Int drop = a.targetCoord;
            if (transIdx < 0 || cargoIdx < 0 || transIdx >= _state.units.Count || cargoIdx >= _state.units.Count)
                return;

            var trans = _state.units[transIdx];
            var cargo = _state.units[cargoIdx];
            if (!TransportRulesShared.CanSnapshotDrop(_state, transIdx, cargoIdx, drop))
                return;

            ref var dest = ref _state.grid[drop.x, drop.y];
            cargo.embarkedOnUnitIndex = -1;
            cargo.gridCoord = drop;
            cargo.hasActed = true;
            dest.unitIndex = cargoIdx;

            trans.hasActed = true;
            _state.units[cargoIdx] = cargo;
            _state.units[transIdx] = trans;
        }

        private void ExecuteSupply(AIAction a)
        {
            if (a.unitIndex < 0 || a.unitIndex >= _state.units.Count)
                return;

            var u = _state.units[a.unitIndex];
            if (!u.alive || !u.IsOnMap)
                return;

            for (int d = 0; d < 4; d++)
            {
                Vector2Int n;
                switch (d)
                {
                    case 0: n = new Vector2Int(u.gridCoord.x + 1, u.gridCoord.y); break;
                    case 1: n = new Vector2Int(u.gridCoord.x - 1, u.gridCoord.y); break;
                    case 2: n = new Vector2Int(u.gridCoord.x, u.gridCoord.y + 1); break;
                    default: n = new Vector2Int(u.gridCoord.x, u.gridCoord.y - 1); break;
                }
                if (!_state.IsInBounds(n))
                    continue;

                ref var cell = ref _state.grid[n.x, n.y];
                if (cell.unitIndex < 0 || cell.unitIndex >= _state.units.Count)
                    continue;

                var targetUnit = _state.units[cell.unitIndex];
                int distance = _state.ManhattanDistance(u.gridCoord, targetUnit.gridCoord);
                if (!SupplyRulesShared.CanSnapshotSupply(u, targetUnit, distance))
                    continue;

                SupplyRulesShared.ApplySnapshotSupply(ref u, ref targetUnit);
                _state.units[cell.unitIndex] = targetUnit;
            }

            u.hasActed = true;
            _state.units[a.unitIndex] = u;
        }
    }
}
