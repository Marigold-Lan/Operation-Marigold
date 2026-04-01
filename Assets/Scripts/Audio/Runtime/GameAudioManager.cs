using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class GameAudioManager : Singleton<GameAudioManager>, IGameAudioService
{
    private const float MinDb = -80f;

    [Header("Config")]
    [SerializeField] private AudioCatalogSO _catalog;
    [SerializeField] private bool _dontDestroyOnLoad = true;

    [Header("Mixer")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _masterVolumeParam = "MasterVolume";
    [SerializeField] private string _bgmVolumeParam = "BgmVolume";
    [SerializeField] private string _sfxVolumeParam = "SfxVolume";
    [SerializeField] private string _uiVolumeParam = "UiVolume";

    [Header("Channels")]
    [SerializeField] private int _sfxPoolSize = 12;
    [SerializeField] private AudioSource _uiSource;
    [SerializeField] private AudioSource _bgmSourceA;
    [SerializeField] private AudioSource _bgmSourceB;

    private readonly List<AudioSource> _sfxSources = new List<AudioSource>();
    private readonly Dictionary<AudioBusType, float> _busVolumes = new Dictionary<AudioBusType, float>();
    private readonly HashSet<AudioCueId> _missingCueWarned = new HashSet<AudioCueId>();
    private bool _missingCatalogWarned;

    private Coroutine _bgmRoutine;
    private AudioSource _activeBgmSource;
    private AudioCueId _activeBgmCueId = AudioCueId.None;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        if (_dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        EnsureChannels();
        ApplySavedBusVolumes();
    }

    public bool PlayUi(AudioCueId cueId)
    {
        if (!TryGetCue(cueId, out var cue))
            return false;

        if (_uiSource == null)
            _uiSource = CreateChildSource("UI_Source");

        var clip = cue.PickRandomClip();
        if (clip == null)
            return false;

        ConfigureSource(_uiSource, cue);
        _uiSource.loop = false;
        _uiSource.pitch = cue.PickRandomPitch();
        _uiSource.PlayOneShot(clip, cue.PickRandomVolume());
        return true;
    }

    public bool PlaySfx(AudioCueId cueId, Vector3 worldPos, float volumeScale = 1f)
    {
        if (!TryGetCue(cueId, out var cue))
            return false;

        var clip = cue.PickRandomClip();
        if (clip == null)
            return false;

        var source = AcquireSfxSource();
        if (source == null)
            return false;

        ConfigureSource(source, cue);
        source.transform.position = worldPos;
        source.pitch = cue.PickRandomPitch();
        source.PlayOneShot(clip, Mathf.Clamp01(cue.PickRandomVolume() * Mathf.Clamp01(volumeScale)));
        return true;
    }

    public bool PlayBgm(AudioCueId cueId, float fadeSeconds = 0.5f)
    {
        if (!TryGetCue(cueId, out var cue))
            return false;

        var clip = cue.PickRandomClip();
        if (clip == null)
            return false;

        if (_activeBgmCueId == cueId && _activeBgmSource != null && _activeBgmSource.isPlaying)
            return true;

        if (_bgmRoutine != null)
            StopCoroutine(_bgmRoutine);
        _bgmRoutine = StartCoroutine(CrossFadeToBgm(cueId, cue, clip, Mathf.Max(0f, fadeSeconds)));
        return true;
    }

    public void StopBgm(float fadeSeconds = 0.5f)
    {
        if (_bgmRoutine != null)
            StopCoroutine(_bgmRoutine);
        _bgmRoutine = StartCoroutine(FadeOutBgm(Mathf.Max(0f, fadeSeconds)));
    }

    public void SetBusVolume(AudioBusType busType, float linearVolume)
    {
        var volume = Mathf.Clamp01(linearVolume);
        _busVolumes[busType] = volume;

        if (_audioMixer != null)
        {
            var parameter = GetMixerParamName(busType);
            if (!string.IsNullOrEmpty(parameter))
                _audioMixer.SetFloat(parameter, LinearToDb(volume));
        }

        AudioSettingsStore.Save(busType, volume);
    }

    public float GetBusVolume(AudioBusType busType)
    {
        if (_busVolumes.TryGetValue(busType, out var volume))
            return volume;
        return 1f;
    }

    private void EnsureChannels()
    {
        if (_uiSource == null)
            _uiSource = CreateChildSource("UI_Source");

        if (_bgmSourceA == null)
            _bgmSourceA = CreateChildSource("BGM_Source_A");
        if (_bgmSourceB == null)
            _bgmSourceB = CreateChildSource("BGM_Source_B");

        _bgmSourceA.loop = true;
        _bgmSourceB.loop = true;
        _bgmSourceA.playOnAwake = false;
        _bgmSourceB.playOnAwake = false;
        _bgmSourceA.spatialBlend = 0f;
        _bgmSourceB.spatialBlend = 0f;

        _activeBgmSource = _bgmSourceA;

        while (_sfxSources.Count < Mathf.Max(1, _sfxPoolSize))
        {
            var source = CreateChildSource($"SFX_Source_{_sfxSources.Count + 1}");
            _sfxSources.Add(source);
        }
    }

    private AudioSource CreateChildSource(string sourceName)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = 1f;
        source.spatialBlend = 0f;
        return source;
    }

    private void ConfigureSource(AudioSource source, AudioCue cue)
    {
        if (source == null || cue == null)
            return;

        source.outputAudioMixerGroup = cue.mixerGroup;
        source.priority = cue.priority;
        source.spatialBlend = Mathf.Clamp01(cue.spatialBlend);
    }

    private bool TryGetCue(AudioCueId cueId, out AudioCue cue)
    {
        cue = null;
        if (_catalog == null)
        {
            if (!_missingCatalogWarned)
                _missingCatalogWarned = true;
            return false;
        }

        if (!_catalog.TryGetCue(cueId, out cue) || cue == null || !cue.HasValidClip())
        {
            _missingCueWarned.Add(cueId);
            return false;
        }

        return true;
    }

    private AudioSource AcquireSfxSource()
    {
        for (var i = 0; i < _sfxSources.Count; i++)
        {
            if (_sfxSources[i] != null && !_sfxSources[i].isPlaying)
                return _sfxSources[i];
        }

        // 池满时复用第一个，保证轻量实现下可持续播放。
        return _sfxSources.Count > 0 ? _sfxSources[0] : null;
    }

    private IEnumerator CrossFadeToBgm(AudioCueId cueId, AudioCue cue, AudioClip clip, float fadeSeconds)
    {
        var from = _activeBgmSource;
        var to = from == _bgmSourceA ? _bgmSourceB : _bgmSourceA;
        if (to == null)
            yield break;

        ConfigureSource(to, cue);
        to.loop = cue.loop;
        to.clip = clip;
        to.pitch = cue.PickRandomPitch();
        var targetVolume = cue.PickRandomVolume();
        to.volume = fadeSeconds <= 0f ? targetVolume : 0f;
        to.Play();

        if (fadeSeconds > 0f)
        {
            var fromStart = from != null ? from.volume : 0f;
            var elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeSeconds);
                to.volume = Mathf.Lerp(0f, targetVolume, t);
                if (from != null)
                    from.volume = Mathf.Lerp(fromStart, 0f, t);
                yield return null;
            }
        }

        to.volume = targetVolume;
        if (from != null)
        {
            from.Stop();
            from.volume = 0f;
        }

        _activeBgmSource = to;
        _activeBgmCueId = cueId;
        _bgmRoutine = null;
    }

    private IEnumerator FadeOutBgm(float fadeSeconds)
    {
        var source = _activeBgmSource;
        if (source == null || !source.isPlaying)
            yield break;

        if (fadeSeconds <= 0f)
        {
            source.Stop();
            source.volume = 0f;
            _activeBgmCueId = AudioCueId.None;
            _bgmRoutine = null;
            yield break;
        }

        var start = source.volume;
        var elapsed = 0f;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(elapsed / fadeSeconds);
            source.volume = Mathf.Lerp(start, 0f, t);
            yield return null;
        }

        source.Stop();
        source.volume = 0f;
        _activeBgmCueId = AudioCueId.None;
        _bgmRoutine = null;
    }

    private void ApplySavedBusVolumes()
    {
        SetBusVolume(AudioBusType.Master, AudioSettingsStore.Load(AudioBusType.Master, 1f));
        SetBusVolume(AudioBusType.Bgm, AudioSettingsStore.Load(AudioBusType.Bgm, 1f));
        SetBusVolume(AudioBusType.Sfx, AudioSettingsStore.Load(AudioBusType.Sfx, 1f));
        SetBusVolume(AudioBusType.Ui, AudioSettingsStore.Load(AudioBusType.Ui, 1f));
    }

    private string GetMixerParamName(AudioBusType busType)
    {
        switch (busType)
        {
            case AudioBusType.Master:
                return _masterVolumeParam;
            case AudioBusType.Bgm:
                return _bgmVolumeParam;
            case AudioBusType.Sfx:
                return _sfxVolumeParam;
            case AudioBusType.Ui:
                return _uiVolumeParam;
            default:
                return string.Empty;
        }
    }

    private static float LinearToDb(float linear)
    {
        if (linear <= 0.0001f)
            return MinDb;
        return Mathf.Log10(linear) * 20f;
    }
}
