using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 中央输入管理器。统一读取按键并发出输入事件，便于扩展新输入动作。
/// 新增按键：在此添加检测逻辑并触发对应事件即可。
/// </summary>
public class InputManager : Singleton<InputManager>
{
    public static event Action<bool> OnCursorMoveSpeedChanged;
    /// <summary>任意光标（网格或菜单）实际移动时触发。仅当光标真的移动了才由各光标调用 NotifyCursorMoved 派发。</summary>
    public static event Action OnCursorMoved;

    /// <summary>由 GridCursor、VerticalMenuNavigator 等在实际发生移动时调用，用于派发 OnCursorMoved 并参与“连按快速移动”判定。</summary>
    public static void NotifyCursorMoved()
    {
        _anyRepeatMoveActuallyMoved = true;
        OnCursorMoved?.Invoke();
    }

    private static bool _anyRepeatMoveActuallyMoved;

    private struct MoveHandlerEntry
    {
        public Func<int, int, bool> Handler;
        public int Priority;
        public long Sequence;
    }

    private struct ConfirmHandlerEntry
    {
        public Func<Vector2Int, bool> Handler;
        public int Priority;
        public long Sequence;
    }

    private struct CancelHandlerEntry
    {
        public Func<bool> Handler;
        public int Priority;
        public long Sequence;
    }

    private static readonly List<MoveHandlerEntry> MoveHandlers = new List<MoveHandlerEntry>();
    private static readonly List<ConfirmHandlerEntry> ConfirmHandlers = new List<ConfirmHandlerEntry>();
    private static readonly List<CancelHandlerEntry> CancelHandlers = new List<CancelHandlerEntry>();
    private static long _handlerSequence;

    private static int _inputBlockCount;

    /// <summary>阻塞玩家输入（如回合/天数动画播放中）。可嵌套，需成对调用 UnblockInput。</summary>
    public static void BlockInput()
    {
        _inputBlockCount++;
    }

    /// <summary>解除一层输入阻塞。</summary>
    public static void UnblockInput()
    {
        if (_inputBlockCount > 0)
            _inputBlockCount--;
    }

    [Header("光标（用于确认键的上下文坐标）")]
    [Tooltip("用于获取确认时的格子坐标，可留空则尝试自动查找")]
    public GridCursor cursor;
    [Header("依赖")]
    [SerializeField] private GameStateFacade _gameStateFacade;

    [Header("光标移动连按")]
    [Tooltip("按住后开始连移的延迟（秒）")]
    public float moveRepeatDelay = 0.4f;

    [Tooltip("连移时每格间隔（秒）")]
    public float moveRepeatInterval = 0.08f;

    [Header("按键绑定")]
    public KeyCode confirmKey = KeyCode.J;
    public KeyCode cancelKey = KeyCode.Escape;
    public KeyCode moveUpKey = KeyCode.W;
    public KeyCode moveDownKey = KeyCode.S;
    public KeyCode moveLeftKey = KeyCode.A;
    public KeyCode moveRightKey = KeyCode.D;

    [Header("手柄（NS/通用）")]
    [Tooltip("左摇杆/方向键死区，低于此值不视为输入")]
    [Range(0.1f, 0.9f)]
    public float gamepadMoveDeadzone = 0.4f;
    [Tooltip("确认键：手柄 A 键，对应键盘 J")]
    public bool gamepadConfirmEnabled = true;
    [Tooltip("取消键：手柄 B 键，对应键盘 ESC")]
    public bool gamepadCancelEnabled = true;

    private KeyCode? _heldMoveKey;
    private float _heldTime;
    private bool _isRapidMove;
    private KeyCode? _lastFrameGamepadMoveKey;

    public static void RegisterMoveHandler(Func<int, int, bool> handler, int priority = 0)
    {
        if (handler == null)
            return;

        UnregisterMoveHandler(handler);
        MoveHandlers.Add(new MoveHandlerEntry
        {
            Handler = handler,
            Priority = priority,
            Sequence = ++_handlerSequence
        });
        MoveHandlers.Sort((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : b.Sequence.CompareTo(a.Sequence);
        });
    }

