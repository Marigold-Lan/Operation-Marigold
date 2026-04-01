using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景级组件：在回合开场动画结束后，为当前阵营所有尚未行动的单位脚下放置一个 Mark 预制体；
/// 当单位行动完后（HasActed 为 true）移除该预制体。
/// 订阅 TurnManager.OnTurnIntroAnimationComplete，与 UnitMoveDustManager 类似，表现与单位逻辑解耦。
/// </summary>
public class UnitCanActMarkManager : MonoBehaviour
{
    [Tooltip("可行动时在单位脚下实例化的 Mark 预制体。")]
    [SerializeField] private GameObject _markPrefab;

    [Tooltip("开启后在 Console 输出详细日志，便于排查不显示 Mark 的问题。")]
    [SerializeField] private bool _debug;

    private readonly Dictionary<UnitController, GameObject> _activeMarks = new Dictionary<UnitController, GameObject>();
    private readonly HashSet<UnitController> _registered = new HashSet<UnitController>();
    private readonly List<FactorySpawner> _spawnersSubscribed = new List<FactorySpawner>();

    private void Awake()
    {
        // 必须在 Awake 中订阅并注册单位。Mark 在开场动画结束后显示（OnTurnIntroAnimationComplete）。
        TurnManager.OnTurnIntroAnimationComplete += HandleTurnIntroAnimationComplete;

        // 使用 Include 以便找到可能挂在未激活物体下的单位（仅对激活且在层级中的单位放置 Mark）
        var units = FindObjectsByType<UnitController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < units.Length; i++)
            RegisterUnit(units[i]);

        var spawners = FindObjectsByType<FactorySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < spawners.Length; i++)
        {
            spawners[i].OnUnitSpawned += OnUnitSpawned;
            _spawnersSubscribed.Add(spawners[i]);
        }

        if (_debug)
        {
            var tm = FindFirstObjectByType<TurnManager>();
            Debug.Log($"[UnitCanActMarkManager] Awake: registered {_registered.Count} units, {_spawnersSubscribed.Count} spawners, prefab={(_markPrefab != null ? "assigned" : "NULL")}, TurnManager={tm != null}");
        }
    }

    private void OnDisable()
    {
        TurnManager.OnTurnIntroAnimationComplete -= HandleTurnIntroAnimationComplete;

        for (var i = 0; i < _spawnersSubscribed.Count; i++)
        {
            if (_spawnersSubscribed[i] != null)
                _spawnersSubscribed[i].OnUnitSpawned -= OnUnitSpawned;
        }
        _spawnersSubscribed.Clear();

        foreach (var unit in _registered)
        {
            if (unit != null)
                unit.OnActed -= HandleUnitActed;
        }
        _registered.Clear();

        foreach (var kv in _activeMarks)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _activeMarks.Clear();
    }

    private void OnUnitSpawned(UnitController unit)
    {
        if (unit == null || _markPrefab == null) return;
        RegisterUnit(unit);
    }

    private void RegisterUnit(UnitController unit)
    {
        if (unit == null || _registered.Contains(unit)) return;
        _registered.Add(unit);
        unit.OnActed += HandleUnitActed;
    }

    private void HandleTurnIntroAnimationComplete(TurnContext context)
    {
        if (_debug)
            Debug.Log($"[UnitCanActMarkManager] HandleTurnIntroAnimationComplete: Faction={context.Faction}, prefab={(_markPrefab != null ? "ok" : "NULL")}, registered={_registered.Count}");

        if (_markPrefab == null || context.Faction == UnitFaction.None)
        {
            if (_debug && (_markPrefab == null || context.Faction == UnitFaction.None))
                Debug.LogWarning("[UnitCanActMarkManager] 未执行: prefab 未赋值或当前阵营为 None。");
            return;
        }

        // 先移除本回合之前的所有 Mark
        foreach (var kv in _activeMarks)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _activeMarks.Clear();

        // 为当前阵营且尚未行动、且在层级中激活的单位放置 Mark
        var placed = 0;
        foreach (var unit in _registered)
        {
            if (unit == null) continue;
            if (!unit.gameObject.activeInHierarchy) continue;
            if (unit.OwnerFaction != context.Faction || unit.HasActed) continue;

            var t = unit.transform;
            var instance = Instantiate(_markPrefab, t.position, Quaternion.identity, t);
            var particle = instance.GetComponentInChildren<ParticleSystem>();
            if (particle != null)
                particle.Play(true);
            _activeMarks[unit] = instance;
            placed++;
            if (_debug)
                Debug.Log($"[UnitCanActMarkManager] 放置 Mark: {unit.name} (Faction={unit.OwnerFaction}, HasActed={unit.HasActed})");
        }

        if (_debug)
            Debug.Log($"[UnitCanActMarkManager] 本回合共放置 {placed} 个 Mark。");
    }

    private void HandleUnitActed(UnitController unit)
    {
        if (unit == null) return;
        if (!_activeMarks.TryGetValue(unit, out var mark)) return;

        if (_debug)
            Debug.Log($"[UnitCanActMarkManager] 移除 Mark: {unit.name}");
        if (mark != null)
        {
            var particle = mark.GetComponentInChildren<ParticleSystem>();
            if (particle != null)
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(mark);
        }
        _activeMarks.Remove(unit);
    }
}
