using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class AudioCue
{
    [Tooltip("候选音频片段，播放时会随机选择一个。")]
    public AudioClip[] clips;

    [Tooltip("音量随机范围。")]
    public Vector2 volumeRange = new Vector2(1f, 1f);

    [Tooltip("音高随机范围。")]
    public Vector2 pitchRange = new Vector2(1f, 1f);

    [Tooltip("同一 Cue 冷却秒数，防止高频事件刷爆音。")]
    [Min(0f)] public float cooldownSec = 0f;

    [Tooltip("同一 Cue 最大并发播放数。")]
    [Min(1)] public int maxVoices = 4;

    [Tooltip("输出混音组。")]
    public AudioMixerGroup mixerGroup;

    [Tooltip("空间化程度：0 为 2D，1 为 3D。")]
    [Range(0f, 1f)] public float spatialBlend = 0f;

    [Tooltip("是否循环（主要用于 BGM）。")]
    public bool loop = false;

    [Tooltip("AudioSource 优先级，数值越小优先级越高。")]
    [Range(0, 256)] public int priority = 128;

    public bool HasValidClip()
    {
        if (clips == null || clips.Length == 0)
            return false;

        for (var i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    public AudioClip PickRandomClip()
    {
        if (clips == null || clips.Length == 0)
            return null;

        var attempts = clips.Length;
        while (attempts-- > 0)
        {
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
                return clip;
        }

        return null;
    }

    public float PickRandomVolume()
    {
        return Mathf.Clamp(Random.Range(volumeRange.x, volumeRange.y), 0f, 1f);
    }

    public float PickRandomPitch()
    {
        return Mathf.Clamp(Random.Range(pitchRange.x, pitchRange.y), -3f, 3f);
    }
}
