# A* + BFS 寻路

## S（场景）

战棋游戏需要两类寻路能力：UI 显示高亮时需要快速列出"这个单位能走到哪些格子"；玩家点目标时需要计算"从当前位置到目标的最优路径"。

## T（挑战）

如何同时满足可达域查询（需要快，不需要最优）和最优路径查询（需要最优，不需要快）两种需求？

## A（方案）

两种算法各司其职，分工明确：

**BFS — 可达域（GetReachableCells）**：从起点扩散，记录每个格子剩余最多的移动力。关键优化是 `bestRemaining` 字典——同一格子被多条路径到达时，只保留剩余最多的那条，避免重复入队。

**A\* — 最优路径（FindPath）**：先调用 BFS 确认目标可达，再搜索。`f(n) = g(n) + h(n)`，h 用 Manhattan 距离（对四连通网格是 admissable 的）。移动力上限裁剪（`tentativeG > range`）显著减少展开节点数。

两侧共用消耗表：

```csharp
public static int GetCost(MovementTerrainKind terrain, MovementType movementType)
// Road/Bridge/Building: 1（全类型）
// Plains: Foot=1, Mech=1, Treads=1, Wheeled=2
// Woods: Foot=1, Mech=1, Treads=2, Wheeled=3
// Mountain/River: Foot=2, Mech=1, Treads/Wheeled=Impassable
// Sea: Impassable
```

AI 侧等价实现：`AIMovementCostProvider` 委托给同一个 `MovementCostProvider`，规则完全一致。

## R（收益）

- UI 高亮用 BFS，毫秒级响应，不需要路径结果
- 实际移动用 A\*，路径最优，且有 BFS 前置验证避免了无用搜索
- 共享消耗表：运行时和 AI 用同一份规则，不会出现"AI 觉得能走但实际走不了"的情况
- 移动力裁剪：A\* 中 `tentativeG > range` 提前剪枝，大幅减少搜索空间
