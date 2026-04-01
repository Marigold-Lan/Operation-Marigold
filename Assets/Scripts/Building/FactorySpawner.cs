using UnityEngine;

/// <summary>
/// 工厂造兵组件。实现 IUnitSpawner，与 BuildingController 配合使用。
/// 挂载在工厂 placeable prefab 上。
/// </summary>
public class FactorySpawner : MonoBehaviour, IUnitSpawner
{
    private BuildingController _building;
    private MapRoot _mapRoot;

    /// <summary>此工厂关联的 BuildingController（Awake 时缓存）。</summary>
    public BuildingController Building => _building;

    /// <summary>呼出造兵菜单时触发，UI 层订阅后显示菜单。</summary>
    public event System.Action<FactorySpawner> OnShowSpawnMenuRequested;

    /// <summary>单位生成成功时触发。</summary>
    public event System.Action<UnitController> OnUnitSpawned;

    private void Awake()
    {
        _building = GetComponent<BuildingController>();
        _mapRoot = GetComponentInParent<MapRoot>() ?? MapRoot.Instance;
    }

    public bool CanSpawn(UnitFaction ownerFaction)
    {
        if (ownerFaction == UnitFaction.None) return false;
        if (_building == null) return false;
        if (_building.OwnerFaction == UnitFaction.None) return false;
        if (_building.OwnerFaction != ownerFaction) return false;
        if (_building.State != null && _building.State.HasSpawnedThisTurn) return false;
        return true;
    }

    public void ShowSpawnMenu()
    {
        OnShowSpawnMenuRequested?.Invoke(this);
    }

    public bool TrySpawn(UnitData unitData, Cell spawnCell)
    {
        if (unitData == null || spawnCell == null || _building == null)
            return false;
        if (_building.OwnerFaction == UnitFaction.None)
            return false;
        if (_building.State != null && _building.State.HasSpawnedThisTurn) return false;
        if (spawnCell.HasUnit) return false;

        if (FactionFundsLedger.Instance.GetFunds(_building.OwnerFaction) < unitData.cost)
            return false;

        if (unitData.prefab == null)
            return false;

        if (_mapRoot == null)
            _mapRoot = MapRoot.Instance;
        if (_mapRoot == null) return false;

        FactionFundsLedger.Instance.AddFunds(_building.OwnerFaction, -unitData.cost);

        if (_building.State != null)
            _building.State.HasSpawnedThisTurn = true;

        var unitParent = ResolveUnitContainer(_building.OwnerFaction);
        var unitGo = Object.Instantiate(unitData.prefab, unitParent);
        unitGo.transform.position = _mapRoot.GridToWorld(spawnCell.gridCoord);

        var controller = unitGo.GetComponent<UnitController>();
        if (controller != null)
        {
            controller.Initialize(unitData, _mapRoot, spawnCell, _building.OwnerFaction);
            // 新造出来的单位本回合不可行动。
            controller.HasActed = true;
        }

        spawnCell.SetUnit(unitGo);
        OnUnitSpawned?.Invoke(controller);

        return true;
    }

    private Transform ResolveUnitContainer(UnitFaction faction)
    {
        if (_mapRoot == null)
            return transform;

        var configuredContainer = faction == UnitFaction.Lancel
            ? _mapRoot.LancelUnitContainer
            : _mapRoot.MarigoldUnitContainer;
        if (configuredContainer != null)
            return configuredContainer;

        return _mapRoot.transform;
    }
}
