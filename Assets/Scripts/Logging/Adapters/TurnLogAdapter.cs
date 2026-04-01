using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using OperationMarigold.Logging.Adapters.Common;
using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    public sealed class TurnLogAdapter : MonoBehaviour
    {
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        private void OnEnable()
        {
            _subscriptions.Add(
                () => TurnManager.OnDayChanged += HandleDayChanged,
                () => TurnManager.OnDayChanged -= HandleDayChanged);
            _subscriptions.Add(
                () => TurnManager.OnTurnStarted += HandleTurnStarted,
                () => TurnManager.OnTurnStarted -= HandleTurnStarted);
            _subscriptions.Add(
                () => TurnManager.OnTurnMainPhase += HandleTurnMainPhase,
                () => TurnManager.OnTurnMainPhase -= HandleTurnMainPhase);
            _subscriptions.Add(
                () => TurnManager.OnTurnEnded += HandleTurnEnded,
                () => TurnManager.OnTurnEnded -= HandleTurnEnded);
        }

        private void OnDisable()
        {
            _subscriptions.DisposeAll();
        }

        private void HandleDayChanged(int day)
        {
            LogHub.Publish(LogChannel.Turn, $"Day {day} begins.");
        }

        private void HandleTurnStarted(TurnContext context)
        {
            LogHub.Publish(LogChannel.Turn, $"Turn start: {LogFormat.Faction(context.Faction)} (Day {context.Day} / P{context.PlayerIndex + 1}).");
        }

        private void HandleTurnMainPhase(TurnContext context)
        {
            LogHub.Publish(LogChannel.Turn, $"Main phase: {LogFormat.Faction(context.Faction)}.");
        }

        private void HandleTurnEnded(TurnContext context)
        {
            LogHub.Publish(LogChannel.Turn, $"Turn end: {LogFormat.Faction(context.Faction)}.");
        }
    }
}

