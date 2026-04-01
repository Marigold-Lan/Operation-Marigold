using System.Collections.Generic;
using UnityEngine;

namespace OperationMarigold.AI.Simulation
{
    /// <summary>
    /// 从真实游戏对象拍摄纯数据快照，生成 AIBoardState。
    /// </summary>
    public static class BoardSnapshotFactory
    {
        private static readonly DamageMatrixLookup CachedDamageMatrix = new DamageMatrixLookup();
        private static readonly HashSet<string> RegisteredUnitIds = new HashSet<string>();

        public sealed class CaptureBuilder
        {
            private readonly MapRoot _mapRoot;
            private readonly int _width;
            private readonly int _height;
            private int _cursor;

            public AIBoardState State { get; }
            public int ProcessedCells => _cursor;
            public int TotalCells => _width * _height;

            public CaptureBuilder(MapRoot mapRoot, FactionFundsLedger ledger, int currentPlayerId)
            {
                _mapRoot = mapRoot;
                _width = mapRoot.gridWidth;
                _height = mapRoot.gridHeight;

                State = new AIBoardState
                {
                    width = _width,
                    height = _height,
                    currentPlayerId = currentPlayerId,
                    grid = new AICellSnapshot[_width, _height],
                    units = new List<AIUnitSnapshot>(32),
                    buildings = new List<AIBuildingSnapshot>(16),
                    funds = new int[]
                    {
                        ledger.GetFunds(UnitFaction.Marigold),
                        ledger.GetFunds(UnitFaction.Lancel)
                    },
                    damageMatrix = CachedDamageMatrix
                };
            }

            public bool ProcessNext(int maxCellsPerStep)
            {
                int budget = Mathf.Max(1, maxCellsPerStep);
                int total = TotalCells;
                for (int n = 0; n < budget && _cursor < total; n++, _cursor++)
                {
                    int x = _cursor % _width;
                    int y = _cursor / _width;
                    var coord = new Vector2Int(x, y);
                    var cell = _mapRoot.GetCellAt(coord);

                    if (cell == null)
                    {
                        State.grid[x, y] = new AICellSnapshot
                        {
                            gridCoord = coord,
                            terrainKind = (int)AITerrainKind.Unknown,
                            terrainStars = 0,
                            movementCost = 999,
                            unitIndex = -1,
                            buildingIndex = -1
                        };
                        continue;
                    }

                    int uIdx = -1;
                    var uc = cell.UnitController;
                    if (uc != null && uc.Data != null && (uc.Health == null || !uc.Health.IsDead))
                    {
                        uIdx = State.units.Count;
                        State.units.Add(CaptureUnit(uc));
                        RegisterDamageMatrixCached(uc.Data);
                        AppendEmbarkedCargoUnits(State, uc, uIdx);
                    }

                    int bIdx = -1;
                    var bc = cell.Building;
                    if (bc != null && bc.Data != null)
                    {
                        bIdx = State.buildings.Count;
                        State.buildings.Add(CaptureBuilding(bc));
                    }

                    State.grid[x, y] = new AICellSnapshot
                    {
                        gridCoord = coord,
                        terrainKind = (int)CaptureTerrainKind(cell),
                        terrainStars = cell.GetTerrainStars(),
                        movementCost = cell.MovementCost,
                        unitIndex = uIdx,
                        buildingIndex = bIdx
                    };
                }

                return _cursor >= total;
            }
        }

        public static CaptureBuilder CreateBuilder(MapRoot mapRoot, FactionFundsLedger ledger, int currentPlayerId)
        {
            return new CaptureBuilder(mapRoot, ledger, currentPlayerId);
        }

        public static AIBoardState Capture(MapRoot mapRoot, FactionFundsLedger ledger, int currentPlayerId)
        {
            var builder = CreateBuilder(mapRoot, ledger, currentPlayerId);
            while (!builder.ProcessNext(int.MaxValue)) { }
            return builder.State;
        }

        private static void AppendEmbarkedCargoUnits(AIBoardState state, UnitController transporterUc, int transporterIndex)
        {
            var trans = transporterUc != null ? transporterUc.GetComponent<ITransporter>() : null;
            if (trans == null || trans.LoadedUnits == null || trans.LoadedCount <= 0)
                return;

            var anchor = transporterUc.GridCoord;
            for (int i = 0; i < trans.LoadedUnits.Count; i++)
            {
                var cargo = trans.LoadedUnits[i];
                if (cargo == null || cargo.Data == null || (cargo.Health != null && cargo.Health.IsDead))
                    continue;

                var cs = CaptureUnit(cargo);
                cs.embarkedOnUnitIndex = transporterIndex;
                cs.gridCoord = anchor;
                state.units.Add(cs);
                RegisterDamageMatrixCached(cargo.Data);
            }
        }

