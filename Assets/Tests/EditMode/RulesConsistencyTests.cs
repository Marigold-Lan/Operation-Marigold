using NUnit.Framework;
using OperationMarigold.AI.Simulation;

public class RulesConsistencyTests
{
    [TestCase(100, 10, 10, 100)]
    [TestCase(100, 5, 10, 50)]
    [TestCase(100, 1, 10, 10)]
    [TestCase(1, 1, 10, 1)]
    [TestCase(0, 10, 10, 0)]
    public void ApplyHpScale_MatchesExpectedRuntimeBehavior(int rawDamage, int hp, int maxHp, int expected)
    {
        var actual = DamageResolver.ApplyHpScale(rawDamage, hp, maxHp);
        Assert.AreEqual(expected, actual);
    }

    [Test]
    public void AiAndRuntimeMovementCostTables_AreEquivalent()
    {
        var terrainKinds = new[]
        {
            AITerrainKind.Plains,
            AITerrainKind.Woods,
            AITerrainKind.Mountain,
            AITerrainKind.River,
            AITerrainKind.Sea,
            AITerrainKind.Road,
            AITerrainKind.Bridge,
            AITerrainKind.Building
        };

        var movementTypes = new[]
        {
            MovementType.Foot,
            MovementType.Mech,
            MovementType.Treads,
            MovementType.Wheeled
        };

        foreach (var terrain in terrainKinds)
        {
            foreach (var movementType in movementTypes)
            {
                var ai = AIMovementCostProvider.GetCost(terrain, movementType);
                var runtime = MovementCostProvider.GetCostForTerrainKind((MovementCostProvider.MovementTerrainKind)terrain, movementType);
                Assert.AreEqual(runtime, ai, $"terrain={terrain}, movement={movementType}");
            }
        }
    }
}
