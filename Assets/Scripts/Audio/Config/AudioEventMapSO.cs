using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = OperationMarigoldPaths.SoAudioEventMap, fileName = "AudioEventMap")]
public class AudioEventMapSO : ScriptableObject
{
    [System.Serializable]
    public struct AudioEventEntry
    {
        public GameAudioEvent gameEvent;
        public AudioCueId cueId;
    }

    [SerializeField] private List<AudioEventEntry> _entries = new List<AudioEventEntry>();

    private Dictionary<GameAudioEvent, AudioCueId> _cache;

    public bool TryGetCueId(GameAudioEvent gameEvent, out AudioCueId cueId)
    {
        EnsureCacheBuilt();
        return _cache.TryGetValue(gameEvent, out cueId) && cueId != AudioCueId.None;
    }

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        RebuildCache();
    }

    [ContextMenu("Audio Event Map/补齐所有 GameAudioEvent 映射（有则跳过）")]
    public void EnsureAllEventMappings()
    {
        if (_entries == null)
            _entries = new List<AudioEventEntry>();

        var indexByEvent = new Dictionary<GameAudioEvent, int>();
        for (var i = 0; i < _entries.Count; i++)
        {
            var evt = _entries[i].gameEvent;
            if (evt == GameAudioEvent.None || indexByEvent.ContainsKey(evt))
                continue;
            indexByEvent.Add(evt, i);
        }

        var events = (GameAudioEvent[])System.Enum.GetValues(typeof(GameAudioEvent));
        for (var i = 0; i < events.Length; i++)
        {
            var evt = events[i];
            if (evt == GameAudioEvent.None)
                continue;

            var defaultCue = SuggestCueId(evt);
            if (indexByEvent.TryGetValue(evt, out var index))
            {
                var existing = _entries[index];
                // 已有有效映射则跳过；仅补齐“占位但未配置”的 None。
                if (existing.cueId == AudioCueId.None && defaultCue != AudioCueId.None)
                {
                    existing.cueId = defaultCue;
                    _entries[index] = existing;
                }
                continue;
            }

            _entries.Add(new AudioEventEntry
            {
                gameEvent = evt,
                cueId = defaultCue
            });
        }

        _entries.Sort((a, b) => ((int)a.gameEvent).CompareTo((int)b.gameEvent));
        RebuildCache();
    }

    private void EnsureCacheBuilt()
    {
        if (_cache == null)
            RebuildCache();
    }

    private void RebuildCache()
    {
        if (_cache == null)
            _cache = new Dictionary<GameAudioEvent, AudioCueId>();
        else
            _cache.Clear();

        if (_entries == null)
            return;

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.gameEvent == GameAudioEvent.None || entry.cueId == AudioCueId.None)
                continue;

            if (_cache.ContainsKey(entry.gameEvent))
                continue;

            _cache.Add(entry.gameEvent, entry.cueId);
        }
    }

    private static AudioCueId SuggestCueId(GameAudioEvent gameEvent)
    {
        switch (gameEvent)
        {
            case GameAudioEvent.CursorMove:
                return AudioCueId.UiCursorMove;
            case GameAudioEvent.UiConfirm:
                return AudioCueId.UiConfirm;
            case GameAudioEvent.UiCancel:
                return AudioCueId.UiCancel;
            case GameAudioEvent.AttackTargetInvalid:
                return AudioCueId.UiInvalid;
            case GameAudioEvent.CursorRapidStart:
                return AudioCueId.SfxCursorRapidStart;
            case GameAudioEvent.CursorRapidEnd:
                return AudioCueId.SfxCursorRapidEnd;
            case GameAudioEvent.SelectedCellChanged:
                return AudioCueId.UiCellSelected;
            case GameAudioEvent.SpawnMenuOpened:
                return AudioCueId.UiSpawnMenuOpen;
            case GameAudioEvent.TypewriterCharacterTyped:
                return AudioCueId.UiTypewriterChar;
            case GameAudioEvent.TurnStarted:
                return AudioCueId.SfxTurnStart;
            case GameAudioEvent.TurnEnded:
                return AudioCueId.SfxTurnEnd;
            case GameAudioEvent.DayChanged:
                return AudioCueId.SfxDayChanged;
            case GameAudioEvent.UnitDamaged:
                return AudioCueId.SfxUnitDamaged;
            case GameAudioEvent.UnitDeath:
                return AudioCueId.SfxUnitDeath;
            case GameAudioEvent.BuildingCaptured:
                return AudioCueId.SfxBuildingCaptured;
            case GameAudioEvent.UnitSpawned:
                return AudioCueId.SfxUnitSpawned;
            case GameAudioEvent.UnitMoveStarted:
                return AudioCueId.SfxUnitMoveStart;
            case GameAudioEvent.UnitMoveEnded:
                return AudioCueId.SfxUnitMoveEnd;
            case GameAudioEvent.UnitMoveStep:
                return AudioCueId.SfxUnitMoveStep;
            case GameAudioEvent.UnitMoveJump:
                return AudioCueId.SfxUnitMoveJump;
            case GameAudioEvent.UnitMoveTurn:
                return AudioCueId.SfxUnitMoveTurn;
            case GameAudioEvent.UnitAttackFire:
                return AudioCueId.SfxUnitAttackFire;
            case GameAudioEvent.UnitHitReceived:
                return AudioCueId.SfxUnitHitReceived;
            case GameAudioEvent.UnitTransportLoad:
                return AudioCueId.SfxUnitTransportLoad;
            case GameAudioEvent.UnitTransportUnload:
                return AudioCueId.SfxUnitTransportUnload;
            case GameAudioEvent.UnitSupplyPerformed:
                return AudioCueId.SfxUnitSupply;
            case GameAudioEvent.VictoryResult:
                return AudioCueId.BgmVictory;
            case GameAudioEvent.DefeatResult:
                return AudioCueId.BgmDefeat;
            default:
                return AudioCueId.None;
        }
    }
}
