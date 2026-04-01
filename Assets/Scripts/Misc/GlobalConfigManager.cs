using UnityEngine;

/// <summary>
/// 全局配置管理器。
/// 目前只管理 MovementTypeIconConfig，后续可以逐步扩展为统一的配置入口。
/// </summary>
public class GlobalConfigManager : Singleton<GlobalConfigManager>
{
    [Header("移动类型图标配置")]
    [SerializeField] private MovementTypeIconConfig _movementTypeIconConfig;

    /// <summary>
    /// 全局访问入口：移动类型图标配置。
    /// </summary>
    public static MovementTypeIconConfig MovementTypeIconConfig =>
        Instance != null ? Instance._movementTypeIconConfig : null;

    /// <summary>
    /// 根据移动类型获取对应图标。封装一层方便调用。
    /// </summary>
    public static Sprite GetMovementIcon(MovementType movementType)
    {
        var config = MovementTypeIconConfig;
        return config != null ? config.GetIcon(movementType) : null;
    }
}

