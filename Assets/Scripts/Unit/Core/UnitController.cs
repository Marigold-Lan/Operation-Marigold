using System;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 单位对外统一接口。持有动态状态，统筹各组件。不依赖 UnitView，AI 模拟时可单独运行。
/// </summary>
public class UnitController : MonoBehaviour, IUnitReadView
{
    [Header("配置")]
    [SerializeField] private UnitData _data;
    [SerializeField] private MapRoot _mapRoot;

    [Header("动态状态")]
    [SerializeField] private Vector2Int _gridCoord;
    [SerializeField] private int _currentFuel;
    [SerializeField] private int _currentAmmo;
    [SerializeField] private bool _hasActed;
    [SerializeField] private bool _hasMovedThisTurn;
    [FormerlySerializedAs("_ownerId")]
    [SerializeField] private UnitFaction _ownerFaction = UnitFaction.Marigold;

    private Cell _currentCell;
    private UnitHealth _health;
    private UnitMovement _movement;
    private UnitCombat _combat;
    private UnitView _view;

    /// <summary>
    /// 静态配置数据（只读）。
    /// </summary>
    public UnitData Data => _data;

    /// <summary>
    /// 地图根，用于寻路与坐标转换。
    /// </summary>
    public MapRoot MapRoot => _mapRoot;

    /// <summary>
    /// 当前所在格子坐标。
    /// </summary>
    public Vector2Int GridCoord
    {
        get => _gridCoord;
        set => _gridCoord = value;
    }

    /// <summary>
    /// 当前所在格子。
    /// </summary>
    public Cell CurrentCell
    {
        get => _currentCell;
        set => _currentCell = value;
    }

    /// <summary>
    /// 当前燃料。
    /// </summary>
    public int CurrentFuel
    {
        get => _currentFuel;
        set => _currentFuel = Mathf.Clamp(value, 0, _data != null ? _data.maxFuel : 99);
    }

    /// <summary>
    /// 当前弹药。
    /// </summary>
    public int CurrentAmmo
    {
        get => _currentAmmo;
        set => _currentAmmo = Mathf.Clamp(value, 0, _data != null ? _data.MaxPrimaryAmmo : 0);
    }

    /// <summary>
    /// 本回合是否已行动完。
    /// </summary>
    public bool HasActed
    {
        get => _hasActed;
        set
        {
            if (_hasActed == value)
                return;
            _hasActed = value;
            if (_hasActed)
                OnActed?.Invoke(this);
        }
    }

    /// <summary>
    /// 本回合是否发生过移动。
    /// </summary>
    public bool HasMovedThisTurn
    {
        get => _hasMovedThisTurn;
        set => _hasMovedThisTurn = value;
    }

    /// <summary>
    /// 当本回合该单位完成一次行动（HasActed 被设为 true）时触发。供标记、音效等表现层使用。
    /// </summary>
    public event Action<UnitController> OnActed;

    /// <summary>
    /// 所属阵营。
    /// </summary>
    public UnitFaction OwnerFaction
    {
        get => _ownerFaction;
        set => _ownerFaction = value;
    }

    public UnitHealth Health => _health;
    public UnitMovement Movement => _movement;
    public UnitCombat Combat => _combat;
    public UnitView View => _view;
    public bool Alive => _health == null || !_health.IsDead;
    public UnitFaction Faction => OwnerFaction;
    public int Hp => _health != null ? _health.CurrentHp : (_data != null ? _data.maxHp : 0);
    public int MaxHp => _data != null ? _data.maxHp : 0;
    public int Fuel => _currentFuel;
    public int MaxFuel => _data != null ? _data.maxFuel : 0;
    public int Ammo => _currentAmmo;
    public int MaxAmmo => _data != null ? _data.MaxPrimaryAmmo : 0;
    public MovementType MovementType => _data != null ? _data.movementType : MovementType.Foot;

    private void Awake()
    {
        CacheComponents();
        TurnManager.OnTurnStarted += HandleTurnStarted;
    }

    private void OnDestroy()
    {
        TurnManager.OnTurnStarted -= HandleTurnStarted;
    }

    private void Start()
    {
        SnapToGridCoord();
    }

    /// <summary>
    /// 开局时将单位固定到其对应的网格坐标。适用于场景中预摆放的单位，以及工厂生成的单位（确保位置与格子同步）。
    /// </summary>
    private void SnapToGridCoord()
    {
        if (_mapRoot == null)
            _mapRoot = MapRoot.Instance;
        if (_mapRoot == null || !_mapRoot.IsInBounds(_gridCoord))
            return;

        transform.position = _mapRoot.GridToWorld(_gridCoord);

        var cell = _mapRoot.GetCellAt(_gridCoord);
        if (cell != null)
        {
            if (_currentCell != cell)
            {
                _currentCell?.ClearUnit();
                _currentCell = cell;
            }
            cell.SetUnit(gameObject);
        }
    }

    /// <summary>
    /// 缓存各组件引用。UnitView 可能为 null（AI 模拟时不挂载）。
    /// </summary>
    private void CacheComponents()
    {
        _health = GetComponent<UnitHealth>();
        _movement = GetComponent<UnitMovement>();
        _combat = GetComponent<UnitCombat>();
        _view = GetComponent<UnitView>();
    }

