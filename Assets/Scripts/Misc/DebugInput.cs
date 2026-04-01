using UnityEngine;

/// <summary>
/// 调试输入：独立于 InputManager，用于全单位范围可视化开关。
/// F1 切换全单位移动范围高亮，F2 切换全单位攻击范围高亮。
/// F3 给 Marigold 增加 10000 资金，F4 给 Lancel 增加 10000 资金。
/// F5 己方一键获胜，F6 己方一键失败。
/// </summary>
public class DebugInput : MonoBehaviour
{
    [Header("调试按键（与正式输入分离）")]
    public KeyCode toggleAllMoveRangeKey = KeyCode.F1;
    public KeyCode toggleAllAttackRangeKey = KeyCode.F2;
    public KeyCode addMarigoldFundsKey = KeyCode.F3;
    public KeyCode addLancelFundsKey = KeyCode.F4;
    public KeyCode oneKeyVictoryKey = KeyCode.F5;
    public KeyCode oneKeyDefeatKey = KeyCode.F6;

    [Header("依赖（可留空自动查找）")]
    public HighlightManager highlightManager;
    public VictoryUIController victoryUi;

    private bool _isMoveRangeDebugOn;
    private bool _isAttackRangeDebugOn;

    private void Awake()
    {
        if (highlightManager == null)
            highlightManager = HighlightManager.Instance;
        if (victoryUi == null)
        {
#if UNITY_2023_1_OR_NEWER
            victoryUi = FindFirstObjectByType<VictoryUIController>(FindObjectsInactive.Include);
#else
            victoryUi = FindObjectOfType<VictoryUIController>();
#endif
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleAllMoveRangeKey))
            ToggleAllMoveRanges();

        if (Input.GetKeyDown(toggleAllAttackRangeKey))
            ToggleAllAttackRanges();

        if (Input.GetKeyDown(addMarigoldFundsKey))
            AddFunds(UnitFaction.Marigold, 10000);

        if (Input.GetKeyDown(addLancelFundsKey))
            AddFunds(UnitFaction.Lancel, 10000);

        if (Input.GetKeyDown(oneKeyVictoryKey))
            TriggerOneKeyVictory();

        if (Input.GetKeyDown(oneKeyDefeatKey))
            TriggerOneKeyDefeat();
    }

    private void TriggerOneKeyVictory()
    {
        if (victoryUi == null)
            return;
        victoryUi.PlayVictory();
    }

    private void TriggerOneKeyDefeat()
    {
        if (victoryUi == null)
            return;
        victoryUi.PlayDefeat();
    }

    private void ToggleAllMoveRanges()
    {
        if (highlightManager == null) return;

        if (_isMoveRangeDebugOn)
        {
            highlightManager.ClearMoveHighlights();
            _isMoveRangeDebugOn = false;
            return;
        }

        // 避免两种调试高光叠加造成阅读困难：开启一种时关闭另一种。
        if (_isAttackRangeDebugOn)
        {
            highlightManager.ClearAttackHighlights();
            _isAttackRangeDebugOn = false;
        }

        highlightManager.ClearMoveHighlights();
#if UNITY_2023_1_OR_NEWER
        var units = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
#else
        var units = FindObjectsOfType<UnitController>();
#endif
        foreach (var unit in units)
        {
            if (unit == null) continue;
            highlightManager.ShowMoveRangeHighlights(unit);
        }

        _isMoveRangeDebugOn = true;
    }

    private void ToggleAllAttackRanges()
    {
        if (highlightManager == null) return;

        if (_isAttackRangeDebugOn)
        {
            highlightManager.ClearAttackHighlights();
            _isAttackRangeDebugOn = false;
            return;
        }

        // 避免两种调试高光叠加造成阅读困难：开启一种时关闭另一种。
        if (_isMoveRangeDebugOn)
        {
            highlightManager.ClearMoveHighlights();
            _isMoveRangeDebugOn = false;
        }

        highlightManager.ClearAttackHighlights();
#if UNITY_2023_1_OR_NEWER
        var units = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
#else
        var units = FindObjectsOfType<UnitController>();
#endif
        var isFirst = true;
        foreach (var unit in units)
        {
            if (unit == null) continue;
            highlightManager.ShowAttackRangeHighlights(unit, clearExisting: isFirst);
            isFirst = false;
        }

        _isAttackRangeDebugOn = true;
    }

    private static void AddFunds(UnitFaction faction, int amount)
    {
        if (faction == UnitFaction.None || amount <= 0)
            return;

        FactionFundsLedger.Instance.AddFunds(faction, amount);
    }
}
