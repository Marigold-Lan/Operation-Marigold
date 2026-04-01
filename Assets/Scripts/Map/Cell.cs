using System;
using UnityEngine;

[ExecuteAlways]
public class Cell : MonoBehaviour, ICellReadView
{
    public event Action<Cell, GameObject> OnUnitEntered;
    public event Action<Cell, GameObject> OnUnitLeft;
    public event Action<Cell, GameObject, float> OnUnitWillEnter;
    public event Action<Cell, GameObject, float> OnUnitWillLeave;

    [SerializeField] private TileBaseType baseType;
    [SerializeField] private TilePlaceableType placeableType;
    [SerializeField] private GameObject unit;

    private Transform _baseInstance;
    private Transform _placeableInstance;

    public TileBaseType BaseType => baseType;
    public TilePlaceableType PlaceableType => placeableType;
    public GameObject Unit => unit;
    public bool HasUnit => unit != null;
    public bool HasBuilding => Building != null;
    public int TerrainStars => GetTerrainStars();

    /// <summary>
    /// 格子上单位的 UnitController。若无单位或单位无 UnitController 则返回 null。
    /// </summary>
    public UnitController UnitController => unit != null ? unit.GetComponent<UnitController>() : null;

    [Tooltip("网格坐标，供寻路等逻辑使用。")]
    public Vector2Int gridCoord;

    [Tooltip("移动消耗，供寻路等逻辑使用。")]
    [SerializeField] private int movementCost = 1;

    [Tooltip("地基绕格子中心水平旋转（0/90/180/270 度）。")]
    [SerializeField] private int baseRotationDegrees;

    [Tooltip("放置物绕格子中心水平旋转（0/90/180/270 度），用于拐角等复用。")]
    [SerializeField] private int placeableRotationDegrees;

    public int MovementCost => movementCost;
    public int BaseRotationDegrees => baseRotationDegrees;
    public int PlaceableRotationDegrees => placeableRotationDegrees;

    private void Awake()
    {
        SyncRefsOrRebuildIfNeeded();
        if (Application.isPlaying)
        {
            var root = GetComponentInParent<MapRoot>();
            if (root != null)
                root.RegisterCell(this);
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && MapRoot.Instance != null)
            MapRoot.Instance.UnregisterCell(this);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            SyncRefsOrRebuildIfNeeded();
    }

    /// <summary>
    /// 加载时仅同步 _baseInstance/_placeableInstance 引用，不修改已有实例的 transform。
    /// 仅当无匹配子物体时才创建新实例。避免覆盖场景中已保存的旋转。
    /// </summary>
    private void SyncRefsOrRebuildIfNeeded()
    {
        if (baseType == null && placeableType == null) return;

        TryReuseOrCleanTileInstances();

        var needsBase = baseType != null && _baseInstance == null;
        var needsPlaceable = placeableType != null && _placeableInstance == null;

        if (needsBase || needsPlaceable)
            RebuildVisuals(preserveExistingRotation: true);
    }

    public void SetBase(TileBaseType type, bool preserveExistingRotation = false)
    {
        if (type == null) return;

        baseType = type;
        if (type.prefab == null) return;

        if (_baseInstance != null && InstanceNameMatches(_baseInstance.name, type.prefab.name))
        {
            FitInstanceToCell(_baseInstance, type.cellSize, type.prefabNativeSize, type.pivot, type.positionYOffset, baseRotationDegrees, skipRotationUpdate: preserveExistingRotation, prefab: type.prefab);
            return;
        }

        DestroyInstance(ref _baseInstance);
        var go = Instantiate(type.prefab, transform);
        go.name = type.prefab.name;
        _baseInstance = go.transform;
        FitInstanceToCell(_baseInstance, type.cellSize, type.prefabNativeSize, type.pivot, type.positionYOffset, baseRotationDegrees, prefab: type.prefab);
    }

