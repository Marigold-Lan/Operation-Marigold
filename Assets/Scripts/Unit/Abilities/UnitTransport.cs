using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运输能力实现。处理装载（Load）和卸载（Drop）其他单位。
/// </summary>
public class UnitTransport : MonoBehaviour, ITransporter
{
    public static event System.Action<UnitController, UnitController> OnLoaded;
    public static event System.Action<UnitController, UnitController, Vector2Int> OnDropped;

    [SerializeField] private int _capacity = 1;

    private readonly List<UnitController> _loaded = new List<UnitController>();
    private UnitHealth _health;
    private bool _cargoKilledOnDeath;

    public int LoadedCount => _loaded.Count;
    public int Capacity => _capacity;
    public IReadOnlyList<UnitController> LoadedUnits => _loaded;

    private void Awake()
    {
        _health = GetComponent<UnitHealth>();
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnDeath += HandleTransporterDeath;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDeath -= HandleTransporterDeath;
    }

    public bool Load(UnitController unit)
    {
        if (unit == null || _loaded.Count >= _capacity) return false;
        if (_loaded.Contains(unit)) return false;

        var transporter = GetComponent<UnitController>();
        if (unit.CurrentCell != null)
        {
            unit.CurrentCell.ClearUnit();
            unit.CurrentCell = null;
        }
        _loaded.Add(unit);
        unit.gameObject.SetActive(false);

        if (transporter != null)
            OnLoaded?.Invoke(transporter, unit);
        return true;
    }

    public bool Drop(UnitController unit, Vector2Int targetCoord)
    {
        if (unit == null || !_loaded.Contains(unit)) return false;

        var controller = GetComponent<UnitController>();
        if (controller == null || controller.MapRoot == null) return false;
        if (!controller.MapRoot.IsInBounds(targetCoord)) return false;

        var movement = unit.GetComponent<UnitMovement>();
        if (movement == null) return false;

        var cell = controller.MapRoot.GetCellAt(targetCoord);
        if (cell == null || cell.HasUnit)
            return false;

        _loaded.Remove(unit);
        unit.gameObject.SetActive(true);
        unit.GridCoord = targetCoord;
        unit.transform.position = controller.MapRoot.GridToWorld(targetCoord);

        unit.CurrentCell = cell;
        cell.SetUnit(unit.gameObject);

        OnDropped?.Invoke(controller, unit, targetCoord);
        return true;
    }

    private void HandleTransporterDeath()
    {
        if (_cargoKilledOnDeath)
            return;
        _cargoKilledOnDeath = true;

        // Requirement: when transporter dies, all carried units should also die.
        // Carried units are inactive, so their own coroutines/visual flows may not run reliably.
        for (var i = 0; i < _loaded.Count; i++)
        {
            var cargo = _loaded[i];
            if (cargo == null) continue;
            if (cargo.gameObject != null)
                Destroy(cargo.gameObject);
        }
        _loaded.Clear();
    }
}
