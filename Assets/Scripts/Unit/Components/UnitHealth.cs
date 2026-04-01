using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 管理单位生命值增减、死亡逻辑，通过事件通知 UnitView 等表现层。
/// </summary>
public class UnitHealth : MonoBehaviour
{
    [SerializeField] private int _currentHp;

    private UnitController _controller;
    private bool _deathNotified;
    private bool _deathDestroyScheduled;

    /// <summary>
    /// 当前生命值发生变化时触发（旧值, 新值）。
    /// </summary>
    public event Action<int, int> OnHpChanged;

    /// <summary>
    /// 单位死亡时触发。
    /// </summary>
    public event Action OnDeath;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public int CurrentHp
    {
        get => _currentHp;
        private set
        {
            var oldVal = _currentHp;
            _currentHp = Mathf.Max(0, value);
            OnHpChanged?.Invoke(oldVal, _currentHp);
            if (_currentHp <= 0)
                HandleDeathEntered();
        }
    }

    /// <summary>
    /// 是否已死亡。
    /// </summary>
    public bool IsDead => _currentHp <= 0;

    private void Awake()
    {
        _controller = GetComponent<UnitController>();
    }

    /// <summary>
    /// 初始化生命值（由 UnitController 在生成时调用）。
    /// </summary>
    public void Initialize(int hp)
    {
        _currentHp = Mathf.Max(0, hp);
        _deathNotified = _currentHp <= 0;
        _deathDestroyScheduled = false;
    }

    /// <summary>
    /// 受到伤害。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        CurrentHp -= amount;
    }

    /// <summary>
    /// 治疗。
    /// </summary>
    public void Heal(int amount)
    {
        if (IsDead) return;
        var maxHp = _controller != null && _controller.Data != null ? _controller.Data.maxHp : 10;
        CurrentHp = Mathf.Min(CurrentHp + amount, maxHp);
    }

    /// <summary>
    /// 设置生命值（用于加载/模拟等）。
    /// </summary>
    public void SetHp(int hp)
    {
        _currentHp = Mathf.Max(0, hp);
        _deathNotified = _currentHp <= 0;
        _deathDestroyScheduled = false;
    }

    private void HandleDeathEntered()
    {
        _controller?.CurrentCell?.ClearUnit();
        if (_controller != null)
            _controller.CurrentCell = null;

        if (!_deathNotified)
        {
            _deathNotified = true;
            OnDeath?.Invoke();
        }

        if (_deathDestroyScheduled)
            return;

        _deathDestroyScheduled = true;
        StartCoroutine(DeathDestroySequence());
    }

    private IEnumerator DeathDestroySequence()
    {
        var view = _controller != null ? _controller.View : GetComponent<UnitView>();
        if (view != null)
            yield return view.PlayDeathEffectRoutine();

        if (this != null && gameObject != null)
            Destroy(gameObject);
    }
}
