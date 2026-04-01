using UnityEngine;

public interface IGameAudioService
{
    bool PlayUi(AudioCueId cueId);
    bool PlaySfx(AudioCueId cueId, Vector3 worldPos, float volumeScale = 1f);
    bool PlayBgm(AudioCueId cueId, float fadeSeconds = 0.5f);
    void StopBgm(float fadeSeconds = 0.5f);
    void SetBusVolume(AudioBusType busType, float linearVolume);
    float GetBusVolume(AudioBusType busType);
}
