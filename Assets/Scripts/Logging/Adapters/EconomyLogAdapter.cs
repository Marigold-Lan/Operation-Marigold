using OperationMarigold.Logging.Domain;
using OperationMarigold.Logging.Runtime;
using OperationMarigold.Logging.Adapters.Common;
using UnityEngine;

namespace OperationMarigold.Logging.Adapters
{
    public sealed class EconomyLogAdapter : MonoBehaviour
    {
        private readonly SubscriptionBag _subscriptions = new SubscriptionBag();

        private void OnEnable()
        {
            _subscriptions.Add(
                () => FactionFundsLedger.OnFundsChanged += HandleFundsChanged,
                () => FactionFundsLedger.OnFundsChanged -= HandleFundsChanged);
        }

        private void OnDisable()
        {
            _subscriptions.DisposeAll();
        }

        private void HandleFundsChanged(UnitFaction faction, int oldValue, int newValue, int delta)
        {
            if (faction == UnitFaction.None)
                return;

            var sign = delta >= 0 ? "+" : "";
            LogHub.Publish(LogChannel.Economy, $"Funds changed: {LogFormat.Faction(faction)} {sign}{delta} ({oldValue} -> {newValue}).");
        }
    }
}

