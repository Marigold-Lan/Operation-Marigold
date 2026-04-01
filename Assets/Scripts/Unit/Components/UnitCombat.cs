using System.Collections;
using System;
using UnityEngine;
using OperationMarigold.GameplayEvents;

/// <summary>
/// 处理战斗逻辑：读取 UnitData 伤害矩阵，结合地形防御，计算伤害并扣除弹药。
/// </summary>
public class UnitCombat : MonoBehaviour
{
    private struct AttackExecutionData
    {
        public UnitWeaponData Weapon;
        public bool UsePrimary;
        public int DamagePercent;
        public int Damage;
    }

    private const string AttackDebugTag = "[AttackDebug]";
    private static readonly float RotateDuration = 0.12f;
    private const float BeamWidth = 0.7f;
    private const float ChargeDuration = 0.2f;
    private const float ChargeStartScale = 0.08f;
    private const float ChargeEndScale = 0.22f;
    private const float RiseToSkyDuration = 0.6f;
    private const float DiveToTargetDuration = 0.1f;
    private const float SkyRiseHeight = 15f;
    private const float EnergyAccumulateDuration = 0.16f;
    private const float EnergyStartScale = 0.22f;
    private const float EnergyEndScale = 0.5f;
    private const float FinalFadeDuration = 0.08f;
    private static readonly Color BeamShellColor = new Color(1f, 0.16f, 0.16f, 1f);
    private static readonly Color BeamCoreColor = new Color(1f, 0.88f, 0.88f, 1f);

    private UnitController _controller;
    private UnitActionMotion _motion;
    private bool _isAttacking;

    /// <summary>
    /// 攻击开始时触发。（攻击者, 防御者, 预计伤害, 是否主武器）
    /// 这是一个“事实事件出口”，用于表现层/遥测/日志等订阅，不应在此处处理展示逻辑。
    /// </summary>
    public static event System.Action<UnitController, UnitController, int, bool> OnAttackStarted;

    /// <summary>
    /// 伤害真正应用到目标前触发。（攻击者, 受击者, 伤害值）
    /// </summary>
    public static event System.Action<UnitController, UnitController, int> OnDamageApplied;

    /// <summary>
    /// 弹药消耗时触发。（单位, 消耗量；通常为 1）
    /// </summary>
    public static event System.Action<UnitController, int> OnAmmoConsumed;

    /// <summary>
    /// 攻击发起失败时触发。（攻击者, 目标, 原因）
    /// </summary>
    public static event System.Action<UnitController, UnitController, AttackFailReason> OnAttackFailed;

    /// <summary>
    /// 反击开始时触发。（反击者, 目标, 预计伤害, 是否主武器）
    /// </summary>
    public static event System.Action<UnitController, UnitController, int, bool> OnCounterAttackStarted;

    /// <summary>
    /// 单位被击杀时触发。（击杀者, 被击杀者, 造成伤害）
    /// </summary>
    public static event System.Action<UnitController, UnitController, int> OnUnitKilled;

    /// <summary>
    /// 攻击序列（含可能的反击）结束后触发：用于 AI 等待攻击表现播放完毕。
    /// </summary>
    /// <remarks>
    /// 触发时机：EngagementSequence 彻底结束（反击协程完成）后。
    /// </remarks>
    public static event System.Action<UnitController, UnitController, bool> OnAttackSequenceCompleted;

