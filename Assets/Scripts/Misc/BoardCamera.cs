using OperationMarigold.AI.Execution;
using UnityEngine;

/// <summary>
/// 棋盘摄像机：右键拖动平移，保持俯视角度与距离不变。
/// 支持矩形死区：光标超出死区时相机缓慢跟随。
/// </summary>
[RequireComponent(typeof(Camera))]
public class BoardCamera : Singleton<BoardCamera>
{
    [Header("依赖")]
    public GridCursor gridCursor;

    [Tooltip("留空则在运行时自动查找；AI 执行队列时，用当前行动单位替代光标做死区跟随")]
    public AIActionExecutor aiActionExecutor;

    [Header("平移")]
    public float panSensitivity = 0.15f;

    [Tooltip("限制平移半径，0 表示不限制")]
    public float panClampRadius = 0f;

    [Header("死区跟随")]
    [Tooltip("死区宽度（占屏幕比例 0~1），中心区域不触发跟随")]
    [Range(0.1f, 0.95f)]
    public float deadZoneWidth = 0.5f;

    [Tooltip("死区高度（占屏幕比例 0~1）")]
    [Range(0.1f, 0.95f)]
    public float deadZoneHeight = 0.5f;

    [Tooltip("超出死区后的跟随速度（像素位移→世界单位的缩放）")]
    public float followSpeed = 100f;

    [Tooltip("连移时的跟随速度倍率")]
    public float fastFollowMultiplier = 2.5f;

    private Vector3 _initialPosition;
    private bool _isPanning;
    private bool _isCursorMovingFast;
    private Camera _cam;

    /// <summary>AI 执行期间最近一次行动焦点（含动作间隙），避免清空 ActingSubject 后立刻改跟远处光标把镜头拉回。</summary>
    private Vector3 _aiFollowStickyWorld;
    private bool _hasAiFollowSticky;

    /// <summary>AI 队列结束后仍用该世界点做死区参考，直到玩家移动光标或右键拖镜头。</summary>
    private bool _retainAiFocusAfterExecute;

    private bool _wasAiExecuting;

    /// <summary>开局重定位后的若干帧内不跑死区跟随，避免数值残差与首帧二次修正造成闪动。</summary>
    private int _suppressDeadZoneFollowFrames;

    private void OnEnable()
    {
        InputManager.OnCursorMoveSpeedChanged += SetCursorMovingFast;
        InputManager.OnCursorMoved += ClearRetainedAiFocusOnPlayerCursor;
    }

    private void OnDisable()
    {
        InputManager.OnCursorMoveSpeedChanged -= SetCursorMovingFast;
        InputManager.OnCursorMoved -= ClearRetainedAiFocusOnPlayerCursor;
    }

    private void ClearRetainedAiFocusOnPlayerCursor()
    {
        if (aiActionExecutor != null && aiActionExecutor.IsExecuting)
            return;
        _retainAiFocusAfterExecute = false;
        _hasAiFollowSticky = false;
    }

    private void SetCursorMovingFast(bool isFast)
    {
        _isCursorMovingFast = isFast;
    }

    private void Start()
    {
        _initialPosition = transform.position;
        _cam = GetComponent<Camera>();
        if (gridCursor == null)
            gridCursor = GridCursor.Instance;
        if (aiActionExecutor == null)
            aiActionExecutor = FindFirstObjectByType<AIActionExecutor>();
    }

    /// <summary>
    /// 将相机水平中心移到指定世界坐标（保留当前俯视高度 Y），并更新平移基准点（含 panClamp 圆心）。
    /// 用于开局对准总部等。
    /// </summary>
    public void RecenterPivotAtWorldPoint(Vector3 worldPoint)
    {
        var p = transform.position;
        p.x = worldPoint.x;
        p.z = worldPoint.z;
        transform.position = p;
        _initialPosition = p;
    }

