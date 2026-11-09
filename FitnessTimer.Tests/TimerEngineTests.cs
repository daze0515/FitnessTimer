using FitnessTimer.App.Models;
using FitnessTimer.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FitnessTimer.Tests;

[TestClass]
public sealed class TimerEngineTests
{
    [TestMethod]
    public void StartUsesPreparationCountdownBeforeTraining()
    {
        TimerEngine engine = new(CreatePlan(30, 15), preparationSeconds: 5);

        engine.Start(new DateTimeOffset(2026, 5, 25, 20, 0, 0, TimeSpan.Zero));

        TimerSnapshot snapshot = engine.Snapshot;
        Assert.AreEqual(TimerPhase.Preparation, snapshot.Phase);
        Assert.AreEqual(5, snapshot.RemainingSeconds);
        Assert.AreEqual(5, snapshot.PhaseDurationSeconds);

        engine.Tick();
        Assert.AreEqual(TimerPhase.Preparation, engine.Snapshot.Phase);
        Assert.AreEqual(4, engine.Snapshot.RemainingSeconds);
        Assert.AreEqual(5, engine.Snapshot.PhaseDurationSeconds);
    }

    [TestMethod]
    public void AfterPreparationTicksFirstTrainingGroupBegins()
    {
        TimerEngine engine = new(CreatePlan(30, 15), preparationSeconds: 2);

        engine.Start();
        engine.Tick();
        engine.Tick();

        TimerSnapshot snapshot = engine.Snapshot;
        Assert.AreEqual(TimerPhase.Training, snapshot.Phase);
        Assert.AreEqual(30, snapshot.RemainingSeconds);
        Assert.AreEqual(30, snapshot.PhaseDurationSeconds);
        Assert.AreEqual(1, snapshot.CurrentGroupIndex);
        Assert.AreEqual("动作 1", snapshot.CurrentActionName);
    }

    [TestMethod]
    public void SkipFromTrainingEntersRestAndMarksTrainingSkipped()
    {
        TimerEngine engine = new(CreatePlan(30, 15), preparationSeconds: 0);

        engine.Start();
        engine.SkipCurrentPhase();

        TimerSnapshot snapshot = engine.Snapshot;
        Assert.AreEqual(TimerPhase.Resting, snapshot.Phase);
        Assert.AreEqual(15, snapshot.RemainingSeconds);
        Assert.AreEqual(15, snapshot.PhaseDurationSeconds);

        WorkoutRecord record = engine.CreateRecord(isCompleted: false, endedAt: DateTimeOffset.Parse("2026-05-25T20:05:00Z"));
        Assert.IsTrue(record.Groups[0].TrainingSkipped);
        Assert.IsFalse(record.Groups[0].RestSkipped);
    }

    [TestMethod]
    public void CompleteWorkoutDoesNotCountPreparationTime()
    {
        DateTimeOffset startedAt = DateTimeOffset.Parse("2026-05-25T20:00:00Z");
        DateTimeOffset endedAt = DateTimeOffset.Parse("2026-05-25T20:00:08Z");
        TrainingPlan plan = CreatePlan(2, 1);
        plan.Name = "HIIT";
        TimerEngine engine = new(plan, preparationSeconds: 5);

        engine.Start(startedAt);
        Tick(engine, 8);

        WorkoutRecord record = engine.CreateRecord(isCompleted: true, endedAt);

        Assert.AreEqual(TimerPhase.Completed, engine.Snapshot.Phase);
        Assert.AreEqual(3, record.TotalElapsedSeconds);
        Assert.AreEqual(2, record.ActualTrainingSeconds);
        Assert.AreEqual(1, record.RestSeconds);
        Assert.AreEqual(0, record.PauseSeconds);
        Assert.AreEqual(1, record.CompletedGroups);
        Assert.AreEqual(5, record.PreparationSeconds);
        Assert.AreEqual(startedAt, record.StartedAt);
        Assert.AreEqual(endedAt, record.EndedAt);
        Assert.IsTrue(record.IsCompleted);
        Assert.AreEqual("HIIT", record.PlanSnapshot.Name);
        Assert.AreEqual(plan.Id, record.PlanSnapshot.Id);
    }