    private void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<UnitController>();
        if (_motion == null)
            _motion = GetComponent<UnitActionMotion>();
        if (_motion == null)
            _motion = gameObject.AddComponent<UnitActionMotion>();
    }

    public void Initialize(UnitController controller)
    {
        _controller = controller;
        if (_motion == null)
            _motion = GetComponent<UnitActionMotion>();
        if (_motion == null)
            _motion = gameObject.AddComponent<UnitActionMotion>();
    }

    /// <summary>
    /// 对目标单位造成伤害。返回预计伤害（实际扣血在攻击序列中执行）。
    /// </summary>
    public int Attack(UnitController target)
    {
        return TryAttack(target, out var predictedDamage) ? predictedDamage : 0;
    }

    /// <summary>
    /// 尝试发起攻击。返回是否真正进入攻击流程（用于判定是否消耗行动）。
    /// </summary>
    public bool TryAttack(UnitController target, out int predictedDamage)
    {
        predictedDamage = 0;
        if (_isAttacking)
        {
            OnAttackFailed?.Invoke(_controller, target, AttackFailReason.AlreadyAttacking);
            return false;
        }
        if (!TryCreateAttackExecutionData(target, requireDamageCapability: false, out var firstStrike, out var failReason))
        {
            OnAttackFailed?.Invoke(_controller, target, failReason);
            return false;
        }

        predictedDamage = firstStrike.Damage;
        StartCoroutine(EngagementSequence(target, firstStrike));
        return true;
    }

    /// <summary>
    /// 使用副武器攻击（不耗弹药）。
    /// </summary>
    public int AttackWithSecondary(UnitController target)
    {
        if (_controller == null || target == null || _controller.Data == null || target.Data == null)
            return 0;
        if (!_controller.Data.HasSecondaryWeapon || _controller.Data.secondaryWeapon == null)
            return 0;
        if (!_controller.Data.secondaryWeapon.CanAttackCategory(target.Data.category))
            return 0;

        var distance = Mathf.Abs(_controller.GridCoord.x - target.GridCoord.x) + Mathf.Abs(_controller.GridCoord.y - target.GridCoord.y);
        if (!_controller.Data.secondaryWeapon.IsDistanceInRange(distance))
            return 0;

        var damagePercent = _controller.Data.secondaryWeapon.GetDamagePercent(target.Data.id);
        var damage = CalculateDamageForStrike(_controller.Data.secondaryWeapon, damagePercent, target);
        if (damage > 0 && target.Health != null)
            target.Health.TakeDamage(damage);
        return damage;
    }

    private IEnumerator EngagementSequence(UnitController defender, AttackExecutionData firstStrike)
    {
        _isAttacking = true;
        yield return ExecuteStrike(defender, firstStrike);

        if (defender != null &&
            defender.Health != null &&
            !defender.Health.IsDead &&
            TryResolveCounterAttack(defender, out var counterStrike))
        {
            OnCounterAttackStarted?.Invoke(defender, _controller, counterStrike.Damage, counterStrike.UsePrimary);
            var defenderCombat = defender.Combat;
            if (defenderCombat != null)
                yield return defenderCombat.ExecuteCounterStrike(_controller, counterStrike);
        }

        _isAttacking = false;
        OnAttackSequenceCompleted?.Invoke(_controller, defender, firstStrike.UsePrimary);
    }

    private IEnumerator ExecuteCounterStrike(UnitController target, AttackExecutionData counterStrike)
    {
        if (_isAttacking)
            yield break;

        _isAttacking = true;
        yield return ExecuteStrike(target, counterStrike);
        _isAttacking = false;
    }

    private IEnumerator ExecuteStrike(UnitController target, AttackExecutionData strike)
    {
        if (target == null)
            yield break;

        OnAttackStarted?.Invoke(_controller, target, strike.Damage, strike.UsePrimary);
        _controller.View?.PlayAttackEffect();
        yield return RotateTowardsTarget(target.transform.position);
        if (_motion != null)
            yield return _motion.PlayBounce(UnitActionMotion.BouncePreset.Attack);
        yield return FireArcProjectile(target, strike.Damage, strike.UsePrimary);
    }

    private bool TryResolveCounterAttack(UnitController defender, out AttackExecutionData counterStrike)
    {
        counterStrike = default;
        if (defender == null || _controller == null)
            return false;
        if (_controller.Health != null && _controller.Health.IsDead)
            return false;
        if (defender.Health != null && defender.Health.IsDead)
            return false;

        var defenderCombat = defender.Combat;
        if (defenderCombat == null || defenderCombat._isAttacking)
            return false;

        // 反击必须满足：存活、射程可达、可攻击目标类别、弹药满足且具备伤害能力。
        return defenderCombat.TryCreateAttackExecutionData(_controller, requireDamageCapability: true, out counterStrike, out _);
    }

    private bool TryCreateAttackExecutionData(UnitController target, bool requireDamageCapability, out AttackExecutionData data, out AttackFailReason failReason)
    {
        data = default;
        if (!CombatRules.TryCreateStrike(
                _controller,
                target,
                requireDamageCapability,
                out var selectedWeapon,
                out var usePrimary,
                out var damagePercent,
                out var damage,
                out failReason))
            return false;

        data = new AttackExecutionData
        {
            Weapon = selectedWeapon,
            UsePrimary = usePrimary,
            DamagePercent = damagePercent,
            Damage = damage
        };
        return true;
    }

    private IEnumerator RotateTowardsTarget(Vector3 targetWorldPos)
    {
        if (RotateDuration <= 0f)
            yield break;

        var from = transform.rotation;
        var toDir = targetWorldPos - transform.position;
        toDir.y = 0f;
        if (toDir.sqrMagnitude < 0.0001f)
            yield break;

        var to = Quaternion.LookRotation(toDir.normalized, Vector3.up);
        var elapsed = 0f;
        while (elapsed < RotateDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / RotateDuration);
            transform.rotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }

        transform.rotation = to;
    }

    private IEnumerator FireArcProjectile(UnitController targetUnit, int damage, bool usePrimary)
    {
        if (targetUnit == null)
            yield break;

        var fxRoot = new GameObject("AttackBeamFx");
        fxRoot.transform.SetParent(transform, worldPositionStays: true);
        var line = fxRoot.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.positionCount = 2;

        var sourcePos = GetCellCenterWorld(_controller, transform);
        var targetPos = GetCellCenterWorld(targetUnit, targetUnit.transform);
        var skyApexPos = sourcePos + Vector3.up * SkyRiseHeight;
        var chargePoint = CreateFxPrimitive(PrimitiveType.Sphere, "AttackChargePoint", sourcePos, ChargeStartScale, BeamCoreColor);
        var energyBlock = (GameObject)null;

        // 第一段：蓄能点缓慢变大（停在发射端）
        var elapsed = 0f;
        while (elapsed < ChargeDuration && targetUnit != null)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / ChargeDuration);
            sourcePos = GetCellCenterWorld(_controller, transform);
            targetPos = GetCellCenterWorld(targetUnit, targetUnit.transform);
            skyApexPos = sourcePos + Vector3.up * SkyRiseHeight;

            var pointScale = Mathf.Lerp(ChargeStartScale, ChargeEndScale, t);
            UpdateFxPrimitive(chargePoint, sourcePos, pointScale, Color.Lerp(BeamShellColor, BeamCoreColor, t));
            UpdateBeamLine(line, sourcePos, sourcePos, BeamWidth * Mathf.Lerp(0.2f, 0.55f, t), 0.75f);
            yield return null;
        }

        // 第二段：从当前格子中心拔地而起，垂直冲向天空顶点。
        elapsed = 0f;
        while (elapsed < RiseToSkyDuration && targetUnit != null)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / RiseToSkyDuration);
            sourcePos = GetCellCenterWorld(_controller, transform);
            targetPos = GetCellCenterWorld(targetUnit, targetUnit.transform);
            skyApexPos = sourcePos + Vector3.up * SkyRiseHeight;
            var currentTip = Vector3.Lerp(sourcePos, skyApexPos, t);

            UpdateFxPrimitive(chargePoint, currentTip, Mathf.Lerp(ChargeEndScale, ChargeStartScale, t), Color.Lerp(BeamCoreColor, BeamShellColor, t));
            UpdateBeamLine(line, sourcePos, currentTip, BeamWidth * Mathf.Lerp(1.15f, 1f, t), 1f);
            yield return null;
        }

        // 第三段：从高空顶点高速俯冲，精准落到目标格子中心。
        elapsed = 0f;
        while (elapsed < DiveToTargetDuration && targetUnit != null)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / DiveToTargetDuration);
            sourcePos = GetCellCenterWorld(_controller, transform);
            targetPos = GetCellCenterWorld(targetUnit, targetUnit.transform);
            skyApexPos = sourcePos + Vector3.up * SkyRiseHeight;
            var currentTip = Vector3.Lerp(skyApexPos, targetPos, t);

            UpdateFxPrimitive(chargePoint, currentTip, Mathf.Lerp(ChargeStartScale, ChargeStartScale * 0.72f, t), Color.Lerp(BeamCoreColor, BeamShellColor, t));
            UpdateBeamLineWithApex(line, sourcePos, skyApexPos, currentTip, BeamWidth * Mathf.Lerp(1.04f, 1f, t), 1f);
            yield return null;
        }

        if (chargePoint != null)
            Destroy(chargePoint);

        if (targetUnit != null && damage > 0)
        {
            if (targetUnit != null && targetUnit.Health != null && !targetUnit.Health.IsDead)
            {
                OnDamageApplied?.Invoke(_controller, targetUnit, damage);
                targetUnit.Health.TakeDamage(damage);
                if (targetUnit.Health != null && targetUnit.Health.IsDead)
                    OnUnitKilled?.Invoke(_controller, targetUnit, damage);
            }
        }

        // 第四段：命中后在目标处汇聚成红色能量块并变大
        if (targetUnit != null)
        {
            sourcePos = GetCellCenterWorld(_controller, transform);
            targetPos = GetCellCenterWorld(targetUnit, targetUnit.transform);
            skyApexPos = sourcePos + Vector3.up * SkyRiseHeight;
            energyBlock = CreateFxPrimitive(PrimitiveType.Cube, "AttackEnergyBlock", targetPos, EnergyStartScale, BeamShellColor);

            elapsed = 0f;
            while (elapsed < EnergyAccumulateDuration && targetUnit != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / EnergyAccumulateDuration);
                sourcePos = GetCellCenterWorld(_controller, transform);
                targetPos = GetCellCenterWorld(targetUnit, targetUnit.transform);
                skyApexPos = sourcePos + Vector3.up * SkyRiseHeight;

                var pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 12f);
                var blockScale = Mathf.Lerp(EnergyStartScale, EnergyEndScale, t) * (1f + pulse * 0.12f);
                var blockColor = Color.Lerp(BeamShellColor, BeamCoreColor, pulse * 0.25f);
                blockColor.a = 1f;

                UpdateFxPrimitive(energyBlock, targetPos, blockScale, blockColor);
                UpdateBeamLineWithApex(line, sourcePos, skyApexPos, targetPos, BeamWidth * Mathf.Lerp(1f, 0.9f, t), Mathf.Lerp(1f, 0.85f, t));
                yield return null;
            }
        }

        if (usePrimary && _controller.CurrentAmmo > 0)
        {
            _controller.CurrentAmmo--;
            OnAmmoConsumed?.Invoke(_controller, 1);
        }

        // 第五段：整体快速消散
        elapsed = 0f;
        while (elapsed < FinalFadeDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / FinalFadeDuration);
            var alpha = 1f - t;

            if (line != null)
            {
                var c1 = BeamCoreColor;
                var c2 = BeamShellColor;
                c1.a = alpha;
                c2.a = alpha;
                line.startColor = c1;
                line.endColor = c2;
            }

            if (energyBlock != null)
            {
                var renderer = energyBlock.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = BeamShellColor;
                    color.a = alpha;
                    renderer.material.color = color;
                }
                energyBlock.transform.localScale *= 1f + Time.deltaTime * 0.8f;
            }

            yield return null;
        }

        if (fxRoot != null)
            Destroy(fxRoot);
        if (energyBlock != null)
            Destroy(energyBlock);
    }

    private void UpdateBeamLine(LineRenderer line, Vector3 start, Vector3 end, float width, float alpha)
    {
        if (line == null)
            return;

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width * 0.9f;

        var core = BeamCoreColor;
        var shell = BeamShellColor;
        core.a = Mathf.Clamp01(alpha);
        shell.a = Mathf.Clamp01(alpha);
        line.startColor = core;
        line.endColor = shell;
    }

    private void UpdateBeamLineWithApex(LineRenderer line, Vector3 start, Vector3 apex, Vector3 end, float width, float alpha)
    {
        if (line == null)
            return;

        line.positionCount = 3;
        line.SetPosition(0, start);
        line.SetPosition(1, apex);
        line.SetPosition(2, end);
        line.startWidth = width;
        line.endWidth = width * 0.9f;

        var core = BeamCoreColor;
        var shell = BeamShellColor;
        core.a = Mathf.Clamp01(alpha);
        shell.a = Mathf.Clamp01(alpha);
        line.startColor = core;
        line.endColor = shell;
    }

    private static Vector3 GetCellCenterWorld(UnitController unit, Transform fallbackTransform)
    {
        if (unit != null)
        {
            var root = unit.MapRoot != null ? unit.MapRoot : MapRoot.Instance;
            if (root != null && root.IsInBounds(unit.GridCoord))
                return root.GridToWorld(unit.GridCoord);
        }

        return fallbackTransform != null ? fallbackTransform.position : Vector3.zero;
    }

    private static GameObject CreateFxPrimitive(PrimitiveType type, string name, Vector3 position, float scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = color;
        }

        return go;
    }

    private static void UpdateFxPrimitive(GameObject fx, Vector3 position, float scale, Color color)
    {
        if (fx == null) return;

        fx.transform.position = position;
        fx.transform.localScale = Vector3.one * scale;
        var renderer = fx.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    /// <summary>
    /// 根据伤害系数与地形防御计算最终伤害。
    /// </summary>
    public int CalculateDamage(int damagePercent, Vector2Int targetCoord, int baseDamage)
    {
        if (_controller == null || _controller.Data == null)
            return 0;

        var mapRoot = _controller.MapRoot != null ? _controller.MapRoot : MapRoot.Instance;
        var currentHp = _controller.Health != null ? _controller.Health.CurrentHp : _controller.Data.maxHp;
        return CombatRules.CalculateScaledDamage(baseDamage, damagePercent, mapRoot, targetCoord, currentHp, _controller.Data.maxHp);
    }

    /// <summary>
    /// 获取地形防御加成（Terrain Stars）。每星提供 10% 减伤（即从伤害百分比中扣除的数值）。
    /// </summary>
    public int GetTerrainDefenseBonus(Vector2Int coord)
    {
        if (_controller == null) return 0;
        return DamageResolver.GetTerrainDefenseBonus(_controller.MapRoot, coord);
    }

    private static string FormatCoord(Vector2Int coord)
    {
        return $"({coord.x},{coord.y})";
    }

    private int CalculateDamageForStrike(UnitWeaponData weapon, int damagePercent, UnitController target)
    {
        if (weapon == null || target == null)
            return 0;
        return CalculateDamage(damagePercent, target.GridCoord, weapon.baseDamage);
    }
}