    public static void UnregisterMoveHandler(Func<int, int, bool> handler)
    {
        if (handler == null)
            return;

        for (var i = MoveHandlers.Count - 1; i >= 0; i--)
        {
            if (MoveHandlers[i].Handler == handler)
                MoveHandlers.RemoveAt(i);
        }
    }

    public static void RegisterConfirmHandler(Func<Vector2Int, bool> handler, int priority = 0)
    {
        if (handler == null)
            return;

        UnregisterConfirmHandler(handler);
        ConfirmHandlers.Add(new ConfirmHandlerEntry
        {
            Handler = handler,
            Priority = priority,
            Sequence = ++_handlerSequence
        });
        ConfirmHandlers.Sort((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : b.Sequence.CompareTo(a.Sequence);
        });
    }

    public static void UnregisterConfirmHandler(Func<Vector2Int, bool> handler)
    {
        if (handler == null)
            return;

        for (var i = ConfirmHandlers.Count - 1; i >= 0; i--)
        {
            if (ConfirmHandlers[i].Handler == handler)
                ConfirmHandlers.RemoveAt(i);
        }
    }

    public static void RegisterCancelHandler(Func<bool> handler, int priority = 0)
    {
        if (handler == null)
            return;

        UnregisterCancelHandler(handler);
        CancelHandlers.Add(new CancelHandlerEntry
        {
            Handler = handler,
            Priority = priority,
            Sequence = ++_handlerSequence
        });
        CancelHandlers.Sort((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : b.Sequence.CompareTo(a.Sequence);
        });
    }

    public static void UnregisterCancelHandler(Func<bool> handler)
    {
        if (handler == null)
            return;

        for (var i = CancelHandlers.Count - 1; i >= 0; i--)
        {
            if (CancelHandlers[i].Handler == handler)
                CancelHandlers.RemoveAt(i);
        }
    }

    [Header("鼠标")]
    [Tooltip("是否隐藏系统鼠标光标（游戏内使用网格光标等）")]
    [SerializeField] private bool _hideCursor = true;

    private void Start()
    {
        Cursor.visible = !_hideCursor;
        if (cursor == null)
            cursor = GridCursor.Instance;
        if (_gameStateFacade == null)
        {
#if UNITY_2023_1_OR_NEWER
            _gameStateFacade = FindFirstObjectByType<GameStateFacade>(FindObjectsInactive.Include);
#else
            _gameStateFacade = FindObjectOfType<GameStateFacade>();
#endif
        }
    }

    private void Update()
    {
        // 游戏结束时仍处理输入，以便 Victory 等结束界面能响应确认/取消/上下移动
        var session = _gameStateFacade != null ? _gameStateFacade.Session : null;
        if (session != null && session.IsGameOver)
        {
            UpdateMoveInput();
            UpdateConfirmInput();
            UpdateCancelInput();
            return;
        }

        var context = TurnManager.Instance != null ? TurnManager.Instance.CurrentContext : default;
        var isPlayerMainPhase = TurnManager.Instance == null ||
                                (context.Phase == TurnPhase.Main &&
                                 session != null &&
                                 context.Faction == session.CurrentFaction);

        if (!isPlayerMainPhase)
            return;
        if (_inputBlockCount > 0)
            return;

        UpdateMoveInput();
        UpdateConfirmInput();
        UpdateCancelInput();
    }

    /// <summary>从左摇杆/方向键读取方向，映射为与 WASD 相同的 KeyCode。优先级：上 &gt; 下 &gt; 左 &gt; 右。</summary>
    private KeyCode? GetGamepadMoveKey()
    {
        var h = Input.GetAxisRaw("Horizontal");
        var v = Input.GetAxisRaw("Vertical");
        if (v > gamepadMoveDeadzone) return moveUpKey;
        if (v < -gamepadMoveDeadzone) return moveDownKey;
        if (h > gamepadMoveDeadzone) return moveRightKey;
        if (h < -gamepadMoveDeadzone) return moveLeftKey;
        return null;
    }

