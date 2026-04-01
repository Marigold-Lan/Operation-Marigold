using UnityEngine;

[CreateAssetMenu(fileName = "NewBaseType", menuName = OperationMarigoldPaths.SoTileBaseType)]
public class TileBaseType : ScriptableObject
{
    public string id;
    public string displayName;
    public GameObject prefab;

    [Tooltip("格子边长（XZ 平面），应与 base 的 2D 尺寸一致。base 与 placeable 的 2D 形状相同，仅 Y 轴不同。")]
    public float cellSize = 1f;

    [Tooltip("Prefab 在模型空间中的 XZ 边长（未缩放时）。用于实例化时正确缩放填满格子。")]
    public float prefabNativeSize = 1f;

    [Tooltip("Prefab 轴心位置：Center=中心（默认）；BottomLeft=左下角（常见于 FBX）")]
    public TilePivot pivot = TilePivot.Center;

    [Tooltip("Y 轴偏移，用于校正地基与格子的高度对齐（负值向下）。")]
    public float positionYOffset = 0f;

    [Header("地形星数（掩体防御）")]
    [Tooltip("地形星数 0–4，决定驻扎单位的减伤比例。0=暴露(道路/桥梁)，1=平原10%，2=森林20%，3=山脉/城市30%，4=总部40%。")]
    [Range(0, 4)]
    public int terrainStars = 1;
}

public enum TilePivot
{
    Center,
    BottomLeft
}
