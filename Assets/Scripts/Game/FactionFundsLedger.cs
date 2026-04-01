using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阵营资金账本。仅管理资金读写，不处理建筑或回合业务。
/// </summary>
public sealed class FactionFundsLedger
{
    private static readonly FactionFundsLedger _instance = new FactionFundsLedger();
    private readonly List<int> _fundsPerOwner = new List<int> { 0, 0 };

    public static FactionFundsLedger Instance => _instance;

    /// <summary>
    /// 资金发生变化时触发。（阵营, 旧值, 新值, 变化量）
    /// 这是一个“事实事件出口”，不应承载 UI/日志等表现层逻辑。
    /// </summary>
    public static event System.Action<UnitFaction, int, int, int> OnFundsChanged;

    private FactionFundsLedger() { }

    public int GetFunds(UnitFaction ownerFaction)
    {
        var ownerIndex = (int)ownerFaction;
        if (ownerIndex < 0 || ownerIndex >= _fundsPerOwner.Count)
            return 0;
        return _fundsPerOwner[ownerIndex];
    }

    public void SetFunds(UnitFaction ownerFaction, int amount)
    {
        var ownerIndex = (int)ownerFaction;
        if (ownerIndex < 0)
            return;

        while (_fundsPerOwner.Count <= ownerIndex)
            _fundsPerOwner.Add(0);

        var oldValue = _fundsPerOwner[ownerIndex];
        var newValue = Mathf.Max(0, amount);
        if (oldValue == newValue)
            return;

        _fundsPerOwner[ownerIndex] = newValue;
        OnFundsChanged?.Invoke(ownerFaction, oldValue, newValue, newValue - oldValue);
    }

    public void AddFunds(UnitFaction ownerFaction, int amount)
    {
        if (ownerFaction == UnitFaction.None)
            return;

        SetFunds(ownerFaction, GetFunds(ownerFaction) + amount);
    }

    public bool TrySpendFunds(UnitFaction ownerFaction, int amount)
    {
        if (ownerFaction == UnitFaction.None)
            return false;

        var spend = Mathf.Max(0, amount);
        var currentFunds = GetFunds(ownerFaction);
        if (currentFunds < spend)
            return false;

        SetFunds(ownerFaction, currentFunds - spend);
        return true;
    }
}
