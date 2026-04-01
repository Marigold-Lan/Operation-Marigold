using UnityEngine;

/// <summary>
/// 胜负判定服务。遍历场景中的 IGameCondition 组件并返回首个有效结果。
/// </summary>
public sealed class WinConditionService
{
    private static readonly WinConditionService _instance = new WinConditionService();

    public static WinConditionService Instance => _instance;

    private WinConditionService() { }

    public GameConditionResult CheckWinConditions(MapRoot preferredRoot = null)
    {
        MonoBehaviour[] conditions;
        if (preferredRoot != null)
        {
            conditions = preferredRoot.GetComponentsInChildren<MonoBehaviour>(true);
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            conditions = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            conditions = Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        }

        for (var i = 0; i < conditions.Length; i++)
        {
            if (conditions[i] is IGameCondition condition)
            {
                var result = condition.Check();
                if (result.HasWinner || result.HasLoser)
                    return result;
            }
        }

        return GameConditionResult.None;
    }
}
