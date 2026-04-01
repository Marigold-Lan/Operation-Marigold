using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CommandOptionItem : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _labelText;

    public RectTransform RectTransform { get; private set; }
    public bool IsInteractable { get; private set; }
    private Color _normalImageColor = Color.white;

    private void Awake()
    {
        RectTransform = transform as RectTransform;

        if (_labelText == null)
            _labelText = GetComponentInChildren<TMP_Text>(true);

        if (_iconImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            Image namedIcon = null;
            Image fallbackIcon = null;
            for (var i = 0; i < images.Length; i++)
            {
                fallbackIcon = images[i];
                var lower = images[i].name.ToLowerInvariant();
                if (lower.Contains("icon"))
                {
                    namedIcon = images[i];
                    break;
                }
            }

            _iconImage = namedIcon != null ? namedIcon : fallbackIcon;
        }

        if (_iconImage != null)
            _normalImageColor = _iconImage.color;
    }

    public void Bind(CommandOption option, bool selected)
    {
        if (_labelText != null)
            _labelText.text = option != null ? option.Label : string.Empty;

        if (_iconImage != null)
        {
            if (option != null && option.Icon != null)
                _iconImage.sprite = option.Icon;
        }

        IsInteractable = option != null && option.Interactable;
        RefreshVisual(selected);
    }

    public void RefreshVisual(bool selected)
    {
        if (_iconImage == null) return;
        _iconImage.color = selected && IsInteractable ? Color.red : _normalImageColor;
    }
}
