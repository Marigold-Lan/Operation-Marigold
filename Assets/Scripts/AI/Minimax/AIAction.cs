using UnityEngine;
using OperationMarigold.MinimaxFramework;
using System.Collections.Generic;

namespace OperationMarigold.AI.Minimax
{
    public enum AIActionType : ushort
    {
        None = 0,
        EndTurn = 1,
        Move = 2,
        Attack = 3,
        Capture = 4,
        Wait = 5,
        Load = 6,
        Drop = 7,
        Supply = 8
    }

    /// <summary>
    /// Minimax 搜索用动作，实现 IAction。支持对象池复用。
    /// </summary>
    public class AIAction : IAction
    {
        private static readonly Stack<AIAction> Pool = new Stack<AIAction>(256);
        private static readonly object PoolLock = new object();

        public AIActionType actionType;
        public int unitIndex;
        public Vector2Int targetCoord;
        public int targetUnitIndex;

        /// <summary>0 = primary, 1 = secondary。</summary>
        public int weaponSlot;

        private int _score;
        private int _sort;
        private bool _valid;
        private bool _inPool;

        // ─── IAction ───────────────────────────────────────

        public ushort Type => (ushort)actionType;

        public int Score
        {
            get => _score;
            set => _score = value;
        }

        public int Sort
        {
            get => _sort;
            set => _sort = value;
        }

        public bool Valid
        {
            get => _valid;
            set => _valid = value;
        }

        public static AIAction Rent()
        {
            lock (PoolLock)
            {
                if (Pool.Count > 0)
                {
                    var action = Pool.Pop();
                    action._inPool = false;
                    action.ResetState();
                    return action;
                }
            }

            var created = new AIAction();
            created._inPool = false;
            created.ResetState();
            return created;
        }

        public static void Return(AIAction action)
        {
            if (action == null || action._inPool)
                return;

            action.ResetState();
            action._inPool = true;
            lock (PoolLock)
            {
                Pool.Push(action);
            }
        }

        public static void ReturnMany(IList<IAction> actions)
        {
            if (actions == null)
                return;

            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] is AIAction aiAction)
                    Return(aiAction);
            }
        }

        public void Clear()
        {
            ResetState();
        }

        private void ResetState()
        {
            actionType = AIActionType.None;
            unitIndex = -1;
            targetCoord = Vector2Int.zero;
            targetUnitIndex = -1;
            weaponSlot = 0;
            _score = 0;
            _sort = 0;
            _valid = true;
        }
    }
}
