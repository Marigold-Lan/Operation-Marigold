using UnityEngine;

public enum CommandType
{
    Capture,
    Load,
    Drop,
    Supply,
    Fire,
    Wait,
    Produce
}

public class CommandOption
{
    public CommandType Type;
    public string Label;
    public Sprite Icon;
    public bool Interactable = true;
    public ICommand Command;
}

public class CommandContext
{
    public enum ExecutionMode
    {
        PlayerInteractive = 0,
        AIImmediate = 1
    }

    public UnitController Unit;
    public UnitController TargetUnit;
    public Cell CurrentCell;
    public MapRoot MapRoot;
    public GameSessionState SessionState;
    public SelectionManager SelectionManager;
    public HighlightManager HighlightManager;
    public Vector2Int GridCoord;
    public bool ConsumeActionOnCancel;
    public PlayerTurnController TurnController;
    public AttackTargetingSession AttackTargetingSession;
    public Vector2Int TargetCoord;
    public bool HasTargetCoord;
    public FactorySpawner FactorySpawner;
    public UnitData ProduceUnitData;
    public Cell SpawnCell;
    public ExecutionMode Mode = ExecutionMode.PlayerInteractive;
}
