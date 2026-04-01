using UnityEngine;

public interface IBuildingReadView
{
    Vector2Int GridCoord { get; }
    UnitFaction OwnerFaction { get; }
    bool IsHq { get; }
    bool IsFactory { get; }
    int IncomePerTurn { get; }
    int CaptureHp { get; }
    int MaxCaptureHp { get; }
    int CaptureDamagePerStep { get; }
}
