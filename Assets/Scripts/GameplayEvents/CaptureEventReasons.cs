namespace OperationMarigold.GameplayEvents
{
    public enum CaptureRejectReason
    {
        Unknown = 0,
        InvalidContext,
        UnitMissing,
        NotInfantryOrMech,
        NoBuildingOnCell,
        TargetNotCaptureTarget,
        AlreadyOwnedByFaction,
        BuildingMissingDataOrState,
        CapturerMissing,
    }

    public enum CaptureInterruptReason
    {
        Unknown = 0,
        AlreadyCapturing,
        InvalidTarget,
        CapturerMissing,
        CapturerDead,
        CapturerLeftTargetCell,
        TargetChanged,
    }

    public enum CaptureResetReason
    {
        Unknown = 0,
        CapturerChanged,
        CapturerLeftCell,
    }
}