    private void UpdateMoveInput()
    {
        var keyboardPressed = (KeyCode?)null;
        if (Input.GetKey(moveUpKey)) keyboardPressed = moveUpKey;
        else if (Input.GetKey(moveDownKey)) keyboardPressed = moveDownKey;
        else if (Input.GetKey(moveLeftKey)) keyboardPressed = moveLeftKey;
        else if (Input.GetKey(moveRightKey)) keyboardPressed = moveRightKey;

        var gamepadPressed = GetGamepadMoveKey();
        var pressed = keyboardPressed ?? gamepadPressed;

        if (!pressed.HasValue)
        {
            _lastFrameGamepadMoveKey = null;
            if (_isRapidMove)
            {
                _isRapidMove = false;
                OnCursorMoveSpeedChanged?.Invoke(false);
            }
            _heldMoveKey = null;
            _heldTime = 0f;
            return;
        }

        var isKeyDown = keyboardPressed.HasValue
            ? Input.GetKeyDown(pressed.Value)
            : (gamepadPressed != _lastFrameGamepadMoveKey);
        _lastFrameGamepadMoveKey = gamepadPressed;

        if (isKeyDown)
        {
            _heldMoveKey = pressed;
            _heldTime = 0f;
            EmitMove(pressed.Value);
            return;
        }

        if (_heldMoveKey != pressed)
        {
            _heldMoveKey = pressed;
            _heldTime = 0f;
            return;
        }

        _heldTime += Time.deltaTime;
        if (_heldTime < moveRepeatDelay) return;

        var elapsed = _heldTime - moveRepeatDelay;
        var n = Mathf.FloorToInt(elapsed / moveRepeatInterval);
        _heldTime = moveRepeatDelay + (elapsed - n * moveRepeatInterval);

        if (n > 0)
        {
            _anyRepeatMoveActuallyMoved = false;
            for (var i = 0; i < n; i++)
                EmitMove(pressed.Value);
            if (_anyRepeatMoveActuallyMoved && !_isRapidMove)
            {
                _isRapidMove = true;
                OnCursorMoveSpeedChanged?.Invoke(true);
            }
        }
    }

    private void EmitMove(KeyCode key)
    {
        int dx = 0, dy = 0;
        if (key == moveUpKey) dy = 1;
        else if (key == moveDownKey) dy = -1;
        else if (key == moveLeftKey) dx = -1;
        else if (key == moveRightKey) dx = 1;
        if (dx != 0 || dy != 0)
            DispatchMove(dx, dy);
    }

    private void UpdateConfirmInput()
    {
        var confirm = Input.GetKeyDown(confirmKey) ||
                     (gamepadConfirmEnabled && Input.GetKeyDown(KeyCode.JoystickButton0)); // NS 手柄 A 键
        if (!confirm) return;

        var coord = cursor != null ? cursor.Coord : Vector2Int.zero;
        DispatchConfirm(coord);
    }

    private void UpdateCancelInput()
    {
        var cancel = Input.GetKeyDown(cancelKey) ||
                     (gamepadCancelEnabled && Input.GetKeyDown(KeyCode.JoystickButton1)); // NS 手柄 B 键
        if (cancel)
            DispatchCancel();
    }

    private static bool DispatchMove(int dx, int dy)
    {
        for (var i = 0; i < MoveHandlers.Count; i++)
        {
            var handler = MoveHandlers[i].Handler;
            if (handler == null)
                continue;
            if (handler.Invoke(dx, dy))
                return true;
        }

        return false;
    }

    private static bool DispatchConfirm(Vector2Int coord)
    {
        for (var i = 0; i < ConfirmHandlers.Count; i++)
        {
            var handler = ConfirmHandlers[i].Handler;
            if (handler == null)
                continue;
            if (handler.Invoke(coord))
                return true;
        }

        return false;
    }

    private static bool DispatchCancel()
    {
        for (var i = 0; i < CancelHandlers.Count; i++)
        {
            var handler = CancelHandlers[i].Handler;
            if (handler == null)
                continue;
            if (handler.Invoke())
                return true;
        }

        return false;
    }
}
