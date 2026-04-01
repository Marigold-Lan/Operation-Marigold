using System;
using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using OperationMarigold.Logging.Adapters.Common;
using OperationMarigold.GameplayEvents;
using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    public sealed class MovementLogAdapter : MonoBehaviour
    {
        private readonly KeyedSubscriptionRegistry<UnitMovement> _movementRegistry = new KeyedSubscriptionRegistry<UnitMovement>();
        private readonly KeyedSubscriptionRegistry<FactorySpawner> _spawnerRegistry = new KeyedSubscriptionRegistry<FactorySpawner>();

        private void OnEnable()
        {
            SubscribeExistingUnits();
            SubscribeSpawners();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void SubscribeExistingUnits()
        {
            var movements = FindAll<UnitMovement>();
            for (var i = 0; i < movements.Length; i++)
                RegisterMovement(movements[i]);
        }

        private void SubscribeSpawners()
        {
            var spawners = FindAll<FactorySpawner>();
            for (var i = 0; i < spawners.Length; i++)
                RegisterSpawner(spawners[i]);
        }

        private void RegisterSpawner(FactorySpawner spawner)
        {
            if (spawner == null)
                return;

            Action<UnitController> spawned = null;
            spawned = unit =>
            {
                if (unit == null)
                    return;
                var movement = unit.Movement != null ? unit.Movement : unit.GetComponent<UnitMovement>();
                RegisterMovement(movement);
            };

            _spawnerRegistry.TryRegister(
                spawner,
                () => spawner.OnUnitSpawned += spawned,
                () => spawner.OnUnitSpawned -= spawned);
        }

        private void RegisterMovement(UnitMovement movement)
        {
            if (movement == null)
                return;

            Action<UnitMovement> started = null;
            Action<UnitMovement> ended = null;
            Action<UnitController, Vector2Int, MoveFailReason> failed = null;
            Action<UnitController, Vector2Int, Vector2Int, MoveStopReason, int, int> stopped = null;

            started = _ =>
            {
                var unit = movement != null ? movement.GetComponent<UnitController>() : null;
                if (unit == null)
                    return;
                LogHub.Publish(
                    LogChannel.Movement,
                    $"{LogFormat.Faction(unit.OwnerFaction)} {LogFormat.UnitName(unit)} started moving at {LogFormat.Coord(unit.GridCoord)}.");
            };

            ended = _ =>
            {
                var unit = movement != null ? movement.GetComponent<UnitController>() : null;
                if (unit == null)
                    return;
                LogHub.Publish(
                    LogChannel.Movement,
                    $"{LogFormat.Faction(unit.OwnerFaction)} {LogFormat.UnitName(unit)} finished moving at {LogFormat.Coord(unit.GridCoord)} (fuel {unit.CurrentFuel}).");
            };

            failed = (unit, target, reason) =>
            {
                if (unit == null)
                    return;
                LogHub.Publish(
                    LogChannel.Movement,
                    $"Move failed: {LogFormat.Faction(unit.OwnerFaction)} {LogFormat.UnitName(unit)} -> {LogFormat.Coord(target)} ({reason}).");
            };

            stopped = (unit, atCoord, nextCoord, reason, consumedFuel, remainingFuel) =>
            {
                if (unit == null)
                    return;
                LogHub.Publish(
                    LogChannel.Movement,
                    $"Move stopped: {LogFormat.Faction(unit.OwnerFaction)} {LogFormat.UnitName(unit)} at {LogFormat.Coord(atCoord)} -> {LogFormat.Coord(nextCoord)} ({reason}, fuel used {consumedFuel}, remaining {remainingFuel}).");
            };

            _movementRegistry.TryRegister(
                movement,
                () =>
                {
                    movement.OnMoveStarted += started;
                    movement.OnMoveEnded += ended;
                    movement.OnMoveFailed += failed;
                    movement.OnPathTraversalStopped += stopped;
                },
                () =>
                {
                    movement.OnMoveStarted -= started;
                    movement.OnMoveEnded -= ended;
                    movement.OnMoveFailed -= failed;
                    movement.OnPathTraversalStopped -= stopped;
                });
        }

        private void UnsubscribeAll()
        {
            _movementRegistry.UnregisterAll();
            _spawnerRegistry.UnregisterAll();
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return FindObjectsOfType<T>();
#endif
        }
    }
}

