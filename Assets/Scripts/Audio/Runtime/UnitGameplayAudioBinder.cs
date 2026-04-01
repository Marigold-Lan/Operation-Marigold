using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅单位玩法层静态事件（移动步进/转身、战斗、运输、补给），经 <see cref="AudioEventMapSO"/> 播放 SFX。
/// 与 UnitMovement / UnitCombat 等无直接引用耦合，仅依赖事件。
/// </summary>
public class UnitGameplayAudioBinder : MonoBehaviour
{
    [SerializeField] private AudioEventMapSO _eventMap;
    [SerializeField] private GameAudioManager _audioManager;

    private readonly HashSet<GameAudioEvent> _missingEventWarned = new HashSet<GameAudioEvent>();
    private bool _missingEventMapWarned;

    private IGameAudioService AudioService => _audioManager != null ? _audioManager : GameAudioManager.Instance;

    private void Awake()
    {
        if (_audioManager == null)
            _audioManager = GameAudioManager.Instance;
    }

    private void OnEnable()
    {
        UnitMovement.OnAnyUnitMoveStateChanged += HandleAnyUnitMoveStateChanged;
        UnitMovement.OnMoveStepCompleted += HandleMoveStepCompleted;
        UnitMovement.OnMoveStepJump += HandleMoveStepJump;
        UnitMovement.OnMoveFacingTurnStarted += HandleMoveFacingTurnStarted;
        UnitCombat.OnAttackStarted += HandleAttackStarted;
        UnitCombat.OnDamageApplied += HandleDamageApplied;
        UnitTransport.OnLoaded += HandleTransportLoaded;
        UnitTransport.OnDropped += HandleTransportDropped;
        UnitSupply.OnSupplyPerformed += HandleSupplyPerformed;
    }

    private void OnDisable()
    {
        UnitMovement.OnAnyUnitMoveStateChanged -= HandleAnyUnitMoveStateChanged;
        UnitMovement.OnMoveStepCompleted -= HandleMoveStepCompleted;
        UnitMovement.OnMoveStepJump -= HandleMoveStepJump;
        UnitMovement.OnMoveFacingTurnStarted -= HandleMoveFacingTurnStarted;
        UnitCombat.OnAttackStarted -= HandleAttackStarted;
        UnitCombat.OnDamageApplied -= HandleDamageApplied;
        UnitTransport.OnLoaded -= HandleTransportLoaded;
        UnitTransport.OnDropped -= HandleTransportDropped;
        UnitSupply.OnSupplyPerformed -= HandleSupplyPerformed;
    }

    private void HandleAnyUnitMoveStateChanged(bool isMoving)
    {
        PlaySfx(isMoving ? GameAudioEvent.UnitMoveStarted : GameAudioEvent.UnitMoveEnded, Vector3.zero);
    }

    private void HandleMoveStepCompleted(UnitController unit, Vector2Int _, Vector2Int __)
    {
        if (unit == null)
            return;
        PlaySfx(GameAudioEvent.UnitMoveStep, unit.transform.position);
    }

    private void HandleMoveStepJump(UnitController unit, Vector2Int _, Vector2Int __)
    {
        if (unit == null)
            return;
        PlaySfx(GameAudioEvent.UnitMoveJump, unit.transform.position);
    }

    private void HandleMoveFacingTurnStarted(UnitController unit)
    {
        if (unit == null)
            return;
        PlaySfx(GameAudioEvent.UnitMoveTurn, unit.transform.position);
    }

    private void HandleAttackStarted(UnitController attacker, UnitController _, int __, bool ___)
    {
        if (attacker == null)
            return;
        PlaySfx(GameAudioEvent.UnitAttackFire, attacker.transform.position);
    }

    private void HandleDamageApplied(UnitController _, UnitController defender, int __)
    {
        if (defender == null)
            return;
        PlaySfx(GameAudioEvent.UnitHitReceived, defender.transform.position);
    }

    private void HandleTransportLoaded(UnitController transporter, UnitController cargo)
    {
        var pos = transporter != null ? transporter.transform.position : Vector3.zero;
        PlaySfx(GameAudioEvent.UnitTransportLoad, pos);
    }

    private void HandleTransportDropped(UnitController transporter, UnitController _, Vector2Int __)
    {
        var pos = transporter != null ? transporter.transform.position : Vector3.zero;
        PlaySfx(GameAudioEvent.UnitTransportUnload, pos);
    }

    private void HandleSupplyPerformed(UnitController supplier, UnitController _, bool ok)
    {
        if (!ok || supplier == null)
            return;
        PlaySfx(GameAudioEvent.UnitSupplyPerformed, supplier.transform.position);
    }

    private void PlaySfx(GameAudioEvent gameEvent, Vector3 worldPos)
    {
        if (_eventMap == null)
        {
            if (!_missingEventMapWarned)
                _missingEventMapWarned = true;
            return;
        }

        if (!_eventMap.TryGetCueId(gameEvent, out var cueId))
        {
            _missingEventWarned.Add(gameEvent);
            return;
        }

        var service = AudioService;
        if (service == null)
            return;

        service.PlaySfx(cueId, worldPos);
    }
}
