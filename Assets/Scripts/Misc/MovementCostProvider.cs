using UnityEngine;

/// <summary>
/// 全局服务站：根据 MovementType + 地形计算移动消耗。
/// 返回 -1 表示不可通行，正数表示消耗。
/// Demo 地形：平原、森林、山地、河流、道路、桥梁、建筑物。
/// </summary>
public static class MovementCostProvider
{
    private const int Impassable = -1;

    public enum MovementTerrainKind
    {
        Unknown = -1,
        Plains = 0,
        Woods = 1,
        Mountain = 2,
        River = 3,
        Sea = 4,
        Road = 5,
        Bridge = 6,
        Building = 7
    }

    /// <summary>
    /// 获取指定格子对某移动类型的有效消耗。
    /// </summary>
    /// <param name="cell">目标格子</param>
    /// <param name="movementType">单位移动类型</param>
    /// <returns>消耗值，-1 表示不可通行</returns>
    public static int GetCost(Cell cell, MovementType movementType)
    {
        if (cell == null) return 99;

        var terrain = GetEffectiveTerrain(cell);
        return GetCostForTerrain(terrain, movementType);
    }

    public static int GetCostForTerrainKind(MovementTerrainKind terrainKind, MovementType movementType)
    {
        return GetCostForTerrain(terrainKind, movementType);
    }

    /// <summary>
    /// 从 Cell 解析有效地形类型。优先看 Placeable（道路、建筑等），否则看 Base。
    /// </summary>
    private static MovementTerrainKind GetEffectiveTerrain(Cell cell)
    {
        var bas = cell.BaseType;
        var placeable = cell.PlaceableType;

        if (placeable != null)
        {
            var id = placeable.id;
            if (id != null && id.StartsWith("Road")) return MovementTerrainKind.Road;
            if (id == "City" || id == "HQ" || id == "Factory" || id == "Airport" || id == "Lab" || id == "CommTower" || id == "Silo")
                return MovementTerrainKind.Building;
            if (id == "Forest" || id == "Woods") return MovementTerrainKind.Woods;
            if (id == "Mountain" || id == "Mountains") return MovementTerrainKind.Mountain;
            if (id == "RiverBridge" || id == "Bridge") return MovementTerrainKind.Bridge;
            if (id == "Sea" || id == "DeepSea") return MovementTerrainKind.Sea;
            if (id == "River" || id == "Rivers")
            {
                if (bas != null && (bas.id == "RiverBridge" || bas.id == "Bridge"))
                    return MovementTerrainKind.Bridge;
                return MovementTerrainKind.River;
            }
        }

        if (bas != null)
        {
            var id = bas.id;
            if (id == "RiverBridge" || id == "Bridge") return MovementTerrainKind.Bridge;
            if (id == "Sea" || id == "DeepSea") return MovementTerrainKind.Sea;
            if (id == "Plain" || id == "Plains") return MovementTerrainKind.Plains;
            if (id == "Forest" || id == "Woods") return MovementTerrainKind.Woods;
            if (id == "Mountain" || id == "Mountains") return MovementTerrainKind.Mountain;
            if (id == "River" || id == "RiverContainer" || id == "Rivers") return MovementTerrainKind.River;
        }

        return MovementTerrainKind.Plains;
    }

    /// <summary>
    /// 消耗表 (Foot, Mech, Treads, Wheeled): 平原 1,1,1,2 | 森林 1,1,2,3 | 山地/河流 2,1,-,- | 道路/桥梁/建筑 1,1,1,1
    /// </summary>
    private static int GetCostForTerrain(MovementTerrainKind terrain, MovementType movementType)
    {
        switch (terrain)
        {
            case MovementTerrainKind.Road:
            case MovementTerrainKind.Bridge:
            case MovementTerrainKind.Building:
                return 1;
            case MovementTerrainKind.Plains:
                return movementType == MovementType.Wheeled ? 2 : 1;
            case MovementTerrainKind.Woods:
                switch (movementType)
                {
                    case MovementType.Foot: case MovementType.Mech: return 1;
                    case MovementType.Treads: return 2;
                    case MovementType.Wheeled: return 3;
                    default: return 1;
                }
            case MovementTerrainKind.Mountain:
            case MovementTerrainKind.River:
                if (movementType == MovementType.Foot) return 2;
                if (movementType == MovementType.Mech) return 1;
                return Impassable;
            case MovementTerrainKind.Sea:
                return Impassable;
            default:
                return 1;
        }
    }
}
