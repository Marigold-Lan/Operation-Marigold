using System.Collections;
using UnityEngine;

/// <summary>
/// 纯粹视觉表现层。订阅 UnitHealth 等事件，播放动画/特效。AI 模拟时不挂载，逻辑可脱离运行。
/// </summary>
public class UnitView : MonoBehaviour
{
    [Header("Death FX")]
    [SerializeField, Min(0f)] private float _deathEffectDuration = 0.35f;

    private UnitController _controller;
    private UnitHealth _health;
    private bool _deathFxPlayed;

    private void Awake()
    {
        _controller = GetComponent<UnitController>();
        _health = GetComponent<UnitHealth>();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnHpChanged += OnHpChanged;
            _health.OnDeath += OnDeath;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnHpChanged -= OnHpChanged;
            _health.OnDeath -= OnDeath;
        }
    }

    private void OnHpChanged(int oldHp, int newHp)
    {
        if (newHp < oldHp)
            PlayDamagedEffect();
    }

    private void OnDeath()
    {
        PlayDeathEffect();
        _deathFxPlayed = true;
    }

    /// <summary>
    /// 播放受伤效果（可重写或由 Animator 驱动）。
    /// </summary>
    protected virtual void PlayDamagedEffect()
    {
        // 占位：后续接入 Animator 或粒子
    }

    /// <summary>
    /// 播放死亡效果。
    /// </summary>
    protected virtual void PlayDeathEffect()
    {
        // Intentionally empty.
        // Death visuals are managed by scene-level managers (e.g., UnitDeathDustManager),
        // keeping UnitView free of concrete FX/prefab dependencies.
    }

    /// <summary>
    /// 播放可等待的死亡表现流程。默认实现为触发死亡特效并等待固定时长。
    /// </summary>
    public virtual IEnumerator PlayDeathEffectRoutine()
    {
        if (!_deathFxPlayed)
        {
            PlayDeathEffect();
            _deathFxPlayed = true;
        }
        if (_deathEffectDuration > 0f)
            yield return new WaitForSeconds(_deathEffectDuration);
    }

    /// <summary>
    /// 播放移动动画（由 UnitMovement 或上层在移动完成后调用）。
    /// </summary>
    public virtual void PlayMoveEffect()
    {
        // 占位
    }

    /// <summary>
    /// 播放开火动画/特效。
    /// </summary>
    public virtual void PlayAttackEffect()
    {
        // 占位
    }
}