    /// <summary>
    /// 使用 UnitData 初始化单位（生成时调用）。
    /// </summary>
    public void Initialize(UnitData data, MapRoot mapRoot, Cell spawnCell, UnitFaction ownerFaction)
    {
        _data = data;
        _mapRoot = mapRoot;
        _currentCell = spawnCell;
        _gridCoord = spawnCell != null ? spawnCell.gridCoord : Vector2Int.zero;
        OwnerFaction = ownerFaction;

        _currentFuel = data != null ? data.maxFuel : 99;
        _currentAmmo = data != null ? data.MaxPrimaryAmmo : 0;
        _hasActed = false;
        _hasMovedThisTurn = false;

        if (_health != null)
            _health.Initialize(data != null ? data.maxHp : 10);

        if (_movement != null)
            _movement.Initialize(this);

        if (_combat != null)
            _combat.Initialize(this);
    }

    /// <summary>
    /// 从现有 Cell 加载时使用（例如读档）。
    /// </summary>
    public void InitializeFromCell(UnitData data, MapRoot mapRoot, Cell cell, UnitFaction ownerFaction)
    {
        Initialize(data, mapRoot, cell, ownerFaction);
    }

    /// <summary>
    /// 回合开始时，根据当前回合阵营自动重置行动状态。
    /// </summary>
    private void HandleTurnStarted(TurnContext context)
    {
        if (context.Faction == UnitFaction.None || context.Faction != OwnerFaction)
            return;

        // 重置行动状态。
        _hasActed = false;
        _hasMovedThisTurn = false;

    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器下：按物体名自动匹配最合适的 UnitData（优先同名）。
    /// </summary>
    public bool TryAutoAssignUnitDataByName()
    {
        var lookupName = NormalizeLookupName(name);
        if (string.IsNullOrWhiteSpace(lookupName))
            return false;

        UnitData best = null;
        var bestScore = int.MinValue;

        var strictNameGuids = AssetDatabase.FindAssets($"t:UnitData {lookupName}");
        for (var i = 0; i < strictNameGuids.Length; i++)
        {
            var candidate = LoadUnitDataFromGuid(strictNameGuids[i]);
            var score = ScoreCandidate(candidate, lookupName);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
        {
            var allUnitDataGuids = AssetDatabase.FindAssets("t:UnitData");
            for (var i = 0; i < allUnitDataGuids.Length; i++)
            {
                var candidate = LoadUnitDataFromGuid(allUnitDataGuids[i]);
                var score = ScoreCandidate(candidate, lookupName);
                if (score <= 0) continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }

        if (best == null)
            return false;

        _data = best;
        EditorUtility.SetDirty(this);
        return true;
    }

    /// <summary>
    /// 编辑器下：按物体名/前缀自动匹配阵营（Lancel 或 Marigold）。
    /// </summary>
    public bool TryAutoAssignFactionByNamePrefix()
    {
        var lookupName = NormalizeLookupName(name);
        if (string.IsNullOrWhiteSpace(lookupName))
            return false;

        var lancelScore = ScoreFactionByName(lookupName, UnitFaction.Lancel);
        var marigoldScore = ScoreFactionByName(lookupName, UnitFaction.Marigold);

        if (lancelScore <= 0 && marigoldScore <= 0)
            return false;

        _ownerFaction = marigoldScore >= lancelScore ? UnitFaction.Marigold : UnitFaction.Lancel;
        EditorUtility.SetDirty(this);
        return true;
    }

    private static UnitData LoadUnitDataFromGuid(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<UnitData>(path);
    }

    private static int ScoreCandidate(UnitData candidate, string lookupName)
    {
        if (candidate == null) return 0;

        var candidateName = NormalizeLookupName(candidate.name);
        var id = NormalizeLookupName(candidate.id);
        var displayName = NormalizeLookupName(candidate.displayName);

        var score = 0;
        if (string.Equals(candidateName, lookupName, System.StringComparison.OrdinalIgnoreCase)) score += 1000;
        if (string.Equals(id, lookupName, System.StringComparison.OrdinalIgnoreCase)) score += 500;
        if (string.Equals(displayName, lookupName, System.StringComparison.OrdinalIgnoreCase)) score += 300;

        var path = AssetDatabase.GetAssetPath(candidate);
        if (!string.IsNullOrEmpty(path) && path.Contains("/Blueprint/Unit/")) score += 50;
        return score;
    }

    private static string NormalizeLookupName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var normalized = raw.Trim();
        normalized = normalized.Replace("(Clone)", "").Trim();
        return normalized;
    }

    private static int ScoreFactionByName(string lookupName, UnitFaction faction)
    {
        var name = lookupName.ToLowerInvariant();
        var key = faction == UnitFaction.Lancel ? "lancel" : "marigold";
        var keyShort = faction == UnitFaction.Lancel ? "lan" : "mari";

        var score = 0;
        if (name.StartsWith(key + "_") || name.StartsWith(key + "-") || name.StartsWith(key + "."))
            score += 1000;
        if (name.StartsWith(key))
            score += 700;
        if (name.Contains("_" + key) || name.Contains("-" + key) || name.Contains("." + key))
            score += 300;
        if (name.Contains(key))
            score += 200;
        if (name.StartsWith(keyShort))
            score += 80;

        return score;
    }
#endif
}

public enum UnitFaction
{
    None = -1,
    Marigold = 0,
    Lancel = 1
}
