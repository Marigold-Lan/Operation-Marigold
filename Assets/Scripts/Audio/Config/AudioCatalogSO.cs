using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = OperationMarigoldPaths.SoAudioCatalog, fileName = "AudioCatalog")]
public class AudioCatalogSO : ScriptableObject
{
    [System.Serializable]
    public class AudioCueDefaults
    {
        [Header("UI Defaults")]
        public Vector2 uiVolumeRange = new Vector2(0.9f, 1f);
        public Vector2 uiPitchRange = new Vector2(1f, 1f);
        [Min(0f)] public float uiCooldownSec = 0.02f;
        [Min(1)] public int uiMaxVoices = 2;
        [Range(0f, 1f)] public float uiSpatialBlend = 0f;
        public bool uiLoop = false;
        [Range(0, 256)] public int uiPriority = 80;

        [Header("SFX Defaults")]
        public Vector2 sfxVolumeRange = new Vector2(0.9f, 1f);
        public Vector2 sfxPitchRange = new Vector2(0.97f, 1.03f);
        [Min(0f)] public float sfxCooldownSec = 0.03f;
        [Min(1)] public int sfxMaxVoices = 4;
        [Range(0f, 1f)] public float sfxSpatialBlend = 0.25f;
        public bool sfxLoop = false;
        [Range(0, 256)] public int sfxPriority = 96;

        [Header("BGM Defaults")]
        public Vector2 bgmVolumeRange = new Vector2(0.8f, 0.8f);
        public Vector2 bgmPitchRange = new Vector2(1f, 1f);
        [Min(0f)] public float bgmCooldownSec = 0f;
        [Min(1)] public int bgmMaxVoices = 1;
        [Range(0f, 1f)] public float bgmSpatialBlend = 0f;
        public bool bgmLoop = true;
        [Range(0, 256)] public int bgmPriority = 32;
    }

    [System.Serializable]
    public struct AudioCueEntry
    {
        public AudioCueId cueId;
        public AudioCue cue;
    }

    [SerializeField] private AudioCueDefaults _defaults = new AudioCueDefaults();
    [SerializeField] private List<AudioCueEntry> _entries = new List<AudioCueEntry>();

    private Dictionary<AudioCueId, AudioCue> _cache;

    public bool TryGetCue(AudioCueId cueId, out AudioCue cue)
    {
        EnsureCacheBuilt();
        return _cache.TryGetValue(cueId, out cue) && cue != null;
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        EnsureSerializedDataValid();
        RebuildCache();
    }

    [ContextMenu("Audio Catalog/补齐所有 AudioCueId 条目")]
    public void EnsureAllCueIdEntries()
    {
        if (_entries == null)
            _entries = new List<AudioCueEntry>();

        var existing = new HashSet<AudioCueId>();
        for (var i = 0; i < _entries.Count; i++)
        {
            var cueId = _entries[i].cueId;
            if (cueId == AudioCueId.None)
                continue;
            existing.Add(cueId);
        }

        var ids = (AudioCueId[])System.Enum.GetValues(typeof(AudioCueId));
        for (var i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (id == AudioCueId.None || existing.Contains(id))
                continue;

            _entries.Add(new AudioCueEntry
            {
                cueId = id,
                cue = new AudioCue()
            });
        }

        _entries.Sort((a, b) => ((int)a.cueId).CompareTo((int)b.cueId));
        RebuildCache();
    }

    [ContextMenu("Audio Catalog/应用默认参数到未配置条目（有则跳过）")]
    public void ApplyDefaultParamsToAllEntries()
    {
        ApplyDefaultParamsToEntries(onlyIfUnconfigured: true);
    }

    public void ApplyDefaultParamsToEntries(bool onlyIfUnconfigured)
    {
        if (_entries == null)
            return;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.cueId == AudioCueId.None)
                continue;

            if (entry.cue == null)
                entry.cue = new AudioCue();

            if (onlyIfUnconfigured && IsCueConfigured(entry.cue))
            {
                _entries[i] = entry;
                continue;
            }

