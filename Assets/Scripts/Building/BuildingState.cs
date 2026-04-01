using System;

/// <summary>
/// 建筑运行时状态。纯 C# 类，不含 UnityEngine 依赖，便于 AI 深度搜索时拷贝快照。
/// </summary>
[Serializable]
public class BuildingState
{
    /// <summary>当前所属阵营。</summary>
    public UnitFaction OwnerFaction;

    /// <summary>当前占领耐久度，被步兵踩会下降，归零则被占领。</summary>
    public int CurrentCaptureHp;

    /// <summary>所在格子 X 坐标。</summary>
    public int GridX;

    /// <summary>所在格子 Z 坐标。</summary>
    public int GridZ;

    /// <summary>仅工厂有效：本回合是否已造过兵。</summary>
    public bool HasSpawnedThisTurn;

    /// <summary>
    /// 创建可拷贝的快照，供 AI 模拟使用。
    /// </summary>
    public BuildingState Copy()
    {
        return new BuildingState
        {
            OwnerFaction = OwnerFaction,
            CurrentCaptureHp = CurrentCaptureHp,
            GridX = GridX,
            GridZ = GridZ,
            HasSpawnedThisTurn = HasSpawnedThisTurn
        };
    }
}
