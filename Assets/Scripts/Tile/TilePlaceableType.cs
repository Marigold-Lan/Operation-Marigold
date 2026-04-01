using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPlaceableType", menuName = OperationMarigoldPaths.SoTilePlaceableType)]
public class TilePlaceableType : ScriptableObject
{
    public string id;
    public string displayName;
    public GameObject prefab;
    [Tooltip("Prefab 在模型空间中的 XZ 边长。应与地基一致。")]
    public float prefabNativeSize = 1f;
    [Tooltip("Prefab 轴心位置。应与地基一致。")]
    public TilePivot pivot = TilePivot.Center;
    [Tooltip("允许放置的地基类型。为空表示可在任意地基上放置。")]
    public List<TileBaseType> allowedBases = new List<TileBaseType>();

    [Header("地形星数（掩体防御）")]
    [Tooltip("放置物存在时覆盖地基的星数。0=暴露(桥梁)，1=平原，2=森林，3=城市/工厂，4=总部。")]
    [Range(0, 4)]
    public int terrainStars = 0;

    [Header("建筑配置")]
    [Tooltip("可选。若设置，放置时会将此数据注入 BuildingController。")]
    public BuildingData buildingData;

    public bool CanPlaceOn(TileBaseType baseType)
    {
        if (baseType == null) return false;
        if (allowedBases == null || allowedBases.Count == 0) return true;
        return allowedBases.Contains(baseType);
    }
}
