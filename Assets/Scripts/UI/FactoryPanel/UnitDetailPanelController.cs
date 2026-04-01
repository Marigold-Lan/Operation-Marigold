using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitDetailPanelController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Header")]
    [SerializeField] private TMP_Text _unitNameText;
    [SerializeField] private Image _headerIcon;

    [Header("Stats")]
    [SerializeField] private Image _moveStatIcon;
    [SerializeField] private TMP_Text _moveStatText;
    [SerializeField] private TMP_Text _visionStatText;
    [SerializeField] private TMP_Text _fuelStatText;

    [Header("Description")]
    [SerializeField] private TMP_Text _descriptionText;

    [Header("Weapon Areas")]
    [SerializeField] private WeaponAreaRefs _weapon1;
    [SerializeField] private WeaponAreaRefs _weapon2;

    [Header("Category Icons")]
    [SerializeField] private Sprite _vehicleIcon;
    [SerializeField] private Sprite _soldierIcon;
    [SerializeField] private GameObject _targetIconPrefab;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    [ContextMenu("Auto Bind Hierarchy References")]
    public void AutoBindHierarchyReferences()
    {
        ResolveMissingReferences(forceRebind: true);
    }

    public void Show(UnitData data)
    {
        ResolveMissingReferences();
        if (data == null)
        {
            Hide();
            return;
        }

        SetActive(_panelRoot != null ? _panelRoot : gameObject, true);

        SetText(_unitNameText, string.IsNullOrWhiteSpace(data.displayName) ? "-" : data.displayName);
        SetImageSprite(_headerIcon, GetCategorySprite(data.category));

        // 移动力数值与图标
        SetImageSprite(_moveStatIcon, GlobalConfigManager.GetMovementIcon(data.movementType));
        SetText(_moveStatText, data.movementRange.ToString());
        SetText(_visionStatText, data.visionRange.ToString());
        SetText(_fuelStatText, data.maxFuel.ToString());
        SetText(_descriptionText, string.IsNullOrWhiteSpace(data.Description) ? string.Empty : data.Description);

        BindWeaponArea(_weapon1, data.HasPrimaryWeapon, data.primaryWeapon, data.primaryAmmoCapacity.ToString());
        BindWeaponArea(_weapon2, data.HasSecondaryWeapon, data.secondaryWeapon, "∞");
    }

    public void Hide()
    {
        ClearWeaponTargets(_weapon1);
        ClearWeaponTargets(_weapon2);
        SetActive(_panelRoot != null ? _panelRoot : gameObject, false);
    }

    private void BindWeaponArea(WeaponAreaRefs area, bool enabled, UnitWeaponData weapon, string ammoText)
    {
        if (area == null)
            return;

        if (area.Root != null)
            area.Root.SetActive(true);

        var usable = enabled && weapon != null;
        if (!usable)
        {
            SetText(area.WeaponNameText, "None");
            SetText(area.AmmoCountText, string.Empty);
            SetText(area.RangeCountText, string.Empty);
            SetActive(area.AmmoLabelText != null ? area.AmmoLabelText.gameObject : null, false);
            SetActive(area.RangeLabelText != null ? area.RangeLabelText.gameObject : null, false);
            ClearWeaponTargets(area);
            return;
        }

        SetActive(area.AmmoLabelText != null ? area.AmmoLabelText.gameObject : null, true);
        SetActive(area.RangeLabelText != null ? area.RangeLabelText.gameObject : null, true);
        SetText(area.WeaponNameText, string.IsNullOrWhiteSpace(weapon.weaponName) ? "-" : weapon.weaponName);
        SetText(area.AmmoCountText, ammoText);
        SetText(area.RangeCountText, FormatRange(weapon.attackRangeMin, weapon.attackRangeMax));
        RebuildTargetIcons(area.TargetIconsContainer, weapon.canAttackSoldier, weapon.canAttackVehicle);
    }

    private void RebuildTargetIcons(Transform container, bool canAttackSoldier, bool canAttackVehicle)
    {
        if (container == null)
            return;

        var template = GetTargetIconTemplate(container);
        if (template == null)
            return;

        ClearGeneratedTargetIcons(container, template);
        if (template.activeSelf)
            template.SetActive(false);

        if (canAttackSoldier)
            SpawnTargetIcon(container, template, _soldierIcon);
        if (canAttackVehicle)
            SpawnTargetIcon(container, template, _vehicleIcon);
    }

    private void SpawnTargetIcon(Transform container, GameObject template, Sprite sprite)
    {
        if (container == null || template == null)
            return;

        var go = Instantiate(template, container);
        go.SetActive(true);
        var image = go.GetComponent<Image>();
        if (image == null)
            image = go.GetComponentInChildren<Image>(true);
        SetImageSprite(image, sprite);
    }

    private void ClearWeaponTargets(WeaponAreaRefs area)
    {
        if (area == null || area.TargetIconsContainer == null)
            return;

        var container = area.TargetIconsContainer;
        var template = GetTargetIconTemplate(container);
        ClearGeneratedTargetIcons(container, template);
        if (template != null && template.activeSelf)
            template.SetActive(false);
    }

    private void ResolveMissingReferences(bool forceRebind = false)
    {
        if (forceRebind)
        {
            _unitNameText = null;
            _headerIcon = null;
            _moveStatText = null;
            _visionStatText = null;
            _fuelStatText = null;
            _descriptionText = null;
        }

        if (_panelRoot == null)
            _panelRoot = gameObject;

        var root = _panelRoot != null ? _panelRoot.transform : transform;
        if (_unitNameText == null)
            _unitNameText = root.Find("Header/UnitNameText")?.GetComponent<TMP_Text>();
        if (_headerIcon == null)
            _headerIcon = root.Find("Header/Headericon")?.GetComponent<Image>() ?? root.Find("Header/HeaderIcon")?.GetComponent<Image>();
        if (_moveStatText == null)
            _moveStatText = root.Find("StatsContainer/MoveStatRow/statText")?.GetComponent<TMP_Text>();
        if (_visionStatText == null)
            _visionStatText = root.Find("StatsContainer/VisionStatRow/statText")?.GetComponent<TMP_Text>();
        if (_fuelStatText == null)
            _fuelStatText = root.Find("StatsContainer/FuelStatRow/statText")?.GetComponent<TMP_Text>();
        if (_descriptionText == null)
            _descriptionText = root.Find("DescriptionArea")?.GetComponent<TMP_Text>();

        _weapon1 ??= new WeaponAreaRefs();
        _weapon2 ??= new WeaponAreaRefs();
        _weapon1.ResolveMissing(root, "Weapon1_area", forceRebind);
        _weapon2.ResolveMissing(root, "Weapon2_area", forceRebind);

        if (_targetIconPrefab == null)
        {
            _targetIconPrefab = TryResolveTargetIconPrefab(_weapon1) ?? TryResolveTargetIconPrefab(_weapon2);
        }
    }

    private Sprite GetCategorySprite(UnitCategory category)
    {
        return category == UnitCategory.Soldier ? _soldierIcon : _vehicleIcon;
    }

    private static string FormatRange(int min, int max)
    {
        var fixedMin = Mathf.Max(1, min);
        var fixedMax = Mathf.Max(fixedMin, max);
        return $"{fixedMin}~{fixedMax}";
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private GameObject GetTargetIconTemplate(Transform container)
    {
        if (_targetIconPrefab != null)
            return _targetIconPrefab;

        if (container == null)
            return null;

        var named = container.Find("TargetIcon");
        if (named != null)
            return named.gameObject;

        return container.childCount > 0 ? container.GetChild(0).gameObject : null;
    }

    private static void ClearGeneratedTargetIcons(Transform container, GameObject template)
    {
        if (container == null)
            return;

        for (var i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i);
            if (template != null && child == template.transform)
                continue;
            Destroy(child.gameObject);
        }
    }

    private static GameObject TryResolveTargetIconPrefab(WeaponAreaRefs area)
    {
        var container = area != null ? area.TargetIconsContainer : null;
        if (container == null)
            return null;

        var named = container.Find("TargetIcon");
        if (named != null)
            return named.gameObject;

        return container.childCount > 0 ? container.GetChild(0).gameObject : null;
    }
}