        private static AIUnitSnapshot CaptureUnit(UnitController uc)
        {
            var d = uc.Data;
            var trans = uc.GetComponent<ITransporter>();
            int cap = trans != null ? trans.Capacity : 0;
            var snap = new AIUnitSnapshot
            {
                unitId = d.id,
                gridCoord = uc.GridCoord,
                hp = uc.Health != null ? uc.Health.CurrentHp : d.maxHp,
                maxHp = d.maxHp,
                fuel = uc.CurrentFuel,
                maxFuel = d.maxFuel,
                ammo = uc.CurrentAmmo,
                maxAmmo = d.MaxPrimaryAmmo,
                faction = uc.OwnerFaction,
                hasActed = uc.HasActed,
                hasMovedThisTurn = uc.HasMovedThisTurn,
                movementRange = d.movementRange,
                movementType = d.movementType,
                category = d.category,
                cost = d.cost,
                alive = true,
                embarkedOnUnitIndex = -1,
                transportCapacity = cap,
                canSupply = uc.GetComponent<ISupplier>() != null,

                hasPrimaryWeapon = d.HasPrimaryWeapon,
                hasSecondaryWeapon = d.HasSecondaryWeapon
            };

            if (d.HasPrimaryWeapon)
            {
                snap.primaryBaseDamage = d.primaryWeapon.baseDamage;
                snap.primaryRangeMin = d.primaryWeapon.attackRangeMin;
                snap.primaryRangeMax = d.primaryWeapon.attackRangeMax;
                snap.primaryCanAttackVehicle = d.primaryWeapon.canAttackVehicle;
                snap.primaryCanAttackSoldier = d.primaryWeapon.canAttackSoldier;
                snap.primaryRequiresStationary = d.primaryWeapon.requiresStationaryToAttack;
            }

            if (d.HasSecondaryWeapon)
            {
                snap.secondaryBaseDamage = d.secondaryWeapon.baseDamage;
                snap.secondaryRangeMin = d.secondaryWeapon.attackRangeMin;
                snap.secondaryRangeMax = d.secondaryWeapon.attackRangeMax;
                snap.secondaryCanAttackVehicle = d.secondaryWeapon.canAttackVehicle;
                snap.secondaryCanAttackSoldier = d.secondaryWeapon.canAttackSoldier;
                snap.secondaryRequiresStationary = d.secondaryWeapon.requiresStationaryToAttack;
            }

            return snap;
        }

        private static AIBuildingSnapshot CaptureBuilding(BuildingController bc)
        {
            return new AIBuildingSnapshot
            {
                gridCoord = bc.Cell != null ? bc.Cell.gridCoord : new Vector2Int(bc.State.GridX, bc.State.GridZ),
                ownerFaction = bc.OwnerFaction,
                isHq = bc.Data.isHq,
                incomePerTurn = bc.Data.incomePerTurn,
                isFactory = bc.Data.factoryBuildCatalog != null,
                hasSpawnedThisTurn = bc.State != null && bc.State.HasSpawnedThisTurn,
                captureHp = bc.CurrentCaptureHp,
                maxCaptureHp = bc.Data.maxCaptureHp,
                captureDamagePerStep = bc.Data.captureDamagePerStep,
                captureActorUnitIndex = -1
            };
        }

        private static void RegisterDamageMatrixCached(UnitData data)
        {
            if (RegisteredUnitIds.Contains(data.id)) return;
            RegisteredUnitIds.Add(data.id);

            if (data.HasPrimaryWeapon && data.primaryWeapon.damageMatrix != null)
            {
                foreach (var entry in data.primaryWeapon.damageMatrix)
                {
                    if (entry == null) continue;
                    int secPercent = 0;
                    if (data.HasSecondaryWeapon && data.secondaryWeapon.damageMatrix != null)
                        secPercent = data.secondaryWeapon.GetDamagePercent(entry.targetUnitId);
                    CachedDamageMatrix.Set(data.id, entry.targetUnitId, entry.damagePercent, secPercent);
                }
            }

            if (data.HasSecondaryWeapon && data.secondaryWeapon.damageMatrix != null)
            {
                foreach (var entry in data.secondaryWeapon.damageMatrix)
                {
                    if (entry == null) continue;
                    int priPercent = 0;
                    if (data.HasPrimaryWeapon && data.primaryWeapon.damageMatrix != null)
                        priPercent = data.primaryWeapon.GetDamagePercent(entry.targetUnitId);
                    CachedDamageMatrix.Set(data.id, entry.targetUnitId, priPercent, entry.damagePercent);
                }
            }
        }

        // 将真实游戏格子的地形/放置物映射到与 MovementCostProvider 相同的“等效地形”。
        private static AITerrainKind CaptureTerrainKind(Cell cell)
        {
            if (cell == null)
                return AITerrainKind.Unknown;

            var bas = cell.BaseType;
            var placeable = cell.PlaceableType;

            if (placeable != null)
            {
                var id = placeable.id;
                if (!string.IsNullOrEmpty(id) && id.StartsWith("Road"))
                    return AITerrainKind.Road;

                if (id == "City" || id == "HQ" || id == "Factory" || id == "Airport" || id == "Lab" ||
                    id == "CommTower" || id == "Silo")
                    return AITerrainKind.Building;

                if (id == "Forest" || id == "Woods")
                    return AITerrainKind.Woods;

                if (id == "Mountain" || id == "Mountains")
                    return AITerrainKind.Mountain;

                if (id == "RiverBridge" || id == "Bridge")
                    return AITerrainKind.Bridge;

                if (id == "Sea" || id == "DeepSea")
                    return AITerrainKind.Sea;

                if (id == "River" || id == "Rivers")
                {
                    if (bas != null && (bas.id == "RiverBridge" || bas.id == "Bridge"))
                        return AITerrainKind.Bridge;
                    return AITerrainKind.River;
                }
            }

            if (bas != null)
            {
                var id = bas.id;
                if (id == "RiverBridge" || id == "Bridge")
                    return AITerrainKind.Bridge;
                if (id == "Sea" || id == "DeepSea")
                    return AITerrainKind.Sea;
                if (id == "Plain" || id == "Plains")
                    return AITerrainKind.Plains;
                if (id == "Forest" || id == "Woods")
                    return AITerrainKind.Woods;
                if (id == "Mountain" || id == "Mountains")
                    return AITerrainKind.Mountain;
                if (id == "River" || id == "RiverContainer" || id == "Rivers")
                    return AITerrainKind.River;
            }

            return AITerrainKind.Plains;
        }
    }
}