    [TestMethod]
    public void PerGroupCustomDurationsAreHonoredAfterFirstGroup()
    {
        TrainingPlan plan = new()
        {
            Groups =
            [
                new TrainingGroup { Order = 1, ActionName = "动作 1", TrainingSeconds = 1, RestSeconds = 1 },
                new TrainingGroup { Order = 2, ActionName = "动作 2", TrainingSeconds = 4, RestSeconds = 3 }
            ]
        };
        TimerEngine engine = new(plan, preparationSeconds: 0);

        engine.Start();
        engine.Tick();
        engine.Tick();

        TimerSnapshot snapshot = engine.Snapshot;
        Assert.AreEqual(TimerPhase.Training, snapshot.Phase);
        Assert.AreEqual(2, snapshot.CurrentGroupIndex);
        Assert.AreEqual("动作 2", snapshot.CurrentActionName);
        Assert.AreEqual(4, snapshot.RemainingSeconds);
        Assert.AreEqual(4, snapshot.PhaseDurationSeconds);

        Tick(engine, 4);
        Assert.AreEqual(TimerPhase.Resting, engine.Snapshot.Phase);
        Assert.AreEqual(3, engine.Snapshot.RemainingSeconds);
        Assert.AreEqual(3, engine.Snapshot.PhaseDurationSeconds);
    }

    [TestMethod]
    public void PauseTicksCountPauseSecondsButDoNotReduceRemainingSeconds()
    {
        TimerEngine engine = new(CreatePlan(30, 15), preparationSeconds: 0);

        engine.Start();
        engine.Tick();
        engine.Pause();
        Tick(engine, 3);

        TimerSnapshot snapshot = engine.Snapshot;
        Assert.AreEqual(TimerPhase.Paused, snapshot.Phase);
        Assert.AreEqual(29, snapshot.RemainingSeconds);
        Assert.AreEqual(30, snapshot.PhaseDurationSeconds);

        engine.Resume();
        Assert.AreEqual(TimerPhase.Training, engine.Snapshot.Phase);
        Assert.AreEqual(29, engine.Snapshot.RemainingSeconds);
        Assert.AreEqual(30, engine.Snapshot.PhaseDurationSeconds);

        WorkoutRecord record = engine.CreateRecord(isCompleted: false, endedAt: DateTimeOffset.Parse("2026-05-25T20:01:00Z"));
        Assert.AreEqual(1, record.ActualTrainingSeconds);
        Assert.AreEqual(3, record.PauseSeconds);
    }

    [TestMethod]
    public void SkipFromRestCountsFullyTrainedGroupAsCompleted()
    {
        TimerEngine engine = new(CreatePlan(2, 10), preparationSeconds: 0);

        engine.Start();
        Tick(engine, 2);
        engine.SkipCurrentPhase();

        WorkoutRecord record = engine.CreateRecord(isCompleted: true, endedAt: DateTimeOffset.Parse("2026-05-25T20:01:00Z"));
        Assert.AreEqual(TimerPhase.Completed, engine.Snapshot.Phase);
        Assert.AreEqual(1, record.CompletedGroups);
        Assert.IsFalse(record.Groups[0].TrainingSkipped);
        Assert.IsTrue(record.Groups[0].RestSkipped);
    }

    [TestMethod]
    public void ZeroSecondTrainingAdvancesWithoutRecordingTrainingTime()
    {
        TimerEngine engine = new(CreatePlan(0, 3), preparationSeconds: 0);

        engine.Start();

        Assert.AreEqual(TimerPhase.Resting, engine.Snapshot.Phase);
        Assert.AreEqual(3, engine.Snapshot.RemainingSeconds);
        Assert.AreEqual(3, engine.Snapshot.PhaseDurationSeconds);

        WorkoutRecord record = engine.CreateRecord(isCompleted: false, endedAt: DateTimeOffset.Parse("2026-05-25T20:01:00Z"));
        Assert.AreEqual(0, record.ActualTrainingSeconds);
        Assert.AreEqual(0, record.Groups[0].ActualTrainingSeconds);
    }

    [TestMethod]
    public void MutatingReturnedSnapshotDoesNotDriveEngineState()
    {
        TimerEngine engine = new(CreatePlan(30, 15), preparationSeconds: 0);
        engine.Start();
        TimerSnapshot leakedSnapshot = engine.Snapshot;

        typeof(TimerSnapshot)
            .GetProperty(nameof(TimerSnapshot.Phase))!
            .SetValue(leakedSnapshot, TimerPhase.Completed);

        engine.Tick();

        Assert.AreEqual(TimerPhase.Training, engine.Snapshot.Phase);
        Assert.AreEqual(29, engine.Snapshot.RemainingSeconds);
        WorkoutRecord record = engine.CreateRecord(isCompleted: false, endedAt: DateTimeOffset.Parse("2026-05-25T20:01:00Z"));
        Assert.AreEqual(1, record.ActualTrainingSeconds);
    }

    private static TrainingPlan CreatePlan(int trainingSeconds, int restSeconds)
    {
        return new TrainingPlan
        {
            Groups =
            [
                new TrainingGroup
                {
                    Order = 1,
                    ActionName = "动作 1",
                    TrainingSeconds = trainingSeconds,
                    RestSeconds = restSeconds
                }
            ]
        };
    }

    private static void Tick(TimerEngine engine, int times)
    {
        for (int i = 0; i < times; i++)
        {
            engine.Tick();
        }
    }
}
