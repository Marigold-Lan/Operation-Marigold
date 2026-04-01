/// <summary>
/// 造兵接口。仅工厂实现。包含弹出建造菜单、生成单位的逻辑。
/// </summary>
public interface IUnitSpawner
{
    /// <summary>
    /// 该工厂是否可为本阵营造兵（己方、本回合未造过、资金足够等由实现判断）。
    /// </summary>
    bool CanSpawn(UnitFaction ownerFaction);

    /// <summary>
    /// 呼出造兵菜单。UI 层调用。
    /// </summary>
    void ShowSpawnMenu();

    /// <summary>
    /// 尝试在指定格子生成单位。返回是否成功。
    /// </summary>
    bool TrySpawn(UnitData unitData, Cell spawnCell);
}
