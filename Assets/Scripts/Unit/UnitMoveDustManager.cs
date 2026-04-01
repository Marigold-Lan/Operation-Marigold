using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景级组件：监听所有单位的 UnitMovement 开始/结束，在移动期间为该单位播放 Dust 粒子。
/// 不挂在单位上，保持 UnitMovement 专属于单位、表现逻辑与单位解耦。
/// </summary>
public class UnitMoveDustManager : MonoBehaviour
{
    [Tooltip("移动时在此单位脚下实例化的 Dust 粒子预制体。")]
    [SerializeField] private GameObject _dustPrefab;

    private readonly HashSet<UnitMovement> _registered = new HashSet<UnitMovement>();
    private readonly Dictionary<UnitMovement, (GameObject instance, ParticleSystem particle)> _activeDust = new Dictionary<UnitMovement, (GameObject, ParticleSystem)>();
    private readonly List<FactorySpawner> _spawnersSubscribed = new List<FactorySpawner>();

    private void Start()
    {
        if (_dustPrefab == null) return;

        var movements = FindObjectsByType<UnitMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < movements.Length; i++)
            Register(movements[i]);

        var spawners = FindObjectsByType<FactorySpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < spawners.Length; i++)
        {
            spawners[i].OnUnitSpawned += OnUnitSpawned;
            _spawnersSubscribed.Add(spawners[i]);
        }
    }

    private void OnDisable()
    {
        for (var i = 0; i < _spawnersSubscribed.Count; i++)
        {
            if (_spawnersSubscribed[i] != null)
                _spawnersSubscribed[i].OnUnitSpawned -= OnUnitSpawned;
        }
        _spawnersSubscribed.Clear();

        foreach (var m in _registered)
        {
            if (m != null)
            {
                m.OnMoveStarted -= HandleMoveStarted;
                m.OnMoveEnded -= HandleMoveEnded;
            }
        }
        _registered.Clear();

        foreach (var kv in _activeDust)
        {
            if (kv.Value.instance != null)
                Destroy(kv.Value.instance);
        }
        _activeDust.Clear();
    }

    private void OnUnitSpawned(UnitController unit)
    {
        if (unit == null || _dustPrefab == null) return;
        var movement = unit.GetComponent<UnitMovement>();
        if (movement != null)
            Register(movement);
    }

    private void Register(UnitMovement movement)
    {
        if (movement == null || _registered.Contains(movement)) return;
        _registered.Add(movement);
        movement.OnMoveStarted += HandleMoveStarted;
        movement.OnMoveEnded += HandleMoveEnded;
    }

    private void HandleMoveStarted(UnitMovement movement)
    {
        if (movement == null || _dustPrefab == null) return;
        if (_activeDust.ContainsKey(movement)) return;

        var t = movement.transform;
        var instance = Instantiate(_dustPrefab, t.position, Quaternion.identity, t);
        var particle = instance.GetComponentInChildren<ParticleSystem>();
        if (particle != null)
            particle.Play(true);
        _activeDust[movement] = (instance, particle);
    }

    private void HandleMoveEnded(UnitMovement movement)
    {
        if (movement == null) return;
        if (!_activeDust.TryGetValue(movement, out var pair)) return;

        if (pair.particle != null)
            pair.particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (pair.instance != null)
            Destroy(pair.instance);
        _activeDust.Remove(movement);
    }
}
