public class ProduceCommand : ICommand
{
    public CommandType Type => CommandType.Produce;

    public bool CanExecute(CommandContext context)
    {
        if (context?.FactorySpawner == null || context.ProduceUnitData == null)
            return false;

        var building = context.FactorySpawner.Building;
        if (building == null || building.OwnerFaction == UnitFaction.None)
            return false;

        if (!context.FactorySpawner.CanSpawn(building.OwnerFaction))
            return false;

        var spawnCell = context.SpawnCell != null ? context.SpawnCell : building.Cell;
        if (spawnCell == null || spawnCell.HasUnit)
            return false;

        if (context.ProduceUnitData.prefab == null)
            return false;

        return FactionFundsLedger.Instance.GetFunds(building.OwnerFaction) >= context.ProduceUnitData.cost;
    }

    public void Execute(CommandContext context)
    {
        if (context?.FactorySpawner == null || context.ProduceUnitData == null)
            return;

        var building = context.FactorySpawner.Building;
        var spawnCell = context.SpawnCell != null ? context.SpawnCell : (building != null ? building.Cell : null);
        if (spawnCell == null)
            return;

        context.FactorySpawner.TrySpawn(context.ProduceUnitData, spawnCell);
    }
}
