using System;
using System.Collections.Generic;

namespace OperationMarigold.Logging.Adapters.Common
{
    /// <summary>
    /// 管理成对订阅/反订阅动作，避免遗漏反注册。
    /// </summary>
    internal sealed class SubscriptionBag
    {
        private readonly List<Action> _unsubscribeActions = new List<Action>();

        public void Add(Action subscribe, Action unsubscribe)
        {
            if (subscribe == null || unsubscribe == null)
                return;

            subscribe();
            _unsubscribeActions.Add(unsubscribe);
        }

        public void DisposeAll()
        {
            for (var i = _unsubscribeActions.Count - 1; i >= 0; i--)
            {
                _unsubscribeActions[i]?.Invoke();
            }
            _unsubscribeActions.Clear();
        }
    }
}
