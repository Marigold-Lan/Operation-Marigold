using NUnit.Framework;
using UnityEngine;
using OperationMarigold.AI.Simulation;

public class SharedRulesConsistencyTests
{
    [Test]
    public void ApplySnapshotEndTurn_UsesSharedEconomyAndResupplyRules()
    {
        var board = new AIBoardState
        {
            width = 1,
            height = 1,
            grid = new AICellSnapshot[1, 1],
            units = new System.Collections.Generic.List<AIUnitSnapshot>(),
            buildings = new System.Collections.Generic.List<AIBuildingSnapshot>(),
            funds = new[] { 0, 500 },
            currentPlayerId = 0,
            damageMatrix = new DamageMatrixLookup()
        };

        board.units.Add(new AIUnitSnapshot
        {
            alive = true,
            faction = UnitFaction.Lancel,
            gridCoord = Vector2Int.zero,
            hp = 5,
            maxHp = 10,
            fuel = 1,
            maxFuel = 9,
            ammo = 0,
            maxAmmo = 5,
            hasActed = true,
            hasMovedThisTurn = true,
            cost = 1000
        });

        board.buildings.Add(new AIBuildingSnapshot
        {
            gridCoord = Vector2Int.zero,
            ownerFaction = UnitFaction.Lancel,
            incomePerTurn = 1000,
            maxCaptureHp = 20,
            captureHp = 20,
            captureDamagePerStep = 1,
            captureActorUnitIndex = -1
        });

        board.grid[0, 0] = new AICellSnapshot
        {
            gridCoord = Vector2Int.zero,
            terrainKind = (int)AITerrainKind.Plains,
            terrainStars = 0,
            movementCost = 1,
            unitIndex = 0,
            buildingIndex = 0
        };

        int next = TurnEconomyRulesShared.ApplySnapshotEndTurn(board, 0);

        Assert.AreEqual(1, next);
        Assert.AreEqual(1, board.currentPlayerId);
        Assert.AreEqual(1300, board.funds[1]);
        Assert.AreEqual(7, board.units[0].hp);
        Assert.AreEqual(9, board.units[0].fuel);
        Assert.AreEqual(5, board.units[0].ammo);
        Assert.IsFalse(board.units[0].hasActed);
        Assert.IsFalse(board.units[0].hasMovedThisTurn);
    }

    [Test]
    public void CaptureRules_ResetProgressWhenCapturerChanges()
    {
        var building = new AIBuildingSnapshot
        {
            ownerFaction = UnitFaction.None,
            maxCaptureHp = 20,
            captureHp = 10,
            captureDamagePerStep = 1,
            captureActorUnitIndex = 2
        };

        var unit = new AIUnitSnapshot
        {
            alive = true,
            faction = UnitFaction.Marigold,
            hp = 5,
            maxHp = 10,
            movementType = MovementType.Foot,
            category = UnitCategory.Soldier
        };

        bool applied = CaptureRulesShared.ApplySnapshotCapture(ref building, ref unit, 1);

        Assert.IsTrue(applied);
        Assert.AreEqual(15, building.captureHp);
        Assert.AreEqual(1, building.captureActorUnitIndex);
        Assert.IsTrue(unit.hasActed);
    }

    [Test]
    public void TransportRules_ValidateLoadAndDropOnSnapshot()
    {
        var board = new AIBoardState
        {
            width = 2,
            height = 2,
            grid = new AICellSnapshot[2, 2],
            units = new System.Collections.Generic.List<AIUnitSnapshot>(),
            buildings = new System.Collections.Generic.List<AIBuildingSnapshot>(),
            funds = new[] { 0, 0 },
            currentPlayerId = 0,
            damageMatrix = new DamageMatrixLookup()
        };

        board.units.Add(new AIUnitSnapshot
        {
            alive = true,
            faction = UnitFaction.Marigold,
            gridCoord = new Vector2Int(0, 0),
            movementType = MovementType.Foot,
            category = UnitCategory.Soldier
        });
        board.units.Add(new AIUnitSnapshot
        {
            alive = true,
            faction = UnitFaction.Marigold,
            gridCoord = new Vector2Int(1, 0),
            transportCapacity = 1
        });

        board.grid[0, 0] = new AICellSnapshot { gridCoord = new Vector2Int(0, 0), terrainKind = (int)AITerrainKind.Plains, unitIndex = 0, buildingIndex = -1 };
        board.grid[1, 0] = new AICellSnapshot { gridCoord = new Vector2Int(1, 0), terrainKind = (int)AITerrainKind.Plains, unitIndex = 1, buildingIndex = -1 };
        board.grid[0, 1] = new AICellSnapshot { gridCoord = new Vector2Int(0, 1), terrainKind = (int)AITerrainKind.Plains, unitIndex = -1, buildingIndex = -1 };

        Assert.IsTrue(TransportRulesShared.CanSnapshotLoad(board, 0, 1));

        var cargo = board.units[0];
        cargo.embarkedOnUnitIndex = 1;
        cargo.gridCoord = new Vector2Int(1, 0);
        board.units[0] = cargo;
        board.grid[0, 0].unitIndex = -1;

        Assert.IsTrue(TransportRulesShared.CanSnapshotDrop(board, 1, 0, new Vector2Int(0, 0)));
        Assert.IsFalse(TransportRulesShared.CanSnapshotDrop(board, 1, 0, new Vector2Int(1, 0)));
    }

    [Test]
    public void SupplyRules_ApplySupplyOnSnapshot()
    {
        var supplier = new AIUnitSnapshot
        {
            alive = true,
            canSupply = true,
            faction = UnitFaction.Marigold,
            hasActed = false,
            gridCoord = new Vector2Int(0, 0)
        };
        var target = new AIUnitSnapshot
        {
            alive = true,
            faction = UnitFaction.Marigold,
            fuel = 1,
            maxFuel = 9,
            ammo = 0,
            maxAmmo = 6,
            hasPrimaryWeapon = true,
            embarkedOnUnitIndex = -1,
            gridCoord = new Vector2Int(1, 0)
        };

        Assert.IsTrue(SupplyRulesShared.CanSnapshotSupply(supplier, target, 1));
        SupplyRulesShared.ApplySnapshotSupply(ref supplier, ref target);
        Assert.AreEqual(9, target.fuel);
        Assert.AreEqual(6, target.ammo);
        Assert.IsTrue(supplier.hasActed);
    }
}
