using FitnessTimer.App.Models;
using FitnessTimer.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FitnessTimer.Tests;

[TestClass]
public sealed class StatisticsServiceTests
{
    [TestMethod]
    public void BuildWeekStats_SumsSameDayRecordsAndProducesMondayFirstBars()
    {
        StatisticsService service = new();
        List<WorkoutRecord> records =
        [
            CreateRecord("2026-05-18T08:00:00+08:00", total: 180, training: 120, rest: 60),
            CreateRecord("2026-05-18T19:00:00+08:00", total: 210, training: 150, rest: 60),
            CreateRecord("2026-05-20T07:30:00+08:00", total: 90, training: 45, rest: 45)
        ];

        StatisticsSummary summary = service.BuildWeekStats(records, new DateTime(2026, 5, 21));

        Assert.AreEqual(3, summary.WorkoutCount);
        Assert.AreEqual(480, summary.TotalElapsedSeconds);
        Assert.AreEqual(315, summary.ActualTrainingSeconds);
        Assert.AreEqual(165, summary.RestSeconds);
        Assert.AreEqual(7, summary.Bars.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                new DateTime(2026, 5, 18),
                new DateTime(2026, 5, 19),
                new DateTime(2026, 5, 20),
                new DateTime(2026, 5, 21),
                new DateTime(2026, 5, 22),
                new DateTime(2026, 5, 23),
                new DateTime(2026, 5, 24)
            },
            summary.Bars.Select(bar => bar.Date).ToArray());
        Assert.AreEqual(5, summary.Bars[0].TrainingMinutes);
        Assert.AreEqual(0, summary.Bars[1].TrainingMinutes);
        Assert.AreEqual(1, summary.Bars[2].TrainingMinutes);
    }

    [TestMethod]
    public void BuildWeekStats_IncludesIncompleteRecords()
    {
        StatisticsService service = new();
        List<WorkoutRecord> records =
        [
            CreateRecord("2026-05-19T08:00:00+08:00", total: 100, training: 70, rest: 30, isCompleted: false)
        ];

        StatisticsSummary summary = service.BuildWeekStats(records, new DateTime(2026, 5, 19));

        Assert.AreEqual(1, summary.WorkoutCount);
        Assert.AreEqual(100, summary.TotalElapsedSeconds);
        Assert.AreEqual(70, summary.ActualTrainingSeconds);
        Assert.AreEqual(30, summary.RestSeconds);
        Assert.AreEqual(1, summary.Bars[1].TrainingMinutes);
    }

    [TestMethod]
    public void BuildWeekStats_ExcludesRecordsOutsideSelectedWeek()
    {
        StatisticsService service = new();
        List<WorkoutRecord> records =
        [
            CreateRecord("2026-05-17T08:00:00+08:00", total: 100, training: 60, rest: 40),
            CreateRecord("2026-05-18T08:00:00+08:00", total: 200, training: 120, rest: 80),
            CreateRecord("2026-05-25T08:00:00+08:00", total: 300, training: 180, rest: 120)
        ];

        StatisticsSummary summary = service.BuildWeekStats(records, new DateTime(2026, 5, 20));

        Assert.AreEqual(1, summary.WorkoutCount);
        Assert.AreEqual(200, summary.TotalElapsedSeconds);
        Assert.AreEqual(120, summary.ActualTrainingSeconds);
        Assert.AreEqual(80, summary.RestSeconds);
        Assert.AreEqual(2, summary.Bars[0].TrainingMinutes);
    }

    [TestMethod]
    public void BuildMonthStats_ProducesOneBarPerDayAndAggregatesSelectedMonth()
    {
        StatisticsService service = new();
        List<WorkoutRecord> records =
        [
            CreateRecord("2026-04-30T08:00:00+08:00", total: 100, training: 60, rest: 40),
            CreateRecord("2026-05-01T08:00:00+08:00", total: 90, training: 45, rest: 45),
            CreateRecord("2026-05-15T08:00:00+08:00", total: 120, training: 70, rest: 50),
            CreateRecord("2026-05-15T18:00:00+08:00", total: 180, training: 110, rest: 70),
            CreateRecord("2026-06-01T08:00:00+08:00", total: 200, training: 120, rest: 80)
        ];

        StatisticsSummary summary = service.BuildMonthStats(records, new DateTime(2026, 5, 25));

        Assert.AreEqual(3, summary.WorkoutCount);
        Assert.AreEqual(390, summary.TotalElapsedSeconds);
        Assert.AreEqual(225, summary.ActualTrainingSeconds);
        Assert.AreEqual(165, summary.RestSeconds);
        Assert.AreEqual(31, summary.Bars.Count);
        Assert.AreEqual(new DateTime(2026, 5, 1), summary.Bars[0].Date);
        Assert.AreEqual(new DateTime(2026, 5, 31), summary.Bars[30].Date);
        Assert.AreEqual(1, summary.Bars[0].TrainingMinutes);
        Assert.AreEqual(3, summary.Bars[14].TrainingMinutes);
    }

    [TestMethod]
    public void HistoryService_AddRecordPersistsAndLoadHistoryReturnsNewestFirst()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);
        HistoryService service = new(store);
        WorkoutRecord oldest = CreateRecord("2026-05-18T08:00:00+08:00", total: 100, training: 60, rest: 40);
        WorkoutRecord newest = CreateRecord("2026-05-20T08:00:00+08:00", total: 200, training: 120, rest: 80);

        service.AddRecord(oldest);
        service.AddRecord(newest);

        List<WorkoutRecord> persisted = new JsonDataStore(projectRoot).LoadHistory();
        Assert.AreEqual(2, persisted.Count);
        List<WorkoutRecord> loaded = service.LoadHistory();
        Assert.AreEqual(newest.Id, loaded[0].Id);
        Assert.AreEqual(oldest.Id, loaded[1].Id);
    }

    [TestMethod]
    public void HistoryService_DeleteRecordRemovesPersistedRecord()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);
        HistoryService service = new(store);
        WorkoutRecord first = CreateRecord("2026-05-18T08:00:00+08:00", total: 100, training: 60, rest: 40);
        WorkoutRecord second = CreateRecord("2026-05-20T08:00:00+08:00", total: 200, training: 120, rest: 80);

        service.AddRecord(first);
        service.AddRecord(second);

        Assert.IsTrue(service.DeleteRecord(first.Id));
        Assert.IsFalse(service.DeleteRecord(Guid.NewGuid()));

        List<WorkoutRecord> loaded = service.LoadHistory();
        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(second.Id, loaded[0].Id);
    }

    private static WorkoutRecord CreateRecord(
        string startedAt,
        int total,
        int training,
        int rest,
        bool isCompleted = true)
    {
        return new WorkoutRecord
        {
            StartedAt = DateTimeOffset.Parse(startedAt),
            IsCompleted = isCompleted,
            TotalElapsedSeconds = total,
            ActualTrainingSeconds = training,
            RestSeconds = rest
        };
    }

    private static string CreateProjectRoot()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "FitnessTimer.StatisticsServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        return projectRoot;
    }
}
