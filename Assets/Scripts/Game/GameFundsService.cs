/// <summary>
/// 资金访问用例服务：封装对账本的读写。
/// </summary>
public sealed class GameFundsService
{
    private static readonly GameFundsService _instance = new GameFundsService();

    public static GameFundsService Instance => _instance;

    private GameFundsService() { }

    public int GetFunds(UnitFaction ownerFaction)
    {
        return FactionFundsLedger.Instance.GetFunds(ownerFaction);
    }

    public void SetFunds(UnitFaction ownerFaction, int amount)
    {
        FactionFundsLedger.Instance.SetFunds(ownerFaction, amount);
    }

    public void AddFunds(UnitFaction ownerFaction, int amount)
    {
        FactionFundsLedger.Instance.AddFunds(ownerFaction, amount);
    }

    public bool TrySpendFunds(UnitFaction ownerFaction, int amount)
    {
        return FactionFundsLedger.Instance.TrySpendFunds(ownerFaction, amount);
    }
}
