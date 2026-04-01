namespace OperationMarigold.GameplayEvents
{
    public enum AttackFailReason
    {
        Unknown = 0,
        AlreadyAttacking,
        InvalidAttackerOrTarget,
        MissingUnitData,
        AttackerDead,
        TargetDead,
        NoValidWeapon,
        OutOfRange,
        NoDamageCapability,
    }
}

