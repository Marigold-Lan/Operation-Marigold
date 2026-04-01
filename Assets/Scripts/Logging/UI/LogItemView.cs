using OperationMarigold.Logging.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OperationMarigold.Logging.UI
{
    public sealed class LogItemView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _message;

        public void SetFont(TMP_FontAsset font)
        {
            if (font == null)
                return;
            EnsureRefs();
            if (_message != null)
                _message.font = font;
        }

        public void Bind(LogEntry entry, Sprite icon)
        {
            EnsureRefs();

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_message != null)
                _message.text = entry.Message ?? string.Empty;
        }

        private void EnsureRefs()
        {
            if (_icon == null)
                _icon = transform.Find("icon")?.GetComponent<Image>();
            if (_message == null)
                _message = transform.Find("message")?.GetComponent<TMP_Text>();
        }
    }
}

