namespace FitnessTimer.App.Models;

public enum TimerPhase
{
    NotStarted,
    Preparation,
    Training,
    Resting,
    Paused,
    Completed,
    Stopped
}

public sealed record TimerSnapshot
{
    public TimerPhase Phase { get; init; }

    public int RemainingSeconds { get; init; }

    public int PhaseDurationSeconds { get; init; }

    public int CurrentGroupIndex { get; init; }

    public int TotalGroups { get; init; }

    public string CurrentActionName { get; init; } = string.Empty;

    public string NextPhaseLabel { get; init; } = string.Empty;

    public IReadOnlyList<WorkoutGroupRecord> Groups { get; init; } = [];
}
