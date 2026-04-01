using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景级组件：监听所有单位死亡事件，在死亡位置播放一次 Dust 粒子。
/// 不挂在单位上，保持 UnitHealth/UnitView 等组件职责单一、表现逻辑集中管理。
/// </summary>
public class UnitDeathDustManager : MonoBehaviour
{
    [Tooltip("单位死亡时在其位置实例化的 Dust 粒子预制体。")]
    [SerializeField] private GameObject _deathDustPrefab;

    [Tooltip("死亡粒子世界坐标偏移（例如略微抬高）。")]
    [SerializeField] private Vector3 _worldOffset = Vector3.zero;

    [Tooltip("销毁粒子的兜底时长（秒）。0 表示不强制销毁。")]
    [SerializeField, Min(0f)] private float _destroyAfterSeconds = 2f;

    private readonly HashSet<UnitHealth> _registered = new HashSet<UnitHealth>();
    private readonly Dictionary<UnitHealth, System.Action> _deathHandlers = new Dictionary<UnitHealth, System.Action>();
    private readonly List<FactorySpawner> _spawnersSubscribed = new List<FactorySpawner>();

    private void Start()
    {
        if (_deathDustPrefab == null) return;

        var healths = FindObjectsByType<UnitHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < healths.Length; i++)
            Register(healths[i]);

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

        foreach (var kv in _deathHandlers)
        {
            if (kv.Key != null && kv.Value != null)
                kv.Key.OnDeath -= kv.Value;
        }
        _deathHandlers.Clear();
        _registered.Clear();
    }

    private void OnUnitSpawned(UnitController unit)
    {
        if (unit == null || _deathDustPrefab == null) return;
        var health = unit.GetComponent<UnitHealth>();
        if (health != null)
            Register(health);
    }

    private void Register(UnitHealth health)
    {
        if (health == null || _registered.Contains(health)) return;
        _registered.Add(health);

        System.Action handler = () => HandleUnitDeath(health);
        _deathHandlers.Add(health, handler);
        health.OnDeath += handler;
    }

    private void HandleUnitDeath(UnitHealth health)
    {
        if (health == null || _deathDustPrefab == null) return;

        var t = health.transform;
        var pos = t.position + _worldOffset;
        var instance = Instantiate(_deathDustPrefab, pos, Quaternion.identity, null);
        var particle = instance != null ? instance.GetComponentInChildren<ParticleSystem>() : null;
        if (particle != null)
            particle.Play(true);

        if (instance != null && _destroyAfterSeconds > 0f)
            Destroy(instance, _destroyAfterSeconds);
    }
}