    public void SetPlaceable(TilePlaceableType type, bool preserveExistingRotation = false)
    {
        if (type != null && !CanAcceptPlaceable(type)) return;

        placeableType = type;
        if (type != null && type.prefab != null)
        {
            if (_placeableInstance != null && InstanceNameMatches(_placeableInstance.name, type.prefab.name))
            {
                var cs = baseType != null ? baseType.cellSize : 1f;
                var pns = baseType != null ? baseType.prefabNativeSize : 1f;
                FitInstanceToCell(_placeableInstance, cs, pns, type.pivot, 0f, placeableRotationDegrees, skipRotationUpdate: preserveExistingRotation, prefab: type.prefab);
                if (type.buildingData != null)
                {
                    var building = _placeableInstance.GetComponent<BuildingController>();
                    if (building != null)
                        building.InjectDataFromPlaceableType(type.buildingData);
                }
                return;
            }

            DestroyInstance(ref _placeableInstance);
            var go = Instantiate(type.prefab, transform);
            go.name = type.prefab.name;
            _placeableInstance = go.transform;
            var cellSize = baseType != null ? baseType.cellSize : 1f;
            var prefabNativeSize = baseType != null ? baseType.prefabNativeSize : 1f;
            FitInstanceToCell(_placeableInstance, cellSize, prefabNativeSize, type.pivot, 0f, placeableRotationDegrees, prefab: type.prefab);

            if (type.buildingData != null)
            {
                var building = _placeableInstance.GetComponent<BuildingController>();
                if (building != null)
                    building.InjectDataFromPlaceableType(type.buildingData);
            }
        }
        else
        {
            DestroyInstance(ref _placeableInstance);
        }
    }

    public void ClearPlaceable()
    {
        SetPlaceable(null);
    }

    public void SetUnit(GameObject u)
    {
        if (unit == u) return;

        if (unit != null)
            OnUnitLeft?.Invoke(this, unit);

        unit = u;

        if (unit != null)
            OnUnitEntered?.Invoke(this, unit);
    }

    public void ClearUnit()
    {
        if (unit == null) return;

        var oldUnit = unit;
        unit = null;
        OnUnitLeft?.Invoke(this, oldUnit);
    }

    public void NotifyUnitWillEnter(GameObject incomingUnit, float duration)
    {
        if (incomingUnit == null) return;
        OnUnitWillEnter?.Invoke(this, incomingUnit, Mathf.Max(0f, duration));
    }

    public void NotifyUnitWillLeave(GameObject leavingUnit, float duration)
    {
        if (leavingUnit == null) return;
        OnUnitWillLeave?.Invoke(this, leavingUnit, Mathf.Max(0f, duration));
    }

    public void SetBaseRotation(int degrees)
    {
        baseRotationDegrees = NormalizeRotation(degrees);
        RebuildVisuals(preserveExistingRotation: false);
    }

    public void SetPlaceableRotation(int degrees)
    {
        placeableRotationDegrees = NormalizeRotation(degrees);
        RebuildVisuals(preserveExistingRotation: false);
    }

    public void SetRotation(int degrees)
    {
        var n = NormalizeRotation(degrees);
        baseRotationDegrees = n;
        placeableRotationDegrees = n;
        RebuildVisuals(preserveExistingRotation: false);
    }

    private static int NormalizeRotation(int degrees)
    {
        var n = ((degrees % 360) + 360) % 360;
        return n % 90 != 0 ? 0 : n;
    }

    public bool CanAcceptPlaceable(TilePlaceableType type)
    {
        if (type == null) return false;
        if (baseType == null) return false;
        return type.CanPlaceOn(baseType);
    }

    /// <summary>
    /// 若本格放置物为建筑，返回其 BuildingController；否则返回 null。
    /// </summary>
    public BuildingController Building => _placeableInstance != null ? _placeableInstance.GetComponent<BuildingController>() : null;

