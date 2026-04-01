using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 缓慢打字（打字机）效果的可复用工具。
/// 原理：在协程中按固定时间间隔（charInterval）增加显示字符数，用 Substring(0, shownLength) 更新文本，
/// 每显示一个字符时触发 <see cref="OnCharacterTyped"/>，供音效等模块订阅。
/// </summary>
public static class TypewriterUtility
{
    /// <summary>
    /// 每打出一个字符时触发（可被音效管理器等订阅）。
    /// 参数：当前显示的字符索引（0-based）、本帧打出的字符。
    /// </summary>
    public static event Action<int, char> OnCharacterTyped;

    /// <summary>
    /// 由非协程的逐字逻辑（如 DayInfoPanel 的按时间比例显示）在每显示一个新字符时调用，以统一触发打字事件与音效。
    /// </summary>
    public static void NotifyCharacterTyped(int index, char c)
    {
        OnCharacterTyped?.Invoke(index, c);
    }

    /// <summary>
    /// 使用协程在指定文本组件上以“缓慢打字”方式显示内容。
    /// </summary>
    /// <param name="runner">用于运行协程的 MonoBehaviour（通常为当前 UI 控制器）。</param>
    /// <param name="target">要更新的 TMP_Text。</param>
    /// <param name="content">要显示的全部文本。</param>
    /// <param name="charInterval">每个字符之间的时间间隔（秒）；≤0 则立即显示全文。</param>
    /// <param name="useUnscaledTime">是否使用 Time.unscaledDeltaTime（用于暂停时仍能打字）。</param>
    /// <returns>协程的 Coroutine，可用于 StopCoroutine；若未启动则返回 null。</returns>
    public static Coroutine RunTypewriter(
        MonoBehaviour runner,
        TMP_Text target,
        string content,
        float charInterval,
        bool useUnscaledTime = true)
    {
        if (runner == null || target == null)
            return null;

        return runner.StartCoroutine(TypewriterCoroutine(target, content ?? string.Empty, charInterval, useUnscaledTime));
    }

    private static IEnumerator TypewriterCoroutine(TMP_Text target, string content, float charInterval, bool useUnscaledTime)
    {
        target.text = string.Empty;
        if (content.Length == 0)
            yield break;

        if (charInterval <= 0f)
        {
            target.text = content;
            yield break;
        }

        var elapsed = 0f;
        var shownLength = 0;
        var deltaTime = useUnscaledTime ? () => Time.unscaledDeltaTime : (Func<float>)(() => Time.deltaTime);

        while (shownLength < content.Length)
        {
            elapsed += deltaTime();
            while (elapsed >= charInterval && shownLength < content.Length)
            {
                elapsed -= charInterval;
                shownLength++;
                target.text = content.Substring(0, shownLength);
                NotifyCharacterTyped(shownLength - 1, content[shownLength - 1]);
            }

            yield return null;
        }
    }
}
