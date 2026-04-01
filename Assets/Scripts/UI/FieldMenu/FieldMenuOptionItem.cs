using UnityEngine;
using UnityEngine.UI;

public class FieldMenuOptionItem : MonoBehaviour
{
    [SerializeField] private Image _selectedImage;
    public RectTransform RectTransform { get; private set; }

    private void Awake()
    {
        RectTransform = transform as RectTransform;
        if (_selectedImage == null)
            _selectedImage = ResolveSelectedImage();
    }

    public void SetSelected(bool selected)
    {
        if (_selectedImage == null)
            return;

        if (_selectedImage.transform == transform)
        {
            if (_selectedImage.enabled != selected)
                _selectedImage.enabled = selected;
            return;
        }

        if (_selectedImage.gameObject.activeSelf != selected)
            _selectedImage.gameObject.SetActive(selected);
    }

    private Image ResolveSelectedImage()
    {
        var images = GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            var lower = images[i].name.ToLowerInvariant();
            if (lower.Contains("select") ||
                lower.Contains("cursor") ||
                lower.Contains("highlight") ||
                lower.Contains("frame") ||
                lower.Contains("border"))
            {
                return images[i];
            }
        }

        return null;
    }
}
