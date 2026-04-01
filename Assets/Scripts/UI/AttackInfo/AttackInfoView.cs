using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AttackInfoView : MonoBehaviour
{
    [Header("Hierarchy refs")]
    [SerializeField] private RectTransform _background;
    [SerializeField] private Image _icon;
    [SerializeField] private TMPro.TMP_Text _hpChangeText;

    private readonly List<Transform> _childCache = new List<Transform>(16);

    public void SetContent(Sprite icon, int hpBefore, int hpAfter)
    {
        if (_icon != null) _icon.sprite = icon;
        if (_hpChangeText != null) _hpChangeText.text = FormatHpChange(hpBefore, hpAfter);
    }

    public void SetHpChange(int hpBefore, int hpAfter)
    {
        if (_hpChangeText != null) _hpChangeText.text = FormatHpChange(hpBefore, hpAfter);
    }

    public void ApplyMirrorForLeftSide(bool isLeftSide)
    {
        // Requirement: left side UI needs mirroring.
        var rootY = isLeftSide ? 180f : 0f;
        transform.localRotation = Quaternion.Euler(0f, rootY, 0f);

        CacheDirectChildren();
        for (var i = 0; i < _childCache.Count; i++)
        {
            var t = _childCache[i];
            if (t == null) continue;
            if (_background != null && t == _background.transform) continue;
            t.localRotation = Quaternion.Euler(0f, rootY, 0f);
        }
    }

    private void CacheDirectChildren()
    {
        _childCache.Clear();
        var count = transform.childCount;
        for (var i = 0; i < count; i++)
            _childCache.Add(transform.GetChild(i));
    }

    private static string FormatHpChange(int hpBefore, int hpAfter)
    {
        return $"HP: {hpBefore} -> {hpAfter}";
    }
}

