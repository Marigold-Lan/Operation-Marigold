using UnityEngine;

/// <summary>
/// 补给能力实现。为周围单位补充燃料和弹药。
/// </summary>
public class UnitSupply : MonoBehaviour, ISupplier
{
    public static event System.Action<UnitController, UnitController, bool> OnSupplyPerformed;

    [SerializeField] private int _supplyRange = 1;

    public int SupplyRange => _supplyRange;

    public bool Supply(UnitController target)
    {
        if (target == null) return false;

        var controller = GetComponent<UnitController>();
        if (controller == null) return false;

        var d = controller.GridCoord - target.GridCoord;
        var dist = Mathf.Abs(d.x) + Mathf.Abs(d.y);
        if (dist > _supplyRange) return false;

        if (controller.OwnerFaction != target.OwnerFaction) return false;

        var ok = UnitResupplyRules.RefillFuelAndPrimaryAmmo(target);
        OnSupplyPerformed?.Invoke(controller, target, ok);
        return ok;
    }
}
