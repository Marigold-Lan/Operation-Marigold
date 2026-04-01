using UnityEngine;

namespace OperationMarigold.AI.Core
{
    /// <summary>
    /// AI 调试日志门面：默认关闭高频日志，避免 Editor 首帧日志尖峰。
    /// </summary>
    public static class AITrace
    {
        public static bool Verbose { get; set; }

        public static void LogVerbose(string message)
        {
            if (Verbose)
                Debug.Log(message);
        }
    }
}