    /// <summary>
    /// 平移相机（仅水平，沿 right/forward），使 followWorld 的屏幕投影对齐屏幕中心。
    /// 死区矩形以屏幕中心为轴，故中心点必在死区内，与 <see cref="ApplyDeadZoneFollow"/> 判定一致。
    /// 会更新 <see cref="_initialPosition"/>，并在随后几帧抑制死区跟随以免闪动。
    /// </summary>
    public void RecenterPivotSoFollowInDeadZone(Vector3 followWorld)
    {
        if (_cam == null)
            _cam = GetComponent<Camera>();

        Canvas.ForceUpdateCanvases();

        var yKeep = transform.position.y;

        // 初值：XZ 对准跟随点，大幅缩短迭代（HQ 在地图一角时尤其重要）
        var p = transform.position;
        p.x = followWorld.x;
        p.z = followWorld.z;
        p.y = yKeep;
        transform.position = p;

        var right = FlattenXZ(transform.right);
        var forward = FlattenXZ(transform.forward);

        var goalX = Screen.width * 0.5f;
        var goalY = Screen.height * 0.5f;
        const int maxIter = 80;
        const float eps = 0.1f;
        const float screenEps = 0.75f;
        const float maxStep = 80f;

        for (var iter = 0; iter < maxIter; iter++)
        {
            var screenPos = _cam.WorldToScreenPoint(followWorld);
            if (screenPos.z <= 0.01f)
                break;

            var errX = goalX - screenPos.x;
            var errY = goalY - screenPos.y;
            if (errX * errX + errY * errY < screenEps * screenEps)
                break;

            var pos0 = transform.position;
            pos0.y = yKeep;

            transform.position = pos0 + right * eps;
            var sR = _cam.WorldToScreenPoint(followWorld);
            transform.position = pos0 + forward * eps;
            var sF = _cam.WorldToScreenPoint(followWorld);
            transform.position = pos0;

            var dRx = (sR.x - screenPos.x) / eps;
            var dRy = (sR.y - screenPos.y) / eps;
            var dFx = (sF.x - screenPos.x) / eps;
            var dFy = (sF.y - screenPos.y) / eps;

            var det = dRx * dFy - dFx * dRy;
            Vector3 step;
            if (Mathf.Abs(det) < 1e-6f)
            {
                var scale = followSpeed / Mathf.Max(1, Mathf.Max(Screen.width, Screen.height)) * 0.5f;
                step = (right * errX + forward * errY) * scale;
            }
            else
            {
                var a = (errX * dFy - errY * dFx) / det;
                var b = (dRx * errY - dRy * errX) / det;
                step = right * a + forward * b;
            }

            if (step.sqrMagnitude > maxStep * maxStep)
                step = step.normalized * maxStep;

            var next = pos0 + step;
            next.y = yKeep;
            transform.position = next;
        }

        // 残差：用与运行时相同的矩形死区再收一次（目标为死区内最近点，通常即已居中）
        for (var k = 0; k < 24; k++)
        {
            var sp = _cam.WorldToScreenPoint(followWorld);
            if (sp.z <= 0.01f)
                break;
            var (inZone, deltaScreen) = GetDeadZoneOffset(sp);
            if (inZone)
                break;

            var pos0 = transform.position;
            pos0.y = yKeep;
            transform.position = pos0 + right * eps;
            var sR = _cam.WorldToScreenPoint(followWorld);
            transform.position = pos0 + forward * eps;
            var sF = _cam.WorldToScreenPoint(followWorld);
            transform.position = pos0;

            var dRx = (sR.x - sp.x) / eps;
            var dRy = (sR.y - sp.y) / eps;
            var dFx = (sF.x - sp.x) / eps;
            var dFy = (sF.y - sp.y) / eps;
            var det = dRx * dFy - dFx * dRy;
            if (Mathf.Abs(det) < 1e-6f)
                break;

            var tx = -deltaScreen.x;
            var ty = -deltaScreen.y;
            var a = (tx * dFy - ty * dFx) / det;
            var b = (dRx * ty - dRy * tx) / det;
            var st = right * a + forward * b;
            if (st.sqrMagnitude > maxStep * maxStep)
                st = st.normalized * maxStep;
            var nx = pos0 + st;
            nx.y = yKeep;
            transform.position = nx;
        }

        var final = transform.position;
        final.y = yKeep;
        transform.position = final;
        _initialPosition = final;
        _suppressDeadZoneFollowFrames = 2;
    }

    private void Update()
    {
        var exec = aiActionExecutor;
        bool nowExecuting = exec != null && exec.IsExecuting;
        if (_wasAiExecuting && !nowExecuting && _hasAiFollowSticky)
            _retainAiFocusAfterExecute = true;

        UpdatePanState();
        if (_isPanning)
            ApplyPan();
        else
            ApplyDeadZoneFollow();

        _wasAiExecuting = nowExecuting;
    }

