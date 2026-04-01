using UnityEngine;

/// <summary>
/// 总部专用胜负判定组件。只需挂在总部建筑上。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingController))]
public sealed class HqGameCondition : MonoBehaviour, IGameCondition
{
    private BuildingController _building;

    private void Awake()
    {
        _building = GetComponent<BuildingController>();
    }

    public GameConditionResult Check()
    {
        if (_building == null)
            return GameConditionResult.None;
        if (_building.Data == null || !_building.Data.isHq)
            return GameConditionResult.None;

        var initialOwner = _building.InitialOwnerFaction;
        var currentOwner = _building.OwnerFaction;
        if (initialOwner == UnitFaction.None || currentOwner == UnitFaction.None)
            return GameConditionResult.None;

        // 总部归属变化则判定原所属失败、当前所属胜利。
        if (currentOwner != initialOwner)
        {
            return new GameConditionResult
            {
                HasWinner = true,
                WinnerFaction = currentOwner,
                HasLoser = true,
                LoserFaction = initialOwner
            };
        }

        return GameConditionResult.None;
    }
}
