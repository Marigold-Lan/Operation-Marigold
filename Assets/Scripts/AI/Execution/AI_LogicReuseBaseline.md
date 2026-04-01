# AI/Player Logic Reuse Baseline

## Verified execution paths (before refactor)

- Player attack: `PlayerTurnController.HandleAttackConfirm` -> `AttackCommand.Execute` -> `UnitCombat.TryAttack`.
- AI attack: `AIActionExecutor.ExecuteAttack` -> `UnitCombat.TryAttack`.
- Player move: `PlayerTurnController` -> `UnitActionValidator.TryGetMovePath` -> `UnitMovement.MoveAlongPath`.
- AI move: `AIActionExecutor.ExecuteMove` -> `PathfindingManager.FindPath` -> `UnitMovement.MoveAlongPath`.
- Capture/Supply/Load/Drop: both sides call shared runtime components (`ICapturable`, `ISupplier`, `ITransporter`).

## Unified execution paths (current)

- Player/AI unit actions execute through `CommandExecutor` (`CanExecute` -> `Execute`).
- Player targeting confirms for `Supply/Load/Drop` now route back to `CommandExecutor` with immediate command context.
- Produce now uses `ProduceCommand` on both sides:
  - Player factory UI confirm -> `CommandExecutor.Execute(new ProduceCommand(), context)`.
  - AI produce action -> `CommandExecutor.Execute(new ProduceCommand(), context)`.
- Supply semantics are unified as area supply (cardinal neighbors) for both runtime and AI simulation.

## Known divergence points

1. AI still uses `AIPlannedAction` queueing, but execution funnels into shared commands.
2. Minimax simulation remains snapshot-based, but combat/movement/supply/capture rules are aligned with shared rule helpers.

## Baseline damage formula sample

- Runtime raw: `DamageResolver.ResolveDamage(baseDamage, percent, terrainBonus, out finalPercent)`.
- Runtime HP scaling: `ceil(rawDamage * currentHp / maxHp)`, min 1 when raw > 0.
- AI minimax currently uses same shape but via local duplicated function.

## Refactor safety checkpoints

- Keep attack/counter weapon selection unchanged.
- Keep `HasActed` and `HasMovedThisTurn` state transitions unchanged.
- Keep attack sequence completion event timing unchanged.
- Keep invalid action behavior unchanged (fail-fast, no side effects).
