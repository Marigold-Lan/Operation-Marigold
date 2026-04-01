using System;
using System.Collections.Generic;

namespace OperationMarigold.Logging.Adapters.Common
{
    /// <summary>
    /// 按 key 去重管理订阅关系，适合动态对象注册。
    /// </summary>
    internal sealed class KeyedSubscriptionRegistry<TKey> where TKey : class
    {
        private readonly Dictionary<TKey, Action> _unsubscribeByKey = new Dictionary<TKey, Action>();

        public bool IsRegistered(TKey key)
        {
            return key != null && _unsubscribeByKey.ContainsKey(key);
        }

        public bool TryRegister(TKey key, Action subscribe, Action unsubscribe)
        {
            if (key == null || subscribe == null || unsubscribe == null || _unsubscribeByKey.ContainsKey(key))
                return false;

            subscribe();
            _unsubscribeByKey.Add(key, unsubscribe);
            return true;
        }

        public void UnregisterAll()
        {
            foreach (var kv in _unsubscribeByKey)
            {
                kv.Value?.Invoke();
            }
            _unsubscribeByKey.Clear();
        }
    }
}
