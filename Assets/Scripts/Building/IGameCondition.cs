/// <summary>
/// 影响游戏胜负的建筑接口。仅总部实现。
/// </summary>
public interface IGameCondition
{
    /// <summary>
    /// 检查胜负条件。如总部陷落则某方失败。
    /// </summary>
    GameConditionResult Check();
}

/// <summary>
/// 胜负判定结果。
/// </summary>
public struct GameConditionResult
{
    /// <summary>是否已有胜者。</summary>
    public bool HasWinner;

    /// <summary>胜方阵营。</summary>
    public UnitFaction WinnerFaction;

    /// <summary>是否已有败者。</summary>
    public bool HasLoser;

    /// <summary>败方阵营。</summary>
    public UnitFaction LoserFaction;

    public static GameConditionResult None => new GameConditionResult
    {
        HasWinner = false,
        WinnerFaction = UnitFaction.None,
        HasLoser = false,
        LoserFaction = UnitFaction.None
    };
}
