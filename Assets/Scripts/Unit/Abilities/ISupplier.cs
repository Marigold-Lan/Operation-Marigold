/// <summary>
/// 补给能力接口。可为周围单位补充燃料和弹药。
/// </summary>
public interface ISupplier
{
    /// <summary>
    /// 对指定单位执行补给。返回是否成功。
    /// </summary>
    bool Supply(UnitController target);

    /// <summary>
    /// 补给范围（格子数）。
    /// </summary>
    int SupplyRange { get; }
}
