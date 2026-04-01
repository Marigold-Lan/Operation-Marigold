using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitOptionItem : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private Image _selectedImage;

    public RectTransform RectTransform { get; private set; }
    public UnitData Data { get; private set; }

    private void Awake()
    {
        RectTransform = transform as RectTransform;

        if (_iconImage == null)
            _iconImage = transform.Find("Icon")?.GetComponent<Image>();
        if (_nameText == null)
            _nameText = transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (_priceText == null)
            _priceText = transform.Find("PriceText")?.GetComponent<TMP_Text>();

        if (_selectedImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] == _iconImage)
                    continue;

                var lower = images[i].name.ToLowerInvariant();
                if (lower.Contains("select") || lower.Contains("cursor") || lower.Contains("highlight"))
                {
                    _selectedImage = images[i];
                    break;
                }
            }
        }
    }

    public void Bind(UnitData unitData, Sprite movementIcon)
    {
        Data = unitData;

        if (_iconImage != null)
            _iconImage.sprite = movementIcon;

        if (_nameText != null)
            _nameText.text = unitData != null ? unitData.displayName : string.Empty;

        if (_priceText != null)
            _priceText.text = unitData != null ? unitData.cost.ToString() : string.Empty;

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (_selectedImage == null)
            return;

        // 若引用误配到本物体根节点的 Image，不能 SetActive，否则会把整条选项隐藏。
        if (_selectedImage.transform == transform)
        {
            if (_selectedImage.enabled != selected)
                _selectedImage.enabled = selected;
            return;
        }

        if (_selectedImage.gameObject.activeSelf != selected)
            _selectedImage.gameObject.SetActive(selected);
    }
}
