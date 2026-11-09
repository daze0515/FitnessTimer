namespace FitnessTimer.App.Models;

public sealed class TrainingGroup
{
    public int Order { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public int TrainingSeconds { get; set; }

    public int RestSeconds { get; set; }
}
