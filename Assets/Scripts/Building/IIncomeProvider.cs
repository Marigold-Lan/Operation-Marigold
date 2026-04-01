/// <summary>
/// 回合开始时提供资金的建筑接口。城市、工厂、总部都实现。
/// </summary>
public interface IIncomeProvider
{
    /// <summary>
    /// 获取本建筑本回合提供的资金。仅对己方建筑生效（由调用方判断 OwnerFaction）。
    /// </summary>
    int GetIncome();
}
