using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioEventBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioEventMapSO _eventMap;
    [SerializeField] private GameAudioManager _audioManager;

    [Header("Result BGM")]
    [Tooltip("结算界面展示时：淡出战斗 BGM 并切入胜利/失败 BGM 的时长（秒）。")]
    [SerializeField] private float _resultBgmFadeSeconds = 0.5f;

    private readonly Dictionary<UnitHealth, Action<int, int>> _unitHpChangedHandlers = new Dictionary<UnitHealth, Action<int, int>>();
    private readonly Dictionary<UnitHealth, Action> _unitDeathHandlers = new Dictionary<UnitHealth, Action>();
    private readonly Dictionary<BuildingController, Action<UnitFaction, UnitFaction>> _buildingCapturedHandlers = new Dictionary<BuildingController, Action<UnitFaction, UnitFaction>>();
    private readonly Dictionary<FactorySpawner, Action<UnitController>> _spawnedHandlers = new Dictionary<FactorySpawner, Action<UnitController>>();
    private readonly Dictionary<FactorySpawner, Action<FactorySpawner>> _spawnMenuHandlers = new Dictionary<FactorySpawner, Action<FactorySpawner>>();
    private readonly HashSet<GameAudioEvent> _missingEventWarned = new HashSet<GameAudioEvent>();
    private bool _missingEventMapWarned;

    private IGameAudioService AudioService => _audioManager != null ? _audioManager : GameAudioManager.Instance;

    private void Awake()
    {
        if (_audioManager == null)
            _audioManager = GameAudioManager.Instance;
        // 结算 BGM 在 Awake 订阅游戏结束事件，避免 OnEnable 顺序或本体被禁用导致收不到
        GameStateFacade.OnGameOver += HandleGameOver;
    }

    private void OnDestroy()
    {
        GameStateFacade.OnGameOver -= HandleGameOver;
    }

    private void OnEnable()
    {
        SubscribeStaticEvents();
        SubscribeDynamicEvents();
    }

    private void OnDisable()
    {
        UnsubscribeStaticEvents();
        UnsubscribeDynamicEvents();
    }

    private void SubscribeStaticEvents()
    {
        TurnManager.OnTurnStarted += HandleTurnStarted;
        TurnManager.OnTurnEnded += HandleTurnEnded;
        TurnManager.OnDayChanged += HandleDayChanged;
        InputManager.OnCursorMoved += HandleCursorMoved;
        SelectionManager.OnAttackTargetInvalid += HandleAttackTargetInvalid;
        SelectionManager.OnSelectedCellChanged += HandleSelectedCellChanged;
        InputManager.OnCursorMoveSpeedChanged += HandleCursorMoveSpeedChanged;
        TypewriterUtility.OnCharacterTyped += HandleTypewriterCharacterTyped;
    }

    private void UnsubscribeStaticEvents()
    {
        TurnManager.OnTurnStarted -= HandleTurnStarted;
        TurnManager.OnTurnEnded -= HandleTurnEnded;
        TurnManager.OnDayChanged -= HandleDayChanged;
        InputManager.OnCursorMoved -= HandleCursorMoved;
        SelectionManager.OnAttackTargetInvalid -= HandleAttackTargetInvalid;
        SelectionManager.OnSelectedCellChanged -= HandleSelectedCellChanged;
        InputManager.OnCursorMoveSpeedChanged -= HandleCursorMoveSpeedChanged;
        TypewriterUtility.OnCharacterTyped -= HandleTypewriterCharacterTyped;
    }

    private void HandleGameOver(bool isVictory, string _)
    {
        var service = AudioService;
        if (service != null)
            service.StopBgm(_resultBgmFadeSeconds);
        PlayBgmForEvent(isVictory ? GameAudioEvent.VictoryResult : GameAudioEvent.DefeatResult, _resultBgmFadeSeconds);
    }

    private void SubscribeDynamicEvents()
    {
        var healths = FindAll<UnitHealth>();
        for (var i = 0; i < healths.Length; i++)
            RegisterUnitHealth(healths[i]);

        var buildings = FindAll<BuildingController>();
        for (var i = 0; i < buildings.Length; i++)
            RegisterBuilding(buildings[i]);

        var spawners = FindAll<FactorySpawner>();
        for (var i = 0; i < spawners.Length; i++)
            RegisterSpawner(spawners[i]);
    }

    private void UnsubscribeDynamicEvents()
    {
        foreach (var kv in _unitHpChangedHandlers)
        {
            if (kv.Key != null)
                kv.Key.OnHpChanged -= kv.Value;
        }
        _unitHpChangedHandlers.Clear();

        foreach (var kv in _unitDeathHandlers)
        {
            if (kv.Key != null)
                kv.Key.OnDeath -= kv.Value;
        }
        _unitDeathHandlers.Clear();

        foreach (var kv in _buildingCapturedHandlers)
        {
            if (kv.Key != null)
                kv.Key.OnCaptured -= kv.Value;
        }
        _buildingCapturedHandlers.Clear();

        foreach (var kv in _spawnedHandlers)
        {
            if (kv.Key != null)
                kv.Key.OnUnitSpawned -= kv.Value;
        }
        _spawnedHandlers.Clear();

        foreach (var kv in _spawnMenuHandlers)
        {
            if (kv.Key != null)
                kv.Key.OnShowSpawnMenuRequested -= kv.Value;
        }
        _spawnMenuHandlers.Clear();
    }

    private void RegisterUnitHealth(UnitHealth health)
    {
        if (health == null || _unitHpChangedHandlers.ContainsKey(health))
            return;

        Action<int, int> hpChanged = (oldHp, newHp) =>
        {
            if (newHp < oldHp)
                PlaySfx(GameAudioEvent.UnitDamaged, health.transform.position);
        };

        Action death = () =>
        {
            PlaySfx(GameAudioEvent.UnitDeath, health.transform.position);
        };

        _unitHpChangedHandlers.Add(health, hpChanged);
        _unitDeathHandlers.Add(health, death);
        health.OnHpChanged += hpChanged;
        health.OnDeath += death;
    }

    private void RegisterBuilding(BuildingController building)
    {
        if (building == null || _buildingCapturedHandlers.ContainsKey(building))
            return;

        Action<UnitFaction, UnitFaction> captured = (_, __) =>
        {
            PlaySfx(GameAudioEvent.BuildingCaptured, building.transform.position);
        };

        _buildingCapturedHandlers.Add(building, captured);
        building.OnCaptured += captured;
    }

    private void RegisterSpawner(FactorySpawner spawner)
    {
        if (spawner == null || _spawnedHandlers.ContainsKey(spawner))
            return;

        Action<UnitController> spawned = unit =>
        {
            if (unit == null)
                return;

            PlaySfx(GameAudioEvent.UnitSpawned, unit.transform.position);
            var health = unit.GetComponent<UnitHealth>();
            RegisterUnitHealth(health);
        };

        _spawnedHandlers.Add(spawner, spawned);
        spawner.OnUnitSpawned += spawned;

        Action<FactorySpawner> showMenu = _ =>
        {
            PlaySfx(GameAudioEvent.SpawnMenuOpened, spawner.transform.position);
        };
        _spawnMenuHandlers.Add(spawner, showMenu);
        spawner.OnShowSpawnMenuRequested += showMenu;
    }

    private void HandleTurnStarted(TurnContext context)
    {
        PlaySfx(GameAudioEvent.TurnStarted, Vector3.zero);

        // 战斗开始时（第一回合：Day 1、首位玩家）播放战斗 BGM
        if (context.Day == 1 && context.PlayerIndex == 0)
        {
            var service = AudioService;
            if (service != null)
                service.PlayBgm(AudioCueId.BgmBattle, 0.5f);
        }
    }

    private void HandleTurnEnded(TurnContext context)
    {
        PlaySfx(GameAudioEvent.TurnEnded, Vector3.zero);
    }

    private void HandleDayChanged(int day)
    {
        PlaySfx(GameAudioEvent.DayChanged, Vector3.zero);
    }

    private void HandleCursorMoved()
    {
        // 任意光标（网格或菜单）移动时统一播放；位置用零向量，作为 UI 音效不参与 3D 定位。
        PlaySfx(GameAudioEvent.CursorMove, Vector3.zero);
    }

    private void HandleAttackTargetInvalid(Vector2Int coord)
    {
        var map = MapRoot.Instance;
        var pos = map != null && map.IsInBounds(coord) ? map.GridToWorld(coord) : Vector3.zero;
        PlaySfx(GameAudioEvent.AttackTargetInvalid, pos);
    }

    private void HandleCursorMoveSpeedChanged(bool isRapid)
    {
        PlaySfx(isRapid ? GameAudioEvent.CursorRapidStart : GameAudioEvent.CursorRapidEnd, Vector3.zero);
    }

    private void HandleSelectedCellChanged(Cell cell)
    {
        if (cell == null)
            return;
        PlaySfx(GameAudioEvent.SelectedCellChanged, cell.transform.position);
    }

    private void HandleTypewriterCharacterTyped(int index, char c)
    {
        PlaySfx(GameAudioEvent.TypewriterCharacterTyped, Vector3.zero);
    }

    /// <summary>
    /// 根据事件映射播放 BGM（用于结算等）。映射在 AudioEventMapSO 中配置，运行「补齐所有 GameAudioEvent 映射」可补全 VictoryResult/DefeatResult。
    /// </summary>
    public bool PlayBgmForEvent(GameAudioEvent gameEvent, float fadeSeconds = 0.5f)
    {
        if (_eventMap == null)
            return false;
        if (!_eventMap.TryGetCueId(gameEvent, out var cueId) || cueId == AudioCueId.None)
            return false;

        var service = AudioService;
        if (service == null)
            return false;

        return service.PlayBgm(cueId, fadeSeconds);
    }

    private void PlaySfx(GameAudioEvent gameEvent, Vector3 worldPos)
    {
        if (_eventMap == null)
        {
            if (!_missingEventMapWarned)
                _missingEventMapWarned = true;
            return;
        }

        if (!_eventMap.TryGetCueId(gameEvent, out var cueId))
        {
            _missingEventWarned.Add(gameEvent);
            return;
        }

        var service = AudioService;
        if (service == null)
            return;

        service.PlaySfx(cueId, worldPos);
    }

    private static T[] FindAll<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return FindObjectsOfType<T>();
#endif
    }
}
