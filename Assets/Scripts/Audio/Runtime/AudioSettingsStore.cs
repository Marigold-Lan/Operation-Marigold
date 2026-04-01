using UnityEngine;

public static class AudioSettingsStore
{
    private const string MasterKey = "Audio.Volume.Master";
    private const string BgmKey = "Audio.Volume.Bgm";
    private const string SfxKey = "Audio.Volume.Sfx";
    private const string UiKey = "Audio.Volume.Ui";

    public static float Load(AudioBusType busType, float fallback = 1f)
    {
        var key = GetKey(busType);
        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, Mathf.Clamp01(fallback)));
    }

    public static void Save(AudioBusType busType, float linearVolume)
    {
        var key = GetKey(busType);
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(linearVolume));
        PlayerPrefs.Save();
    }

    private static string GetKey(AudioBusType busType)
    {
        switch (busType)
        {
            case AudioBusType.Master:
                return MasterKey;
            case AudioBusType.Bgm:
                return BgmKey;
            case AudioBusType.Sfx:
                return SfxKey;
            case AudioBusType.Ui:
                return UiKey;
            default:
                return MasterKey;
        }
    }
}
