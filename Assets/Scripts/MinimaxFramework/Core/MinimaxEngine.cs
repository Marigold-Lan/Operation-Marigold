using System.Collections.Generic;
using System.Threading;
using OperationMarigold.AI.Core;
using UnityEngine;

namespace OperationMarigold.MinimaxFramework
{
    /// <summary>
    /// 通用 Minimax + Alpha-Beta 剪枝引擎
    /// 独立于具体游戏，通过接口与游戏逻辑解耦
    /// </summary>
    public class MinimaxEngine
    {
        private void TimingLog(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        private SearchConfig config;
        private int ai_player_id;
        private IGameLogic game_logic;
        private IHeuristic heuristic;
        private IActionGenerator action_generator;
        private Thread ai_thread;

        private IGameState original_state;
        private SearchNode first_node;
        private SearchNode best_move;

        private bool running;
        private int nb_calculated;
        private int reached_depth;

        private Pool<SearchNode> node_pool = new Pool<SearchNode>();
        private Pool<List<IAction>> list_pool = new Pool<List<IAction>>();
        private Pool<OperationMarigold.AI.Simulation.AIBoardState> board_pool = new Pool<OperationMarigold.AI.Simulation.AIBoardState>();
        private System.Diagnostics.Stopwatch _searchWatch;

        /// <summary>
        /// 创建 Minimax 引擎
        /// </summary>
        public static MinimaxEngine Create(
            int playerId,
            IGameLogic logic,
            IHeuristic heuristic,
            IActionGenerator actionGenerator,
            SearchConfig config = null)
        {
            var engine = new MinimaxEngine();
            engine.ai_player_id = playerId;
            engine.game_logic = logic;
            engine.heuristic = heuristic;
            engine.action_generator = actionGenerator;
            engine.config = config ?? new SearchConfig();
            return engine;
        }

        /// <summary>
        /// 启动 AI 计算（异步线程）
        /// </summary>
        public void RunAI(IGameState state)
        {
            if (running)
                return;

            var mainTid = Thread.CurrentThread.ManagedThreadId;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            original_state = CloneState(state);
            TimingLog($"[MinimaxEngine] RunAI clone done elapsedMs={sw.ElapsedMilliseconds} mainTid={mainTid}");

            game_logic.ClearResolve();
            game_logic.SetState(original_state);

            first_node = null;
            reached_depth = 0;
            nb_calculated = 0;
            running = true;

            ai_thread = new Thread(Execute);
            ai_thread.Start();

            TimingLog($"[MinimaxEngine] RunAI thread started elapsedMs={sw.ElapsedMilliseconds} mainTid={mainTid}");
        }

        /// <summary>
        /// 停止 AI
        /// </summary>
        public void Stop()
        {
            running = false;
            if (ai_thread != null && ai_thread.IsAlive)
            {
                ai_thread.Abort();
                ai_thread.Join(50);
            }
        }

        private void Execute()
        {
            try
            {
                var aiTid = Thread.CurrentThread.ManagedThreadId;
                TimingLog($"[MinimaxEngine] Execute begin aiTid={aiTid}");

                first_node = CreateNode(null, null, ai_player_id, 0, 0);
                first_node.hvalue = heuristic.CalculateStateScore(original_state, first_node);
                first_node.alpha = int.MinValue;
                first_node.beta = int.MaxValue;

                _searchWatch = System.Diagnostics.Stopwatch.StartNew();
                CalculateNode(original_state, first_node);
                _searchWatch.Stop();

                best_move = first_node.best_child;

                TimingLog(
                    $"[MinimaxEngine] Execute end aiTid={aiTid} " +
                    $"elapsedMs={_searchWatch.ElapsedMilliseconds} nb={nb_calculated} depth={reached_depth} " +
                    $"hasBest={best_move != null}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MinimaxEngine] Search thread crashed: " + ex);
                best_move = null;
            }
            finally
            {
                running = false;
            }
        }

        private void CalculateNode(IGameState state, SearchNode node)
        {
            if (_searchWatch != null && _searchWatch.Elapsed.TotalSeconds >= config.search_timeout_seconds)
                return;

            List<IAction> action_list = list_pool.Create();
            action_list.Clear();
            action_generator.GetPossibleActions(state, node, action_list);

            FilterActions(state, node, action_list);

            bool timed_out = false;
            for (int o = 0; o < action_list.Count; o++)
            {
                if (!timed_out && _searchWatch != null && _searchWatch.Elapsed.TotalSeconds >= config.search_timeout_seconds)
                    timed_out = true;

                IAction action = action_list[o];

                bool consumed_by_tree = false;
                if (!timed_out && action.Valid && action.Type != 0 && node.alpha < node.beta)
                {
                    CalculateChildNode(state, node, action);
                    consumed_by_tree = true;
                }

                if (!consumed_by_tree)
                    ReleaseAction(action);
            }

            action_list.Clear();
            list_pool.Dispose(action_list);
        }

        private void FilterActions(IGameState state, SearchNode node, List<IAction> action_list)
        {
            int max_taction = node.tdepth < config.depth_wide ? config.actions_per_turn_wide : config.actions_per_turn;
            bool must_end_turn = node.taction >= max_taction;

            int count_valid = 0;
            for (int o = 0; o < action_list.Count; o++)
            {
                IAction action = action_list[o];
                if (must_end_turn && action.Type != config.end_turn_action_type)
                {
                    action.Valid = false;
                    continue;
                }
                action.Sort = heuristic.CalculateActionSort(state, action);
                action.Valid = action.Sort <= 0 || action.Sort >= node.sort_min;
                if (action.Valid)
                    count_valid++;
            }

            int max_actions = node.tdepth < config.depth_wide ? config.nodes_per_action_wide : config.nodes_per_action;
            int max_actions_skip = max_actions + 2;

            if (count_valid <= max_actions_skip)
                return;

            for (int o = 0; o < action_list.Count; o++)
            {
                IAction action = action_list[o];
                if (action.Valid)
                    action.Score = heuristic.CalculateActionScore(state, action);
            }

            action_list.Sort((a, b) => b.Score.CompareTo(a.Score));

            for (int o = 0; o < action_list.Count; o++)
            {
                IAction action = action_list[o];
                action.Valid = action.Valid && o < max_actions;
            }
        }

        private void CalculateChildNode(IGameState state, SearchNode parent, IAction action)
        {
            int player_id = state.GetCurrentPlayerId();
            IGameState new_state = CloneState(state);
            try
            {
                game_logic.ClearResolve();
                game_logic.SetState(new_state);
                game_logic.ExecuteAction(action, player_id);

                bool new_turn = action.Type == config.end_turn_action_type;
                int next_tdepth = parent.tdepth;
                int next_taction = parent.taction + 1;

                if (new_turn)
                {
                    next_tdepth = parent.tdepth + 1;
                    next_taction = 0;
                }

                SearchNode child_node = CreateNode(parent, action, player_id, next_tdepth, next_taction);
                parent.childs.Add(child_node);
                child_node.sort_min = new_turn ? 0 : Mathf.Max(action.Sort, child_node.sort_min);

                if (!new_state.HasEnded() && child_node.tdepth < config.depth)
                {
                    CalculateNode(new_state, child_node);
                }
                else
                {
                    child_node.hvalue = heuristic.CalculateStateScore(new_state, child_node);
                }

                if (player_id == ai_player_id)
                {
                    if (parent.best_child == null || child_node.hvalue > parent.hvalue)
                    {
                        parent.best_child = child_node;
                        parent.hvalue = child_node.hvalue;
                        parent.alpha = Mathf.Max(parent.alpha, parent.hvalue);
                    }
                }
                else
                {
                    if (parent.best_child == null || child_node.hvalue < parent.hvalue)
                    {
                        parent.best_child = child_node;
                        parent.hvalue = child_node.hvalue;
                        parent.beta = Mathf.Min(parent.beta, parent.hvalue);
                    }
                }

                nb_calculated++;
                if (child_node.tdepth > reached_depth)
                    reached_depth = child_node.tdepth;
            }
            finally
            {
                ReturnState(new_state);
            }
        }

        private SearchNode CreateNode(SearchNode parent, IAction action, int player_id, int turn_depth, int turn_action)
        {
            SearchNode nnode = node_pool.Create();
            nnode.current_player = player_id;
            nnode.tdepth = turn_depth;
            nnode.taction = turn_action;
            nnode.parent = parent;
            nnode.last_action = action;
            nnode.alpha = parent != null ? parent.alpha : int.MinValue;
            nnode.beta = parent != null ? parent.beta : int.MaxValue;
            nnode.hvalue = 0;
            nnode.sort_min = 0;
            return nnode;
        }

        public bool IsRunning() => running;
        public int GetNbNodesCalculated() => nb_calculated;
        public int GetDepthReached() => reached_depth;
        public SearchNode GetBest() => best_move;
        public SearchNode GetFirst() => first_node;
        public IAction GetBestAction() => best_move?.last_action;
        public bool IsBestFound() => best_move != null;
        public IGameState GetOriginalState() => original_state;

        /// <summary>
        /// 清空 AI 内存
        /// </summary>
        public void ClearMemory()
        {
            var mainTid = Thread.CurrentThread.ManagedThreadId;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            TimingLog($"[MinimaxEngine] ClearMemory begin mainTid={mainTid}");

            ReturnState(original_state);
            original_state = null;
            first_node = null;
            best_move = null;

            foreach (SearchNode node in node_pool.GetAllActive())
            {
                ReleaseAction(node.last_action);
                node.Clear();
            }

            foreach (List<IAction> list in list_pool.GetAllActive())
            {
                ReleaseActions(list);
                list.Clear();
            }

            node_pool.DisposeAll();
            list_pool.DisposeAll();
            board_pool.DisposeAll();
            // 使用对象池复用节点/列表，避免手动触发 Full GC 造成主线程停顿与卡帧。
            TimingLog($"[MinimaxEngine] ClearMemory done elapsedMs={sw.ElapsedMilliseconds} mainTid={mainTid}");
        }

        private IGameState CloneState(IGameState source)
        {
            if (source is OperationMarigold.AI.Simulation.AIBoardState boardState)
            {
                var cloned = board_pool.Create();
                cloned.CopyFrom(boardState);
                return cloned;
            }

            return source.Clone();
        }

        private void ReturnState(IGameState state)
        {
            if (state is OperationMarigold.AI.Simulation.AIBoardState boardState)
                board_pool.Dispose(boardState);
        }

        private static void ReleaseAction(IAction action)
        {
            if (action is OperationMarigold.AI.Minimax.AIAction aiAction)
                OperationMarigold.AI.Minimax.AIAction.Return(aiAction);
        }

        private static void ReleaseActions(List<IAction> actions)
        {
            for (int i = 0; i < actions.Count; i++)
                ReleaseAction(actions[i]);
        }
    }

}
