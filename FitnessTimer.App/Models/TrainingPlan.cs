using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FitnessTimer.App.Models;

public sealed class TrainingPlan : INotifyPropertyChanged
{
    private string name = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => name;
        set
        {
            if (name == value)
            {
                return;
            }

            name = value;
            OnPropertyChanged();
        }
    }

    public List<TrainingGroup> Groups { get; set; } = [];

    public static TrainingPlan CreateDefault()
    {
        return new TrainingPlan
        {
            Name = "默认方案",
            Groups =
            [
                new TrainingGroup
                {
                    Order = 1,
                    ActionName = "动作 1",
                    TrainingSeconds = 30,
                    RestSeconds = 15
                },
                new TrainingGroup
                {
                    Order = 2,
                    ActionName = "动作 2",
                    TrainingSeconds = 30,
                    RestSeconds = 15
                },
                new TrainingGroup
                {
                    Order = 3,
                    ActionName = "动作 3",
                    TrainingSeconds = 30,
                    RestSeconds = 15
                }
            ]
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class TrainingPlanLibrary
{
    public Guid ActivePlanId { get; set; }

    public List<TrainingPlan> Plans { get; set; } = [];

    public static TrainingPlanLibrary CreateDefault()
    {
        TrainingPlan plan = TrainingPlan.CreateDefault();
        return new TrainingPlanLibrary
        {
            ActivePlanId = plan.Id,
            Plans = [plan]
        };
    }
}
