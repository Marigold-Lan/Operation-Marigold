using System.Collections;
using UnityEngine;

/// <summary>
/// 控制方向光：订阅 DayInfoPanel 小太阳开始公转事件，与太阳同步绕 Z 轴自转一周。
/// 挂在与 Directional Light 同层级的父物体（如 LightControler）上即可。
/// </summary>
public class LightController : MonoBehaviour
{
    [Header("依赖")]
    [Tooltip("不赋值则运行时查找场景中的 DayInfoPanelController。")]
    [SerializeField] private DayInfoPanelController _dayInfoPanelController;

    [Header("旋转")]
    [Tooltip("绕 Z 轴旋转方向。true=与小太阳同向（逆时针）。")]
    [SerializeField] private bool _counterClockwise = true;

    private Coroutine _orbitRoutine;

    private void Awake()
    {
        if (_dayInfoPanelController == null)
            _dayInfoPanelController = FindFirstObjectByType<DayInfoPanelController>();
    }

    private void OnEnable()
    {
        if (_dayInfoPanelController != null)
            _dayInfoPanelController.OnSunOrbitStarted += HandleSunOrbitStarted;
    }

    private void OnDisable()
    {
        if (_dayInfoPanelController != null)
            _dayInfoPanelController.OnSunOrbitStarted -= HandleSunOrbitStarted;
        if (_orbitRoutine != null)
        {
            StopCoroutine(_orbitRoutine);
            _orbitRoutine = null;
        }
    }

    private void HandleSunOrbitStarted(float duration)
    {
        if (_orbitRoutine != null)
        {
            StopCoroutine(_orbitRoutine);
            _orbitRoutine = null;
        }
        _orbitRoutine = StartCoroutine(RotateAroundZOneTurn(duration));
    }

    private IEnumerator RotateAroundZOneTurn(float duration)
    {
        if (duration <= 0f)
        {
            _orbitRoutine = null;
            yield break;
        }

        var startEuler = transform.localEulerAngles;
        var startZ = startEuler.z;
        var sign = _counterClockwise ? 1f : -1f;

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var z = startZ + sign * 360f * t;
            transform.localEulerAngles = new Vector3(startEuler.x, startEuler.y, z);
            yield return null;
        }

        transform.localEulerAngles = new Vector3(startEuler.x, startEuler.y, startZ + sign * 360f);
        _orbitRoutine = null;
    }
}
