using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 运输单位能力接口。可装载（Load）和卸载（Drop）其他单位。
/// </summary>
public interface ITransporter
{
    /// <summary>
    /// 尝试装载目标单位。返回是否成功。
    /// </summary>
    bool Load(UnitController unit);

    /// <summary>
    /// 在指定格子卸载单位。返回是否成功。
    /// </summary>
    bool Drop(UnitController unit, Vector2Int targetCoord);

    /// <summary>
    /// 当前装载的单位数量。
    /// </summary>
    int LoadedCount { get; }

    /// <summary>
    /// 当前搭载单位列表（只读）。
    /// </summary>
    IReadOnlyList<UnitController> LoadedUnits { get; }

    /// <summary>
    /// 最大可装载数量。
    /// </summary>
    int Capacity { get; }
}
