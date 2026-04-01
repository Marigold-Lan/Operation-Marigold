using System.Collections.Generic;

namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 对象池类，用于重用频繁创建和销毁的对象，避免频繁分配内存
    /// 可以显著提升AI等需要大量临时对象操作的性能
    /// </summary>
    public class Pool<T> where T : new()
    {
        // 正在使用中的对象集合
        private HashSet<T> in_use = new HashSet<T>();

        // 可用对象栈
        private Stack<T> available = new Stack<T>();

        /// <summary>
        /// 从池中创建或获取一个对象
        /// </summary>
        public T Create()
        {
            if (available.Count > 0)
            {
                T elem = available.Pop(); // 从可用栈中取出对象
                in_use.Add(elem);         // 标记为正在使用
                return elem;
            }
            T new_obj = new T();          // 没有可用对象则新建
            in_use.Add(new_obj);          // 标记为正在使用
            return new_obj;
        }

        /// <summary>
        /// 将对象归还到池中
        /// </summary>
        public void Dispose(T elem)
        {
            in_use.Remove(elem);          // 从使用中移除
            available.Push(elem);         // 放入可用栈
        }

        /// <summary>
        /// 将所有正在使用的对象全部归还池中
        /// </summary>
        public void DisposeAll()
        {
            foreach (T obj in in_use)
                available.Push(obj);      // 逐个归还
            in_use.Clear();               // 清空使用集合
        }

        /// <summary>
        /// 清空池中所有对象，包括正在使用和可用的
        /// </summary>
        public void Clear()
        {
            in_use.Clear();               // 清空正在使用集合
            available.Clear();            // 清空可用集合
        }

        /// <summary>
        /// 获取所有当前正在使用的对象
        /// </summary>
        public HashSet<T> GetAllActive()
        {
            return in_use;
        }

        /// <summary>
        /// 当前正在使用的对象数量
        /// </summary>
        public int Count
        {
            get { return in_use.Count; }
        }

        /// <summary>
        /// 当前可用对象数量
        /// </summary>
        public int CountAvailable
        {
            get { return available.Count; }
        }

        /// <summary>
        /// 当前池的总容量（正在使用 + 可用对象数量）
        /// </summary>
        public int CountCapacity
        {
            get { return in_use.Count + available.Count; }
        }
    }
}
