namespace FitnessTimer.App.Models;

public sealed class WorkoutRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsCompleted { get; set; }

    public int TotalElapsedSeconds { get; set; }

    public int ActualTrainingSeconds { get; set; }

    public int RestSeconds { get; set; }

    public int PauseSeconds { get; set; }

    public int CompletedGroups { get; set; }

    public int TotalPlannedGroups { get; set; }

    public int PreparationSeconds { get; set; }

    public TrainingPlan PlanSnapshot { get; set; } = new();

    public List<WorkoutGroupRecord> Groups { get; set; } = [];
}

public sealed class WorkoutGroupRecord
{
    public int GroupNumber { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public int PlannedTrainingSeconds { get; set; }

    public int ActualTrainingSeconds { get; set; }

    public int PlannedRestSeconds { get; set; }

    public int ActualRestSeconds { get; set; }

    public bool TrainingSkipped { get; set; }

    public bool RestSkipped { get; set; }
}
