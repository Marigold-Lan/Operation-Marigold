namespace OperationMarigold.GameplayEvents
{
    public enum MoveFailReason
    {
        Unknown = 0,
        MissingControllerOrMapRoot,
        DestinationNotOccupiable,
        DestinationCellMissing,
    }

    public enum MoveStopReason
    {
        Unknown = 0,
        TraversalCostUnavailable,
        InsufficientFuel,
    }
}

