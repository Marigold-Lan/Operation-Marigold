using UnityEngine;

public interface IUnitReadView
{
    bool Alive { get; }
    UnitFaction Faction { get; }
    Vector2Int GridCoord { get; }
    int Hp { get; }
    int MaxHp { get; }
    int Fuel { get; }
    int MaxFuel { get; }
    int Ammo { get; }
    int MaxAmmo { get; }
    bool HasActed { get; }
    bool HasMovedThisTurn { get; }
    MovementType MovementType { get; }
}
