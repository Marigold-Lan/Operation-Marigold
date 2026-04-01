using System.Collections.Generic;

namespace OperationMarigold.BehaviorTreeFramework
{
    /// <summary>
    /// 行为树黑板：类型安全的共享数据存储
    /// 按值类型分离字典存储，避免装箱拆箱产生的 GC
    /// </summary>
    public class Blackboard
    {
        private Dictionary<int, int> int_data = new Dictionary<int, int>();
        private Dictionary<int, float> float_data = new Dictionary<int, float>();
        private Dictionary<int, bool> bool_data = new Dictionary<int, bool>();
        private Dictionary<int, object> ref_data = new Dictionary<int, object>();

        /// <summary>
        /// 将字符串键转换为 int 哈希，业务层应缓存返回值为 static readonly 常量
        /// </summary>
        public static int Key(string name)
        {
            return name.GetHashCode();
        }

        // ─── int ────────────────────────────────────────────

        /// <summary>
        /// 设置 int 值
        /// </summary>
        public void SetInt(int key, int value)
        {
            int_data[key] = value;
        }

        /// <summary>
        /// 获取 int 值，键不存在时返回默认值
        /// </summary>
        public int GetInt(int key, int default_value = 0)
        {
            return int_data.TryGetValue(key, out int v) ? v : default_value;
        }

        /// <summary>
        /// 检查是否存在指定 int 键
        /// </summary>
        public bool HasInt(int key)
        {
            return int_data.ContainsKey(key);
        }

        // ─── float ──────────────────────────────────────────

        /// <summary>
        /// 设置 float 值
        /// </summary>
        public void SetFloat(int key, float value)
        {
            float_data[key] = value;
        }

        /// <summary>
        /// 获取 float 值，键不存在时返回默认值
        /// </summary>
        public float GetFloat(int key, float default_value = 0f)
        {
            return float_data.TryGetValue(key, out float v) ? v : default_value;
        }

        /// <summary>
        /// 检查是否存在指定 float 键
        /// </summary>
        public bool HasFloat(int key)
        {
            return float_data.ContainsKey(key);
        }

        // ─── bool ───────────────────────────────────────────

        /// <summary>
        /// 设置 bool 值
        /// </summary>
        public void SetBool(int key, bool value)
        {
            bool_data[key] = value;
        }

        /// <summary>
        /// 获取 bool 值，键不存在时返回默认值
        /// </summary>
        public bool GetBool(int key, bool default_value = false)
        {
            return bool_data.TryGetValue(key, out bool v) ? v : default_value;
        }

        /// <summary>
        /// 检查是否存在指定 bool 键
        /// </summary>
        public bool HasBool(int key)
        {
            return bool_data.ContainsKey(key);
        }

        // ─── 引用类型 ──────────────────────────────────────

        /// <summary>
        /// 设置引用类型值（class 不会装箱）
        /// </summary>
        public void SetRef<T>(int key, T value) where T : class
        {
            ref_data[key] = value;
        }

        /// <summary>
        /// 获取引用类型值，键不存在时返回 null
        /// </summary>
        public T GetRef<T>(int key) where T : class
        {
            return ref_data.TryGetValue(key, out object v) ? v as T : null;
        }

        /// <summary>
        /// 检查是否存在指定引用类型键
        /// </summary>
        public bool HasRef(int key)
        {
            return ref_data.ContainsKey(key);
        }

        // ─── 通用操作 ──────────────────────────────────────

        /// <summary>
        /// 移除指定键（所有类型字典中查找）
        /// </summary>
        public void Remove(int key)
        {
            int_data.Remove(key);
            float_data.Remove(key);
            bool_data.Remove(key);
            ref_data.Remove(key);
        }

        /// <summary>
        /// 清空黑板中的所有数据
        /// </summary>
        public void Clear()
        {
            int_data.Clear();
            float_data.Clear();
            bool_data.Clear();
            ref_data.Clear();
        }
    }
}
