/// <summary>
/// 终局用例服务：负责胜负查询、归因与终局通知。
/// </summary>
public sealed class GameOverService
{
    private static readonly GameOverService _instance = new GameOverService();

    public static GameOverService Instance => _instance;

    private GameOverService() { }

    public GameConditionResult CheckWinConditions(MapRoot preferredRoot = null)
    {
        return WinConditionService.Instance.CheckWinConditions(preferredRoot);
    }

    public bool TryResolveOutcome(GameConditionResult result, UnitFaction selfFaction, out bool isVictory)
    {
        isVictory = false;
        if (selfFaction == UnitFaction.None)
            return false;
        if (result.HasWinner && result.WinnerFaction == selfFaction)
        {
            isVictory = true;
            return true;
        }
        if (result.HasLoser && result.LoserFaction == selfFaction)
        {
            isVictory = false;
            return true;
        }
        if (result.HasWinner && result.WinnerFaction != UnitFaction.None && result.WinnerFaction != selfFaction)
        {
            isVictory = false;
            return true;
        }
        return false;
    }

    public void NotifyGameOver(GameStateFacade facade, bool isVictory, UnitFaction selfFaction)
    {
        if (facade == null)
            return;
        facade.NotifyGameOver(isVictory, FormatFactionName(selfFaction));
    }

    public static string FormatFactionName(UnitFaction faction)
    {
        return faction == UnitFaction.None ? "Unknown" : faction.ToString();
    }
}
