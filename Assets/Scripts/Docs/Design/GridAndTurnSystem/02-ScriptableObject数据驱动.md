# ScriptableObject 数据驱动

## S（场景）

这个战棋游戏有大量配置数据：单位属性、建筑参数、地形类型、AI 难度等。如果这些硬编码在 C# 类里，调参得改代码 → 重新编译 → 重启引擎，策划无法独立工作。

## T（挑战）

如何让非程序侧（策划/设计师）能够独立调整游戏参数，而不需要每次都走代码修改流程？

## A（方案）

所有静态配置都定义为 ScriptableObject，通过 `[CreateAssetMenu]` 在 Unity 右键菜单创建实例。运行时通过 SerializeField 引用注入。

核心配置类：

- `UnitData`：单位 id/HP/Fuel/Ammo、移动类型、武器数据（含伤害矩阵）
- `BuildingData`：收入、占领 HP、是否为 HQ、阵营外观
- `TileBaseType` / `TilePlaceableType`：地形预制体、防御星级、放置约束
- `AIDifficultyProfile`：搜索深度、评估权重、随机扰动系数
- `FactoryBuildCatalogSO`：工厂生产单位目录（按阵营去重）

关键原则：**ScriptableObject 只存静态配置，不持有运行时状态**。`UnitData` 甚至内置了武器选择方法（`TrySelectWeaponForTarget`），确保运行时和 AI 使用完全一致的选枪逻辑。

## R（收益）

- 策划在 Editor 里直接改 .asset 文件，无需编译
- 配置按阵营/类型分离管理，可版本控制
- 字段迁移机制（`OnValidate`）保证了重命名后数据不丢失
- 跨配置引用（如 TilePlaceableType 内嵌 BuildingData）天然支持组合配置
