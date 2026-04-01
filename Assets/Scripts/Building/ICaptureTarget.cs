/// <summary>
/// 可被占领的建筑接口。所有建筑（City、Factory、HQ）都应实现。
/// </summary>
public interface ICaptureTarget
{
    /// <summary>当前所属阵营。</summary>
    UnitFaction OwnerFaction { get; }

    /// <summary>当前占领耐久度。</summary>
    int CurrentCaptureHp { get; }

    /// <summary>占领耐久度上限。</summary>
    int MaxCaptureHp { get; }

    /// <summary>
    /// 施加占领伤害。返回本次是否完成占领（耐久归零）。
    /// </summary>
    /// <param name="power">占领力度（每次减少的耐久）</param>
    /// <param name="attackerFaction">占领方阵营</param>
    /// <param name="capturer">执行占领动作的单位</param>
    bool ApplyCapture(int power, UnitFaction attackerFaction, UnitController capturer);
}
