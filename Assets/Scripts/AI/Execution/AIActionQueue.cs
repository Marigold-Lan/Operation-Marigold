using System.Collections.Generic;

namespace OperationMarigold.AI.Execution
{
    /// <summary>
    /// AI 动作先进先出队列。BT 节点向其中添加计划动作，AIActionExecutor 逐个执行。
    /// </summary>
    public class AIActionQueue
    {
        private readonly Queue<AIPlannedAction> _queue = new Queue<AIPlannedAction>();

        public int Count => _queue.Count;

        public void Enqueue(AIPlannedAction action) => _queue.Enqueue(action);
        public AIPlannedAction Dequeue() => _queue.Dequeue();
        public AIPlannedAction Peek() => _queue.Peek();
        public void Clear() => _queue.Clear();
        public bool HasActions => _queue.Count > 0;
    }
}
