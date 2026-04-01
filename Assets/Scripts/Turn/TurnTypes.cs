using UnityEngine;

/// <summary>
/// 回合内的阶段：回合开始、主要行动、回合结束。
/// </summary>
public enum TurnPhase
{
    Start,
    Main,
    End
}

/// <summary>
/// 当前回合上下文信息，供 TurnManager 与其他系统之间传递使用。
/// </summary>
public struct TurnContext
{
    /// <summary>当前天数（Day），从 1 开始。</summary>
    public int Day;

    /// <summary>当前玩家在玩家顺序列表中的索引。</summary>
    public int PlayerIndex;

    /// <summary>当前行动玩家所属阵营。</summary>
    public UnitFaction Faction;

    /// <summary>当前回合阶段。</summary>
    public TurnPhase Phase;
}

