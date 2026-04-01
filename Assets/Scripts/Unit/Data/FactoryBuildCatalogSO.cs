using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = OperationMarigoldPaths.SoFactoryBuildCatalog, fileName = "FactoryBuildCatalog")]
public class FactoryBuildCatalogSO : ScriptableObject
{
    [Header("通用（所有阵营共享）")]
    [Tooltip("无论哪个阵营打开工厂菜单都会包含的单位。")]
    [SerializeField] private List<UnitData> _commonUnits = new List<UnitData>();

    [Header("按阵营")]
    [SerializeField] private List<UnitData> _marigoldUnits = new List<UnitData>();
    [SerializeField] private List<UnitData> _lancelUnits = new List<UnitData>();

    /// <summary>
    /// 获取指定阵营的可制造单位列表（会自动去重，忽略空/无 id 项）。
    /// </summary>
    public List<UnitData> GetBuildableUnits(UnitFaction faction)
    {
        var result = new List<UnitData>();
        var added = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        AddRange(_commonUnits, result, added);
        AddRange(GetFactionList(faction), result, added);
        return result;
    }

    private List<UnitData> GetFactionList(UnitFaction faction)
    {
        switch (faction)
        {
            case UnitFaction.Marigold:
                return _marigoldUnits;
            case UnitFaction.Lancel:
                return _lancelUnits;
            default:
                return null;
        }
    }

    private static void AddRange(List<UnitData> source, List<UnitData> dest, HashSet<string> added)
    {
        if (source == null || dest == null || added == null)
            return;

        for (var i = 0; i < source.Count; i++)
        {
            var data = source[i];
            if (data == null)
                continue;
            if (string.IsNullOrWhiteSpace(data.id))
                continue;
            if (!added.Add(data.id))
                continue;

            dest.Add(data);
        }
    }
}

