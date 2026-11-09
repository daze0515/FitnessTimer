using FitnessTimer.App.Models;

namespace FitnessTimer.App.Services;

public sealed class StatisticsService
{
    public StatisticsSummary BuildWeekStats(IReadOnlyList<WorkoutRecord> records, DateTime dayInWeek)
    {
        DateTime selectedDay = dayInWeek.Date;
        int daysSinceMonday = ((int)selectedDay.DayOfWeek + 6) % 7;
        DateTime monday = selectedDay.AddDays(-daysSinceMonday);
        List<DateTime> days = Enumerable.Range(0, 7)
            .Select(offset => monday.AddDays(offset))
            .ToList();

        return BuildStats(records, days);
    }

    public StatisticsSummary BuildMonthStats(IReadOnlyList<WorkoutRecord> records, DateTime dayInMonth)
    {
        DateTime firstDay = new(dayInMonth.Year, dayInMonth.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(dayInMonth.Year, dayInMonth.Month);
        List<DateTime> days = Enumerable.Range(0, daysInMonth)
            .Select(offset => firstDay.AddDays(offset))
            .ToList();

        return BuildStats(records, days);
    }

    private static StatisticsSummary BuildStats(IReadOnlyList<WorkoutRecord> records, IReadOnlyList<DateTime> days)
    {
        HashSet<DateTime> includedDays = days.Select(day => day.Date).ToHashSet();
        List<WorkoutRecord> includedRecords = records
            .Where(record => includedDays.Contains(record.StartedAt.Date))
            .ToList();
        Dictionary<DateTime, int> trainingSecondsByDay = includedRecords
            .GroupBy(record => record.StartedAt.Date)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(record => record.ActualTrainingSeconds));

        return new StatisticsSummary
        {
            WorkoutCount = includedRecords.Count,
            TotalElapsedSeconds = includedRecords.Sum(record => record.TotalElapsedSeconds),
            ActualTrainingSeconds = includedRecords.Sum(record => record.ActualTrainingSeconds),
            RestSeconds = includedRecords.Sum(record => record.RestSeconds),
            Bars = days
                .Select(day => new StatisticsBar
                {
                    Date = day.Date,
                    TrainingMinutes = (int)Math.Round(
                        trainingSecondsByDay.GetValueOrDefault(day.Date) / 60.0,
                        MidpointRounding.AwayFromZero)
                })
                .ToList()
        };
    }
}

public sealed class StatisticsSummary
{
    public int WorkoutCount { get; set; }

    public int TotalElapsedSeconds { get; set; }

    public int ActualTrainingSeconds { get; set; }

    public int RestSeconds { get; set; }

    public List<StatisticsBar> Bars { get; set; } = [];
}

public sealed class StatisticsBar
{
    public DateTime Date { get; set; }

    public int TrainingMinutes { get; set; }
}
