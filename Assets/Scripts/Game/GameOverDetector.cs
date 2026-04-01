using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责检测游戏结束条件（HQ 被占领、胜负轮询），并触发 GameStateFacade.OnGameOver。
/// VictoryUIController、AudioEventBinder 等仅监听该事件，与此类无直接耦合。
/// </summary>
public sealed class GameOverDetector : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameStateFacade _gameStateFacade;

    [Header("己方阵营（用于判定胜负归属）")]
    [SerializeField] private UnitFaction _selfFaction = UnitFaction.Marigold;

    [Header("HQ 占领触发")]
    [SerializeField] private bool _triggerOnHqCaptured = true;

    [Header("轮询胜负条件（兜底）")]
    [SerializeField] private bool _autoCheckWinCondition = true;
    [SerializeField] private float _autoCheckInterval = 0.2f;

    private Coroutine _autoCheckCoroutine;
    private readonly List<BuildingController> _subscribedHqBuildings = new List<BuildingController>();

    private GameStateFacade Facade => _gameStateFacade != null ? _gameStateFacade : GameStateFacade.Instance;

    private void OnEnable()
    {
        SubscribeHqCaptureEvents();
        TryStartAutoCheck();
    }

    private void OnDisable()
    {
        UnsubscribeHqCaptureEvents();
        if (_autoCheckCoroutine != null)
        {
            StopCoroutine(_autoCheckCoroutine);
            _autoCheckCoroutine = null;
        }
    }

    private void TryStartAutoCheck()
    {
        if (!_autoCheckWinCondition || _autoCheckCoroutine != null)
            return;
        var facade = Facade;
        if (facade != null && facade.Session != null && facade.Session.IsGameOver)
            return;
        _autoCheckCoroutine = StartCoroutine(AutoCheckWinConditionRoutine());
    }

    private IEnumerator AutoCheckWinConditionRoutine()
    {
        var waitSeconds = Mathf.Max(0.01f, _autoCheckInterval);

        while (true)
        {
            var facade = Facade;
            if (facade != null && facade.Session != null && facade.Session.IsGameOver)
                yield break;

            if (facade != null)
            {
                var result = GameOverService.Instance.CheckWinConditions(facade.MapRoot);
                if (GameOverService.Instance.TryResolveOutcome(result, _selfFaction, out var isVictory))
                {
                    GameOverService.Instance.NotifyGameOver(facade, isVictory, _selfFaction);
                    yield break;
                }
            }

            var elapsed = 0f;
            while (elapsed < waitSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    private void SubscribeHqCaptureEvents()
    {
        UnsubscribeHqCaptureEvents();
        if (!_triggerOnHqCaptured || _selfFaction == UnitFaction.None)
            return;

#if UNITY_2023_1_OR_NEWER
        var allBuildings = FindObjectsByType<BuildingController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var allBuildings = FindObjectsOfType<BuildingController>(true);
#endif
        for (var i = 0; i < allBuildings.Length; i++)
        {
            var building = allBuildings[i];
            if (building == null || building.Data == null || !building.Data.isHq)
                continue;
            building.OnCaptured += HandleHqCaptured;
            _subscribedHqBuildings.Add(building);
        }
    }

    private void UnsubscribeHqCaptureEvents()
    {
        for (var i = 0; i < _subscribedHqBuildings.Count; i++)
        {
            var building = _subscribedHqBuildings[i];
            if (building != null)
                building.OnCaptured -= HandleHqCaptured;
        }
        _subscribedHqBuildings.Clear();
    }

    private void HandleHqCaptured(UnitFaction oldOwner, UnitFaction newOwner)
    {
        var facade = Facade;
        if (facade != null && facade.Session != null && facade.Session.IsGameOver)
            return;
        if (_selfFaction == UnitFaction.None)
            return;

        if (newOwner == _selfFaction)
        {
            GameOverService.Instance.NotifyGameOver(facade, true, _selfFaction);
            return;
        }
        if (oldOwner == _selfFaction)
            GameOverService.Instance.NotifyGameOver(facade, false, _selfFaction);
    }
}
