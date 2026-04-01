using System;
using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using OperationMarigold.Logging.Adapters.Common;
using OperationMarigold.GameplayEvents;
using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    public sealed class CaptureLogAdapter : MonoBehaviour
    {
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();
        private readonly KeyedSubscriptionRegistry<BuildingController> _buildingRegistry = new KeyedSubscriptionRegistry<BuildingController>();

        private void OnEnable()
        {
            SubscribeAllExisting();
            _subscriptions.Add(
                () => CaptureCommand.OnCaptureCommandRejected += HandleCaptureCommandRejected,
                () => CaptureCommand.OnCaptureCommandRejected -= HandleCaptureCommandRejected);
            _subscriptions.Add(
                () => UnitCapture.OnCaptureStarted += HandleCaptureStarted,
                () => UnitCapture.OnCaptureStarted -= HandleCaptureStarted);
            _subscriptions.Add(
                () => UnitCapture.OnCaptureInterrupted += HandleCaptureInterrupted,
                () => UnitCapture.OnCaptureInterrupted -= HandleCaptureInterrupted);
            _subscriptions.Add(
                () => UnitCapture.OnCaptureApplied += HandleCaptureApplied,
                () => UnitCapture.OnCaptureApplied -= HandleCaptureApplied);
            // 兜底：每回合开始重扫一次，覆盖运行时新增建筑场景。
            _subscriptions.Add(
                () => TurnManager.OnTurnStarted += HandleTurnStarted,
                () => TurnManager.OnTurnStarted -= HandleTurnStarted);
        }

        private void OnDisable()
        {
            _subscriptions.DisposeAll();
            UnsubscribeAll();
        }

        private void SubscribeAllExisting()
        {
            var buildings = FindAll<BuildingController>();
            for (var i = 0; i < buildings.Length; i++)
                Register(buildings[i]);
        }

        private void Register(BuildingController building)
        {
            if (building == null)
                return;

            Action<UnitFaction, UnitFaction> captured = null;
            Action<UnitFaction, UnitFaction> ownerChanged = null;
            Action<UnitController, UnitFaction, int, int, int> progressChanged = null;
            Action<UnitController, CaptureResetReason> reset = null;
            Action<UnitController, UnitFaction, CaptureRejectReason> rejected = null;

            captured = (oldFaction, newFaction) =>
            {
                LogHub.Publish(
                    LogChannel.Capture,
                    $"Captured: {LogFormat.BuildingName(building)} {LogFormat.Faction(oldFaction)} -> {LogFormat.Faction(newFaction)}.");
            };

            ownerChanged = (oldFaction, newFaction) =>
            {
                LogHub.Publish(
                    LogChannel.Capture,
                    $"Owner changed: {LogFormat.BuildingName(building)} {LogFormat.Faction(oldFaction)} -> {LogFormat.Faction(newFaction)}.");
            };

            progressChanged = (capturer, attackerFaction, oldHp, newHp, damage) =>
            {
                if (capturer == null || building == null)
                    return;

                // 仅输出关键节点：开始一次占领（旧Hp==Max）或即将占领完成（newHp==0）。
                if (oldHp == building.MaxCaptureHp)
                {
                    LogHub.Publish(
                        LogChannel.Capture,
                        $"Capture started: {LogFormat.Faction(attackerFaction)} {LogFormat.UnitName(capturer)} -> {LogFormat.BuildingName(building)} ({oldHp}->{newHp}).");
                }
                else if (newHp <= 0)
                {
                    LogHub.Publish(
                        LogChannel.Capture,
                        $"Capture completes: {LogFormat.BuildingName(building)} remaining {oldHp}->{newHp} (damage {damage}).");
                }
            };

            reset = (previousCapturer, reason) =>
            {
                if (building == null)
                    return;
                var capturerName = previousCapturer != null ? LogFormat.UnitName(previousCapturer) : "UnknownUnit";
                LogHub.Publish(LogChannel.Capture, $"Capture reset: {LogFormat.BuildingName(building)} by {capturerName} ({reason}).");
            };

            rejected = (capturer, attackerFaction, reason) =>
            {
                // 拒绝通常是边缘情况；只输出一次即可
                var unitName = capturer != null ? LogFormat.UnitName(capturer) : "UnknownUnit";
                LogHub.Publish(LogChannel.Capture, $"Capture rejected: {LogFormat.BuildingName(building)} by {LogFormat.Faction(attackerFaction)} {unitName} ({reason}).");
            };

            _buildingRegistry.TryRegister(
                building,
                () =>
                {
                    building.OnCaptured += captured;
                    building.OnOwnerFactionChanged += ownerChanged;
                    building.OnCaptureProgressChanged += progressChanged;
                    building.OnCaptureProgressReset += reset;
                    building.OnCaptureAttemptRejected += rejected;
                },
                () =>
                {
                    building.OnCaptured -= captured;
                    building.OnOwnerFactionChanged -= ownerChanged;
                    building.OnCaptureProgressChanged -= progressChanged;
                    building.OnCaptureProgressReset -= reset;
                    building.OnCaptureAttemptRejected -= rejected;
                });
        }

        private void UnsubscribeAll()
        {
            _buildingRegistry.UnregisterAll();
        }

        private void HandleTurnStarted(TurnContext _)
        {
            SubscribeAllExisting();
        }

        private void HandleCaptureCommandRejected(UnitController unit, BuildingController building, CaptureRejectReason reason)
        {
            var unitName = unit != null ? LogFormat.UnitName(unit) : "UnknownUnit";
            var buildingName = building != null ? LogFormat.BuildingName(building) : "UnknownBuilding";
            LogHub.Publish(LogChannel.Capture, $"Capture command rejected: {unitName} -> {buildingName} ({reason}).");
        }

        private void HandleCaptureStarted(UnitController unit, BuildingController building, int capturePower)
        {
            if (unit == null || building == null)
                return;
            LogHub.Publish(
                LogChannel.Capture,
                $"Capture action: {LogFormat.Faction(unit.OwnerFaction)} {LogFormat.UnitName(unit)} starts capturing {LogFormat.BuildingName(building)} (power {capturePower}).");
        }

        private void HandleCaptureInterrupted(UnitController unit, BuildingController building, CaptureInterruptReason reason)
        {
            var unitName = unit != null ? LogFormat.UnitName(unit) : "UnknownUnit";
            var buildingName = building != null ? LogFormat.BuildingName(building) : "UnknownBuilding";
            LogHub.Publish(LogChannel.Capture, $"Capture interrupted: {unitName} -> {buildingName} ({reason}).");
        }

        private void HandleCaptureApplied(UnitController unit, BuildingController building, int capturePower, int oldHp, int newHp, bool captured)
        {
            if (unit == null || building == null)
                return;

            // 只输出关键节点：完成占领（captured==true）
            if (captured)
            {
                LogHub.Publish(
                    LogChannel.Capture,
                    $"Capture applied: {LogFormat.UnitName(unit)} -> {LogFormat.BuildingName(building)} ({oldHp}->{newHp}, power {capturePower}).");
            }
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