[System.Serializable]
public class WeaponAreaRefs
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _weaponNameText;
    [SerializeField] private TMP_Text _ammoLabelText;
    [SerializeField] private TMP_Text _ammoCountText;
    [SerializeField] private TMP_Text _rangeLabelText;
    [SerializeField] private TMP_Text _rangeCountText;
    [SerializeField] private Transform _targetIconsContainer;

    public GameObject Root => _root;
    public TMP_Text WeaponNameText => _weaponNameText;
    public TMP_Text AmmoLabelText => _ammoLabelText;
    public TMP_Text AmmoCountText => _ammoCountText;
    public TMP_Text RangeLabelText => _rangeLabelText;
    public TMP_Text RangeCountText => _rangeCountText;
    public Transform TargetIconsContainer => _targetIconsContainer;

    public void ResolveMissing(Transform panelRoot, string areaPath, bool forceRebind = false)
    {
        if (panelRoot == null || string.IsNullOrWhiteSpace(areaPath))
            return;

        var area = panelRoot.Find(areaPath);
        if (forceRebind)
        {
            _root = null;
            _weaponNameText = null;
            _ammoLabelText = null;
            _ammoCountText = null;
            _rangeLabelText = null;
            _rangeCountText = null;
            _targetIconsContainer = null;
        }
        if (_root == null)
            _root = area != null ? area.gameObject : null;
        if (area == null)
            return;

        if (_weaponNameText == null)
            _weaponNameText =
                area.Find("WeaponHeader/Weapon1NameText")?.GetComponent<TMP_Text>() ??
                area.Find("WeaponHeader/Weapon2NameText")?.GetComponent<TMP_Text>() ??
                area.Find("WeaponHeader/WeaponNameText")?.GetComponent<TMP_Text>();
        if (_ammoCountText == null)
            _ammoCountText = area.Find("WeaponHeader/AmmoCountText")?.GetComponent<TMP_Text>();
        if (_ammoLabelText == null)
            _ammoLabelText = area.Find("WeaponHeader/AmmoText")?.GetComponent<TMP_Text>();
        if (_rangeCountText == null)
            _rangeCountText =
                area.Find("WeaponHeader/RangeCountText")?.GetComponent<TMP_Text>() ??
                area.Find("WeaponHeader/RangeText")?.GetComponent<TMP_Text>();
        if (_rangeLabelText == null)
            _rangeLabelText = area.Find("WeaponHeader/RangeText")?.GetComponent<TMP_Text>();
        if (_targetIconsContainer == null)
            _targetIconsContainer = area.Find("TargetIconsContainer");
    }
}
