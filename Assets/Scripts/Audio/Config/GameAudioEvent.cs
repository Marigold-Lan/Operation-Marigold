public enum GameAudioEvent
{
    None = 0,

    CursorMove = 10,
    UiConfirm = 11,
    UiCancel = 12,
    AttackTargetInvalid = 13,
    CursorRapidStart = 14,
    CursorRapidEnd = 15,
    SelectedCellChanged = 16,
    SpawnMenuOpened = 17,
    TypewriterCharacterTyped = 18,

    TurnStarted = 100,
    TurnEnded = 101,
    DayChanged = 102,
    UnitDamaged = 103,
    UnitDeath = 104,
    BuildingCaptured = 105,
    UnitSpawned = 106,
    UnitMoveStarted = 107,
    UnitMoveEnded = 108,

    /// <summary>沿路径每走完一格并落格后（可映射为多次脚步/履带声）。</summary>
    UnitMoveStep = 111,
    /// <summary>该步落格时出发格与目标格世界高度差超过阈值（用于坡地/悬崖等）。</summary>
    UnitMoveJump = 112,
    /// <summary>移动中因朝向与下一步方向偏差而开始转身时。</summary>
    UnitMoveTurn = 113,
    /// <summary>单位发起攻击（开火/出手瞬间，含反击起手）。</summary>
    UnitAttackFire = 114,
    /// <summary>单位被攻击结算命中、扣血前瞬间（与 <see cref="GameAudioEvent.UnitDamaged"/> 的 HP 变化可分别映射）。</summary>
    UnitHitReceived = 115,
    /// <summary>运输单位装载其他单位成功。</summary>
    UnitTransportLoad = 116,
    /// <summary>运输单位卸载其他单位成功。</summary>
    UnitTransportUnload = 117,
    /// <summary>补给单位成功为友军补给。</summary>
    UnitSupplyPerformed = 118,

    /// <summary>结算界面：胜利时播放的 BGM（由 EventMap 映射到 BgmVictory 等）。</summary>
    VictoryResult = 109,
    /// <summary>结算界面：失败时播放的 BGM（由 EventMap 映射到 BgmDefeat 等）。</summary>
    DefeatResult = 110
}
