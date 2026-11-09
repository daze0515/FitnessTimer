using FitnessTimer.App.Models;

namespace FitnessTimer.App.Services;

public sealed class TimerEngine
{
    private readonly TrainingPlan plan;
    private readonly List<WorkoutGroupRecord> groupRecords;
    private readonly int preparationSeconds;
    private TimerPhase phase = TimerPhase.NotStarted;
    private TimerPhase previousActivePhase = TimerPhase.NotStarted;
    private int remainingSeconds;
    private int phaseDurationSeconds;
    private int currentGroupPosition;
    private int actualTrainingSeconds;
    private int restSeconds;
    private int pauseSeconds;
    private int completedGroups;
    private DateTimeOffset startedAt;

    public TimerEngine(TrainingPlan plan, int preparationSeconds)
    {
        this.plan = ClonePlan(plan);
        this.preparationSeconds = Math.Clamp(preparationSeconds, 0, 60);
        groupRecords = CreateGroupRecords(this.plan);
    }

    public TimerSnapshot Snapshot => CreateSnapshot();

    public void Start()
    {
        Start(DateTimeOffset.Now);
    }

    public void Start(DateTimeOffset startedAt)
    {
        this.startedAt = startedAt;

        if (plan.Groups.Count == 0)
        {
            CompleteWorkout();
            return;
        }

        currentGroupPosition = 0;

        if (preparationSeconds > 0)
        {
            SetPhase(TimerPhase.Preparation, preparationSeconds);
            return;
        }

        EnterTraining();
    }

    public void Tick()
    {
        switch (phase)
        {
            case TimerPhase.Preparation:
                TickPreparation();
                break;
            case TimerPhase.Training:
                TickTraining();
                break;
            case TimerPhase.Resting:
                TickResting();
                break;
            case TimerPhase.Paused:
                pauseSeconds++;
                break;
        }
    }

    public void Pause()
    {
        if (phase is not (TimerPhase.Preparation or TimerPhase.Training or TimerPhase.Resting))
        {
            return;
        }

        previousActivePhase = phase;
        SetPhase(TimerPhase.Paused, remainingSeconds, phaseDurationSeconds);
    }

    public void Resume()
    {
        if (phase != TimerPhase.Paused || previousActivePhase == TimerPhase.NotStarted)
        {
            return;
        }

        SetPhase(previousActivePhase, remainingSeconds, phaseDurationSeconds);
        previousActivePhase = TimerPhase.NotStarted;
    }

    public void Stop()
    {
        SetPhase(TimerPhase.Stopped, 0);
    }

    public void SkipCurrentPhase()
    {
        if (phase == TimerPhase.Training)
        {
            CurrentGroupRecord.TrainingSkipped = true;
            EnterRest();
            return;
        }

        if (phase == TimerPhase.Resting)
        {
            CurrentGroupRecord.RestSkipped = true;
            completedGroups++;
            MoveToNextGroupOrComplete();
        }
    }

    public void CompleteWorkout()
    {
        SetPhase(TimerPhase.Completed, 0);
    }

    public WorkoutRecord CreateRecord(bool isCompleted, DateTimeOffset endedAt)
    {
        return new WorkoutRecord
        {
            StartedAt = startedAt,
            EndedAt = endedAt,
            IsCompleted = isCompleted,
            TotalElapsedSeconds = actualTrainingSeconds + restSeconds + pauseSeconds,
            ActualTrainingSeconds = actualTrainingSeconds,
            RestSeconds = restSeconds,
            PauseSeconds = pauseSeconds,
            CompletedGroups = completedGroups,
            TotalPlannedGroups = plan.Groups.Count,
            PreparationSeconds = preparationSeconds,
            PlanSnapshot = ClonePlan(plan),
            Groups = CloneGroupRecords(groupRecords)
        };
    }

    private WorkoutGroupRecord CurrentGroupRecord => groupRecords[currentGroupPosition];

    private TrainingGroup CurrentGroup => plan.Groups[currentGroupPosition];

    private void TickPreparation()
    {
        int remaining = remainingSeconds - 1;
        if (remaining <= 0)
        {
            EnterTraining();
            return;
        }

        SetPhase(TimerPhase.Preparation, remaining);
    }

    private void TickTraining()
    {
        if (remainingSeconds <= 0)
        {
            EnterRest();
            return;
        }

        actualTrainingSeconds++;
        CurrentGroupRecord.ActualTrainingSeconds++;

        int remaining = remainingSeconds - 1;
        if (remaining <= 0)
        {
            EnterRest();
            return;
        }

        SetPhase(TimerPhase.Training, remaining);
    }

    private void TickResting()
    {
        if (remainingSeconds <= 0)
        {
            completedGroups++;
            MoveToNextGroupOrComplete();
            return;
        }

        restSeconds++;
        CurrentGroupRecord.ActualRestSeconds++;

        int remaining = remainingSeconds - 1;
        if (remaining <= 0)
        {
            completedGroups++;
            MoveToNextGroupOrComplete();
            return;
        }

        SetPhase(TimerPhase.Resting, remaining);
    }