    private void UpdatePanState()
    {
        if (Input.GetMouseButtonDown(1))
        {
            _isPanning = true;
            _retainAiFocusAfterExecute = false;
            _hasAiFollowSticky = false;
        }

        if (Input.GetMouseButtonUp(1)) _isPanning = false;
    }

    private void ApplyPan()
    {
        var delta = GetPanDelta();
        var pos = transform.position + delta;
        pos.y = _initialPosition.y;

        if (panClampRadius > 0f)
            pos = ClampPosition(pos);

        transform.position = pos;
    }

    private Vector3 GetPanDelta()
    {
        float dx = Input.GetAxis("Mouse X");
        float dy = Input.GetAxis("Mouse Y");

        var right = FlattenXZ(transform.right);
        var forward = FlattenXZ(transform.forward);

        return - (right * dx + forward * dy) * panSensitivity;
    }

    private static Vector3 FlattenXZ(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude > 0.001f) v.Normalize();
        return v;
    }

    private Vector3 ClampPosition(Vector3 pos)
    {
        var offset = pos - _initialPosition;
        offset.y = 0f;

        if (offset.sqrMagnitude <= panClampRadius * panClampRadius)
            return pos;

        offset = offset.normalized * panClampRadius;
        var clamped = _initialPosition + offset;
        clamped.y = _initialPosition.y;
        return clamped;
    }

    private void ApplyDeadZoneFollow()
    {
        if (_cam == null) return;

        if (_suppressDeadZoneFollowFrames > 0)
        {
            _suppressDeadZoneFollowFrames--;
            return;
        }

        if (!TryGetDeadZoneFollowWorldPoint(out var followWorld))
            return;

        var screenPos = _cam.WorldToScreenPoint(followWorld);
        var (inZone, deltaScreen) = GetDeadZoneOffset(screenPos);

        if (inZone) return;

        var right = FlattenXZ(transform.right);
        var forward = FlattenXZ(transform.forward);

        var speed = followSpeed * (_isCursorMovingFast ? fastFollowMultiplier : 1f);
        var scale = speed / Mathf.Max(Screen.width, Screen.height) * Time.deltaTime;
        var deltaWorld = (right * deltaScreen.x + forward * deltaScreen.y) * scale;
        var pos = transform.position + deltaWorld;
        pos.y = _initialPosition.y;

        if (panClampRadius > 0f)
            pos = ClampPosition(pos);

        transform.position = pos;
    }

    /// <summary>AI 正在执行某一动作时优先跟随行动单位，否则跟随棋盘光标。</summary>
    private bool TryGetDeadZoneFollowWorldPoint(out Vector3 world)
    {
        var exec = aiActionExecutor;

        if (exec != null && exec.IsExecuting)
        {
            if (exec.ActingSubject != null)
            {
                _aiFollowStickyWorld = exec.ActingSubject.position;
                _hasAiFollowSticky = true;
                world = _aiFollowStickyWorld;
                return true;
            }

            if (_hasAiFollowSticky)
            {
                world = _aiFollowStickyWorld;
                return true;
            }
        }
        else if (_retainAiFocusAfterExecute && _hasAiFollowSticky)
        {
            world = _aiFollowStickyWorld;
            return true;
        }

        if (gridCursor != null)
        {
            world = gridCursor.transform.position;
            return true;
        }

        world = default;
        return false;
    }

    private (bool inZone, Vector2 deltaScreen) GetDeadZoneOffset(Vector3 screenPos)
    {
        var w = Screen.width;
        var h = Screen.height;
        var cx = w * 0.5f;
        var cy = h * 0.5f;
        var halfW = w * deadZoneWidth * 0.5f;
        var halfH = h * deadZoneHeight * 0.5f;

        var minX = cx - halfW;
        var maxX = cx + halfW;
        var minY = cy - halfH;
        var maxY = cy + halfH;

        var px = screenPos.x;
        var py = screenPos.y;

        float dx = 0f, dy = 0f;
        if (px < minX) dx = px - minX;
        else if (px > maxX) dx = px - maxX;
        if (py < minY) dy = py - minY;
        else if (py > maxY) dy = py - maxY;

        var inZone = dx == 0f && dy == 0f;
        return (inZone, new Vector2(dx, dy));
    }
}
