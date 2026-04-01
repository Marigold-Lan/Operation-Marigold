using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 建筑查询服务。封装场景中的建筑检索逻辑，供收入与胜负模块复用。
/// </summary>
public sealed class BuildingQueryService
{
    private static readonly BuildingQueryService _instance = new BuildingQueryService();
    private MapRoot _mapRoot;

    public static BuildingQueryService Instance => _instance;

    private BuildingQueryService() { }

    public List<BuildingController> FindAllBuildings(MapRoot preferredRoot = null)
    {
        var list = new List<BuildingController>();

        if (preferredRoot != null)
            _mapRoot = preferredRoot;
        if (_mapRoot == null)
            _mapRoot = MapRoot.Instance;

        if (_mapRoot != null)
        {
            var cells = _mapRoot.GetComponentsInChildren<Cell>(true);
            for (var i = 0; i < cells.Length; i++)
            {
                var building = cells[i] != null ? cells[i].Building : null;
                if (building != null)
                    list.Add(building);
            }
        }

        if (list.Count == 0)
        {
#if UNITY_2023_1_OR_NEWER
            var allBuildings = Object.FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var allBuildings = Object.FindObjectsOfType<BuildingController>(true);
#endif
            for (var i = 0; i < allBuildings.Length; i++)
            {
                if (allBuildings[i] != null)
                    list.Add(allBuildings[i]);
            }
        }

        return list;
    }
}
