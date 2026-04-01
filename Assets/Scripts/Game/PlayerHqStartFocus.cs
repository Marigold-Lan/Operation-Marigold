using UnityEngine;

/// <summary>
/// 场景加载后将网格光标与棋盘相机对准指定阵营的总部（BuildingData.isHq）。
/// 须在 GridCursor、BoardCamera 完成 Start 之后执行，故使用较晚的默认执行顺序，且不用 WaitForEndOfFrame，避免首帧错误机位闪屏。
/// </summary>
[DefaultExecutionOrder(500)]
public sealed class PlayerHqStartFocus : MonoBehaviour
{
    [Tooltip("为 None 时使用 TurnManager 玩家顺序中的第一方（通常即人类玩家/先手）。")]
    [SerializeField] private UnitFaction _hqOwnerFaction = UnitFaction.None;

    [Tooltip("找不到匹配总部时是否保持场景默认光标位置。")]
    [SerializeField] private bool _skipIfNoHqFound = true;

    private void Start()
    {
        var faction = ResolveFaction();
        if (faction == UnitFaction.None)
            return;

        var mapRoot = MapRoot.Instance;
        if (mapRoot == null)
            return;

        if (!TryFindHqGridCoord(faction, mapRoot, out var hqCoord))
        {
            if (_skipIfNoHqFound)
                return;
            return;
        }

        var cursor = GridCursor.Instance;
        if (cursor != null)
        {
            if (cursor.mapRoot == null)
                cursor.mapRoot = mapRoot;
            cursor.SetPosition(hqCoord, immediate: true);
        }

        var cam = BoardCamera.Instance;
        if (cam != null)
        {
            if (cam.gridCursor == null)
                cam.gridCursor = cursor;
            var follow = cursor != null ? cursor.transform.position : mapRoot.GridToWorld(hqCoord);
            cam.RecenterPivotSoFollowInDeadZone(follow);
        }
    }

    private UnitFaction ResolveFaction()
    {
        if (_hqOwnerFaction != UnitFaction.None)
            return _hqOwnerFaction;

        if (TurnManager.Instance != null)
        {
            var f = TurnManager.Instance.GetFirstFactionInPlayerOrder();
            if (f != UnitFaction.None)
                return f;
        }

        var facade = GameStateFacade.Instance;
        if (facade != null && facade.Session != null && facade.Session.CurrentFaction != UnitFaction.None)
            return facade.Session.CurrentFaction;

        return UnitFaction.Marigold;
    }

    private static bool TryFindHqGridCoord(UnitFaction faction, MapRoot mapRoot, out Vector2Int coord)
    {
        coord = default;
        var buildings = BuildingQueryService.Instance.FindAllBuildings(mapRoot);
        for (var i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.Data == null || !b.Data.isHq)
                continue;

            if (b.InitialOwnerFaction != faction && b.OwnerFaction != faction)
                continue;

            var cell = b.Cell;
            if (cell == null)
                continue;

            coord = cell.gridCoord;
            return true;
        }

        return false;
    }
}
