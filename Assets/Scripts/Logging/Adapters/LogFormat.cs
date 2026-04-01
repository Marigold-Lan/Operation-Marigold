using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    internal static class LogFormat
    {
        public static string Faction(UnitFaction faction)
        {
            return faction.ToString();
        }

        public static string Coord(Vector2Int coord)
        {
            return $"({coord.x},{coord.y})";
        }

        public static string UnitName(UnitController unit)
        {
            if (unit == null) return "UnknownUnit";
            var data = unit.Data;
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.displayName)) return data.displayName;
                if (!string.IsNullOrWhiteSpace(data.id)) return data.id;
                if (!string.IsNullOrWhiteSpace(data.name)) return data.name;
            }
            return unit.name;
        }

        public static string BuildingName(BuildingController building)
        {
            if (building == null) return "UnknownBuilding";
            var data = building.Data;
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.displayName)) return data.displayName;
                if (!string.IsNullOrWhiteSpace(data.id)) return data.id;
                if (!string.IsNullOrWhiteSpace(data.name)) return data.name;
            }
            return building.name;
        }
    }
}

