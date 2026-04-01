using UnityEngine;

/// <summary>
/// 回合收入结算服务。输入阵营，结算建筑收入并返回本次总收入。
/// </summary>
public sealed class TurnIncomeService
{
    private static readonly TurnIncomeService _instance = new TurnIncomeService();

    public static TurnIncomeService Instance => _instance;

    public static event System.Action<UnitFaction> OnTurnIncomeStarted;
    public static event System.Action<UnitFaction, BuildingController, int> OnIncomeFromBuilding;
    public static event System.Action<UnitFaction, int, int> OnTurnIncomeCompleted;

    private TurnIncomeService() { }

    public int ApplyTurnIncome(UnitFaction faction, MapRoot preferredRoot = null)
    {
        if (faction == UnitFaction.None)
            return 0;

        OnTurnIncomeStarted?.Invoke(faction);
        var totalIncome = 0;
        var contributingBuildings = 0;
        var buildings = BuildingQueryService.Instance.FindAllBuildings(preferredRoot);

        for (var i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (building is IIncomeProvider provider && building.OwnerFaction == faction)
            {
                var income = provider.GetIncome();
                if (income <= 0)
                    continue;

                FactionFundsLedger.Instance.AddFunds(faction, income);
                totalIncome += income;
                contributingBuildings++;
                OnIncomeFromBuilding?.Invoke(faction, building, income);
            }
        }

        OnTurnIncomeCompleted?.Invoke(faction, totalIncome, contributingBuildings);
        return totalIncome;
    }
}
