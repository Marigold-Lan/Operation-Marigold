using UnityEngine;

/// <summary>
/// 单局游戏会话状态。仅保存当前天数、玩家索引与当前阵营，不承载业务逻辑。
/// </summary>
public sealed class GameSessionState
{
    private int _currentDay;
    private int _currentPlayerIndex;
    private UnitFaction _currentFaction;
    private bool _isGameOver;

    public GameSessionState(int currentDay = 1, int currentPlayerIndex = 0, UnitFaction currentFaction = UnitFaction.Marigold)
    {
        _currentDay = Mathf.Max(1, currentDay);
        _currentPlayerIndex = Mathf.Max(0, currentPlayerIndex);
        _currentFaction = currentFaction;
        _isGameOver = false;
    }

    /// <summary>终局状态：胜负已出，应冻结输入与回合推进。新开对局时应重置为 false。</summary>
    public bool IsGameOver
    {
        get => _isGameOver;
        set => _isGameOver = value;
    }

    public int CurrentDay
    {
        get => _currentDay;
        set => _currentDay = Mathf.Max(1, value);
    }

    public int CurrentPlayerIndex
    {
        get => _currentPlayerIndex;
        set => _currentPlayerIndex = Mathf.Max(0, value);
    }

    public UnitFaction CurrentFaction
    {
        get => _currentFaction;
        set => _currentFaction = value;
    }

    public void Reset(int currentDay = 1, int currentPlayerIndex = 0, UnitFaction currentFaction = UnitFaction.Marigold)
    {
        CurrentDay = currentDay;
        CurrentPlayerIndex = currentPlayerIndex;
        CurrentFaction = currentFaction;
        IsGameOver = false;
    }
}
