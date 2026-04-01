using UnityEngine;

/// <summary>
/// 全局配置：为每种 MovementType 提供对应的图标。
/// 这样可以保证「移动类型」与「图标」是一对一强关联。
/// </summary>
[CreateAssetMenu(
    fileName = "MovementTypeIconConfig",
    menuName = OperationMarigoldPaths.SoMovementTypeIconConfig)]
public class MovementTypeIconConfig : ScriptableObject
{
    [Header("移动类型图标")]
    [SerializeField] private Sprite _footIcon;
    [SerializeField] private Sprite _mechIcon;
    [SerializeField] private Sprite _wheeledIcon;
    [SerializeField] private Sprite _treadsIcon;

    /// <summary>
    /// 根据移动类型获取对应图标。
    /// 如果未配置，返回 null。
    /// </summary>
    public Sprite GetIcon(MovementType movementType)
    {
        switch (movementType)
        {
            case MovementType.Foot:
                return _footIcon;
            case MovementType.Mech:
                return _mechIcon;
            case MovementType.Wheeled:
                return _wheeledIcon;
            case MovementType.Treads:
                return _treadsIcon;
            default:
                return null;
        }
    }
}

