/// <summary>
/// 回合生命周期用例服务：封装回合开始时的经济/补给流程。
/// </summary>
public sealed class TurnLifecycleService
{
    private static readonly TurnLifecycleService _instance = new TurnLifecycleService();

    public static TurnLifecycleService Instance => _instance;

    private TurnLifecycleService() { }

    public void HandleTurnStart(UnitFaction faction, MapRoot preferredRoot = null)
    {
        TurnEconomyRulesShared.ApplyRuntimeTurnStart(faction, preferredRoot);
    }
}