    /// <summary>
    /// 获取该格子的地形星数（掩体防御等级）。
    /// 若格子包含桥梁（Bridge/RiverBridge），优先采用桥梁星数；否则放置物覆盖地基。
    /// </summary>
    public int GetTerrainStars()
    {
        if (IsBridgePlaceable(placeableType))
            return placeableType.terrainStars;
        if (IsBridgeBase(baseType))
            return baseType.terrainStars;

        if (placeableType != null)
            return placeableType.terrainStars;
        return baseType != null ? baseType.terrainStars : 0;
    }

    private static bool IsBridgeId(string id)
    {
        return id == "Bridge" || id == "RiverBridge";
    }

    private static bool IsBridgePlaceable(TilePlaceableType type)
    {
        return type != null && IsBridgeId(type.id);
    }

    private static bool IsBridgeBase(TileBaseType type)
    {
        return type != null && IsBridgeId(type.id);
    }

    public void RebuildVisuals(bool preserveExistingRotation = false)
    {
        TryReuseOrCleanTileInstances();
        if (baseType != null) SetBase(baseType, preserveExistingRotation);
        if (placeableType != null) SetPlaceable(placeableType, preserveExistingRotation);
    }

    private static bool InstanceNameMatches(string instanceName, string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return false;
        if (instanceName == prefabName) return true;
        var stripped = instanceName?.Replace("(Clone)", "").Trim();
        return stripped == prefabName;
    }

    /// <summary>
    /// 尝试复用 prefab 已有子物体，仅清理重复项。避免误删原有正确实例、保留错误重复实例。
    /// </summary>
    private void TryReuseOrCleanTileInstances()
    {
        var basePrefabName = baseType?.prefab != null ? baseType.prefab.name : null;
        var placeablePrefabName = placeableType?.prefab != null ? placeableType.prefab.name : null;

        Transform foundBase = null;
        Transform foundPlaceable = null;

        for (var i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            var name = child.name;
            if (basePrefabName != null && InstanceNameMatches(name, basePrefabName) && foundBase == null)
                foundBase = child;
            else if (placeablePrefabName != null && InstanceNameMatches(name, placeablePrefabName) && foundPlaceable == null)
                foundPlaceable = child;
        }

        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            var name = child.name;
            var keep = (InstanceNameMatches(name, basePrefabName) && child == foundBase) || (InstanceNameMatches(name, placeablePrefabName) && child == foundPlaceable);
            if (!keep)
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        _baseInstance = foundBase;
        _placeableInstance = foundPlaceable;
    }

    /// <param name="skipRotationUpdate">为 true 时仅更新位置和缩放，保持实例原有旋转。</param>
    /// <param name="prefab">若提供，在 prefab 默认旋转基础上叠加 cell 的 Y 旋转，确保与 prefab 一致。</param>
    private static void FitInstanceToCell(Transform instance, float cellSize, float prefabNativeSize, TilePivot pivot, float positionYOffset = 0f, int rotationDegrees = 0, bool skipRotationUpdate = false, GameObject prefab = null)
    {
        if (cellSize <= 0 || prefabNativeSize <= 0) return;

        var scale = cellSize / prefabNativeSize;
        instance.localScale = new Vector3(scale, scale, scale);

        if (!skipRotationUpdate)
        {
            var worldYRot = Quaternion.Euler(0f, rotationDegrees, 0f);
            var prefabRot = prefab != null ? prefab.transform.localRotation : Quaternion.identity;
            instance.localRotation = worldYRot * prefabRot;
        }

        float px, pz;
        if (pivot == TilePivot.BottomLeft)
        {
            px = cellSize / 2f;
            pz = cellSize / 2f;
        }
        else
        {
            px = 0f;
            pz = 0f;
        }

        instance.localPosition = new Vector3(px, positionYOffset, pz);
    }

    private void DestroyInstance(ref Transform instance)
    {
        if (instance == null) return;

        if (Application.isPlaying)
            Destroy(instance.gameObject);
        else
            DestroyImmediate(instance.gameObject);

        instance = null;
    }

}