            ApplyDefaultsToCue(entry.cueId, entry.cue);
            _entries[i] = entry;
        }

        RebuildCache();
    }

    public void EnsureAndApplyDefaults()
    {
        EnsureAllCueIdEntries();
        ApplyDefaultParamsToEntries(onlyIfUnconfigured: true);
    }

    private void EnsureCacheBuilt()
    {
        if (_cache == null)
            RebuildCache();
    }

    private void RebuildCache()
    {
        if (_cache == null)
            _cache = new Dictionary<AudioCueId, AudioCue>();
        else
            _cache.Clear();

        if (_entries == null)
            return;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.cueId == AudioCueId.None || entry.cue == null)
                continue;

            if (_cache.ContainsKey(entry.cueId))
                continue;

            _cache.Add(entry.cueId, entry.cue);
        }
    }

    private void EnsureSerializedDataValid()
    {
        if (_defaults == null)
            _defaults = new AudioCueDefaults();
        if (_entries == null)
            _entries = new List<AudioCueEntry>();
    }

    private void ApplyDefaultsToCue(AudioCueId cueId, AudioCue cue)
    {
        if (cue == null || _defaults == null)
            return;

        switch (GetCueKind(cueId))
        {
            case CueKind.Ui:
                cue.volumeRange = NormalizeRange(_defaults.uiVolumeRange);
                cue.pitchRange = NormalizeRange(_defaults.uiPitchRange);
                cue.cooldownSec = Mathf.Max(0f, _defaults.uiCooldownSec);
                cue.maxVoices = Mathf.Max(1, _defaults.uiMaxVoices);
                cue.spatialBlend = Mathf.Clamp01(_defaults.uiSpatialBlend);
                cue.loop = _defaults.uiLoop;
                cue.priority = Mathf.Clamp(_defaults.uiPriority, 0, 256);
                break;
            case CueKind.Bgm:
                cue.volumeRange = NormalizeRange(_defaults.bgmVolumeRange);
                cue.pitchRange = NormalizeRange(_defaults.bgmPitchRange);
                cue.cooldownSec = Mathf.Max(0f, _defaults.bgmCooldownSec);
                cue.maxVoices = Mathf.Max(1, _defaults.bgmMaxVoices);
                cue.spatialBlend = Mathf.Clamp01(_defaults.bgmSpatialBlend);
                cue.loop = _defaults.bgmLoop;
                cue.priority = Mathf.Clamp(_defaults.bgmPriority, 0, 256);
                break;
            default:
                cue.volumeRange = NormalizeRange(_defaults.sfxVolumeRange);
                cue.pitchRange = NormalizeRange(_defaults.sfxPitchRange);
                cue.cooldownSec = Mathf.Max(0f, _defaults.sfxCooldownSec);
                cue.maxVoices = Mathf.Max(1, _defaults.sfxMaxVoices);
                cue.spatialBlend = Mathf.Clamp01(_defaults.sfxSpatialBlend);
                cue.loop = _defaults.sfxLoop;
                cue.priority = Mathf.Clamp(_defaults.sfxPriority, 0, 256);
                break;
        }
    }

    private static bool IsCueConfigured(AudioCue cue)
    {
        if (cue == null)
            return false;

        if (cue.HasValidClip())
            return true;
        if (cue.mixerGroup != null)
            return true;
        return false;
    }

    private static Vector2 NormalizeRange(Vector2 range)
    {
        return range.x <= range.y ? range : new Vector2(range.y, range.x);
    }

    private static CueKind GetCueKind(AudioCueId cueId)
    {
        var name = cueId.ToString();
        if (name.StartsWith("Ui", System.StringComparison.Ordinal))
            return CueKind.Ui;
        if (name.StartsWith("Bgm", System.StringComparison.Ordinal))
            return CueKind.Bgm;
        return CueKind.Sfx;
    }

    private enum CueKind
    {
        Ui,
        Sfx,
        Bgm
    }
}