    private void EnterTraining()
    {
        if (CurrentGroup.TrainingSeconds <= 0)
        {
            EnterRest();
            return;
        }

        SetPhase(TimerPhase.Training, CurrentGroup.TrainingSeconds);
    }

    private void EnterRest()
    {
        if (CurrentGroup.RestSeconds <= 0)
        {
            completedGroups++;
            MoveToNextGroupOrComplete();
            return;
        }

        SetPhase(TimerPhase.Resting, CurrentGroup.RestSeconds);
    }

    private void MoveToNextGroupOrComplete()
    {
        currentGroupPosition++;
        if (currentGroupPosition >= plan.Groups.Count)
        {
            CompleteWorkout();
            return;
        }

        EnterTraining();
    }

    private void SetPhase(TimerPhase nextPhase, int nextRemainingSeconds)
    {
        int nextDuration = nextPhase == phase ? phaseDurationSeconds : Math.Max(0, nextRemainingSeconds);
        SetPhase(nextPhase, nextRemainingSeconds, nextDuration);
    }

    private void SetPhase(TimerPhase nextPhase, int nextRemainingSeconds, int nextPhaseDurationSeconds)
    {
        phase = nextPhase;
        remainingSeconds = Math.Max(0, nextRemainingSeconds);
        phaseDurationSeconds = Math.Max(0, nextPhaseDurationSeconds);
    }

    private TimerSnapshot CreateSnapshot()
    {
        return new TimerSnapshot
        {
            Phase = phase,
            RemainingSeconds = remainingSeconds,
            PhaseDurationSeconds = phaseDurationSeconds,
            CurrentGroupIndex = GetCurrentGroupIndex(phase),
            TotalGroups = plan.Groups.Count,
            CurrentActionName = GetCurrentActionName(phase),
            NextPhaseLabel = GetNextPhaseLabel(phase),
            Groups = CloneGroupRecords(groupRecords)
        };
    }

    private int GetCurrentGroupIndex(TimerPhase phase)
    {
        if (phase is TimerPhase.NotStarted or TimerPhase.Completed or TimerPhase.Stopped || plan.Groups.Count == 0)
        {
            return 0;
        }

        return currentGroupPosition + 1;
    }

    private string GetCurrentActionName(TimerPhase phase)
    {
        if (phase is TimerPhase.Training or TimerPhase.Resting or TimerPhase.Paused && plan.Groups.Count > 0)
        {
            return CurrentGroup.ActionName;
        }

        return string.Empty;
    }

    private string GetNextPhaseLabel(TimerPhase phase)
    {
        return phase switch
        {
            TimerPhase.NotStarted => preparationSeconds > 0 ? "准备" : "训练",
            TimerPhase.Preparation => "训练",
            TimerPhase.Training => CurrentGroup.RestSeconds > 0 ? "休息" : "下一组",
            TimerPhase.Resting => currentGroupPosition + 1 < plan.Groups.Count ? "下一组" : "完成",
            TimerPhase.Paused => previousActivePhase switch
            {
                TimerPhase.Preparation => "准备",
                TimerPhase.Training => "训练",
                TimerPhase.Resting => "休息",
                _ => string.Empty
            },
            _ => string.Empty
        };
    }

    private static TrainingPlan ClonePlan(TrainingPlan source)
    {
        return new TrainingPlan
        {
            Id = source.Id,
            Name = source.Name,
            Groups = source.Groups
                .Select(group => new TrainingGroup
                {
                    Order = group.Order,
                    ActionName = group.ActionName,
                    TrainingSeconds = Math.Max(0, group.TrainingSeconds),
                    RestSeconds = Math.Max(0, group.RestSeconds)
                })
                .ToList()
        };
    }

    private static List<WorkoutGroupRecord> CreateGroupRecords(TrainingPlan plan)
    {
        return plan.Groups
            .Select((group, index) => new WorkoutGroupRecord
            {
                GroupNumber = index + 1,
                ActionName = group.ActionName,
                PlannedTrainingSeconds = group.TrainingSeconds,
                PlannedRestSeconds = group.RestSeconds
            })
            .ToList();
    }

    private static List<WorkoutGroupRecord> CloneGroupRecords(IEnumerable<WorkoutGroupRecord> records)
    {
        return records
            .Select(record => new WorkoutGroupRecord
            {
                GroupNumber = record.GroupNumber,
                ActionName = record.ActionName,
                PlannedTrainingSeconds = record.PlannedTrainingSeconds,
                ActualTrainingSeconds = record.ActualTrainingSeconds,
                PlannedRestSeconds = record.PlannedRestSeconds,
                ActualRestSeconds = record.ActualRestSeconds,
                TrainingSkipped = record.TrainingSkipped,
                RestSkipped = record.RestSkipped
            })
            .ToList();
    }
}
