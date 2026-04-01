using System.Collections;
using UnityEngine;

/// <summary>
/// 单位动作表现组件：提供可复用的上下跳动能力（攻击/占领等）。
/// </summary>
public class UnitActionMotion : MonoBehaviour
{
    [System.Serializable]
    public struct BounceConfig
    {
        [Min(0f)] public float duration;
        [Min(0f)] public float height;
        [Min(1)] public int count;
        [Min(0f)] public float postPause;
    }

    public enum BouncePreset
    {
        Attack = 0,
        Capture = 1
    }

    [Header("Attack Bounce")]
    [SerializeField] private BounceConfig _attackBounce = new BounceConfig
    {
        duration = 0.3f,
        height = 0.26f,
        count = 2,
        postPause = 0.09f
    };

    [Header("Capture Bounce")]
    [SerializeField] private BounceConfig _captureBounce = new BounceConfig
    {
        duration = 0.3f,
        height = 0.22f,
        count = 2,
        postPause = 0f
    };

    public IEnumerator PlayBounce(BouncePreset preset)
    {
        var config = preset == BouncePreset.Capture ? _captureBounce : _attackBounce;
        yield return PlayBounce(config);
    }

    public IEnumerator PlayBounce(BounceConfig config)
    {
        var bounceCount = Mathf.Max(1, config.count);
        if (config.duration <= 0f || config.height <= 0f)
            yield break;

        var basePos = transform.position;
        var singleBounceDuration = config.duration / bounceCount;

        for (var bounceIndex = 0; bounceIndex < bounceCount; bounceIndex++)
        {
            var elapsed = 0f;
            while (elapsed < singleBounceDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / singleBounceDuration);
                var yOffset = Mathf.Sin(t * Mathf.PI) * config.height;
                transform.position = basePos + Vector3.up * yOffset;
                yield return null;
            }

            transform.position = basePos;
        }

        transform.position = basePos;
        if (config.postPause > 0f)
            yield return new WaitForSeconds(config.postPause);
    }
}
