using FitnessTimer.App.Models;

namespace FitnessTimer.App.Services;

public sealed class HistoryService
{
    private readonly JsonDataStore store;

    public HistoryService(JsonDataStore store)
    {
        this.store = store;
    }

    public List<WorkoutRecord> LoadHistory()
    {
        return store.LoadHistory()
            .OrderByDescending(record => record.StartedAt)
            .ToList();
    }

    public void AddRecord(WorkoutRecord record)
    {
        List<WorkoutRecord> records = store.LoadHistory();
        records.Add(record);
        store.SaveHistory(records);
    }

    public bool DeleteRecord(Guid id)
    {
        List<WorkoutRecord> records = store.LoadHistory();
        int removedCount = records.RemoveAll(record => record.Id == id);
        if (removedCount == 0)
        {
            return false;
        }

        store.SaveHistory(records);
        return true;
    }
}
