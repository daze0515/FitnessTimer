using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using FitnessTimer.App.Models;

namespace FitnessTimer.App.Services;

public sealed class JsonDataStore
{
    private const string CurrentPlanFileName = "current-plan.json";
    private const string SettingsFileName = "settings.json";
    private const string WorkoutHistoryFileName = "workout-history.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string dataRoot;
    private readonly string currentPlanPath;
    private readonly string settingsPath;
    private readonly string workoutHistoryPath;

    public JsonDataStore(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        dataRoot = Path.Combine(projectRoot, "data");
        currentPlanPath = Path.Combine(dataRoot, CurrentPlanFileName);
        settingsPath = Path.Combine(dataRoot, SettingsFileName);
        workoutHistoryPath = Path.Combine(dataRoot, WorkoutHistoryFileName);
    }

    public void EnsureDefaults()
    {
        Directory.CreateDirectory(dataRoot);
        CreateIfMissing(currentPlanPath, TrainingPlanLibrary.CreateDefault());
        CreateIfMissing(settingsPath, AppSettings.CreateDefault());
        CreateIfMissing(workoutHistoryPath, new List<WorkoutRecord>());
    }

    public TrainingPlan LoadTrainingPlan()
    {
        TrainingPlanLibrary library = LoadTrainingPlanLibrary();
        return library.Plans.FirstOrDefault(plan => plan.Id == library.ActivePlanId) ?? library.Plans[0];
    }

    public void SaveTrainingPlan(TrainingPlan plan)
    {
        TrainingPlan normalizedPlan = EnsurePlanIdentity(plan, "默认方案");
        SaveTrainingPlanLibrary(new TrainingPlanLibrary
        {
            ActivePlanId = normalizedPlan.Id,
            Plans = [normalizedPlan]
        });
    }

    public TrainingPlanLibrary LoadTrainingPlanLibrary()
    {
        CreateIfMissing(currentPlanPath, TrainingPlanLibrary.CreateDefault());
        TrainingPlanLibrary library = ReadTrainingPlanLibrary(currentPlanPath);
        NormalizeLibrary(library);
        return library;
    }

    public void SaveTrainingPlanLibrary(TrainingPlanLibrary library)
    {
        NormalizeLibrary(library);
        WriteJson(currentPlanPath, library);
    }

    public AppSettings LoadSettings()
    {
        CreateIfMissing(settingsPath, AppSettings.CreateDefault());
        return ReadJson<AppSettings>(settingsPath);
    }

    public void SaveSettings(AppSettings settings)
    {
        AppSettings clampedSettings = new()
        {
            PreparationSeconds = Math.Clamp(settings.PreparationSeconds, 0, 60),
            LastPage = settings.LastPage,
            CompactAlwaysOnTop = settings.CompactAlwaysOnTop,
            UseDarkMode = settings.UseDarkMode,
            CompactLeft = SanitizeNullableDimension(settings.CompactLeft),
            CompactTop = SanitizeNullableDimension(settings.CompactTop),
            CompactWidth = SanitizeCompactSize(settings.CompactWidth, 380, 1200),
            CompactHeight = SanitizeCompactSize(settings.CompactHeight, 480, 1400),
            IsCompactMode = settings.IsCompactMode
        };

        WriteJson(settingsPath, clampedSettings);
    }

    public List<WorkoutRecord> LoadHistory()
    {
        CreateIfMissing(workoutHistoryPath, new List<WorkoutRecord>());
        return ReadJson<List<WorkoutRecord>>(workoutHistoryPath);
    }

    public void SaveHistory(List<WorkoutRecord> records)
    {
        WriteJson(workoutHistoryPath, records);
    }

    private static T ReadJson<T>(string path)
    {
        try
        {
            T? value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            if (value is null)
            {
                throw new InvalidDataException($"JSON file '{path}' did not contain a {typeof(T).Name} value.");
            }

            return value;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid JSON in '{path}'.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException($"Unsupported JSON value in '{path}'.", exception);
        }
    }

    private static TrainingPlanLibrary ReadTrainingPlanLibrary(string path)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(nameof(TrainingPlanLibrary.Plans), out _))
            {
                TrainingPlanLibrary? library = document.Deserialize<TrainingPlanLibrary>(JsonOptions);
                return library ?? throw new InvalidDataException($"JSON file '{path}' did not contain a TrainingPlanLibrary value.");
            }

            TrainingPlan? plan = document.Deserialize<TrainingPlan>(JsonOptions);
            if (plan is null)
            {
                throw new InvalidDataException($"JSON file '{path}' did not contain a TrainingPlan value.");
            }

            TrainingPlan normalizedPlan = EnsurePlanIdentity(plan, "默认方案");
            return new TrainingPlanLibrary
            {
                ActivePlanId = normalizedPlan.Id,
                Plans = [normalizedPlan]
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid JSON in '{path}'.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException($"Unsupported JSON value in '{path}'.", exception);
        }
    }

    private static void NormalizeLibrary(TrainingPlanLibrary library)
    {
        if (library.Plans.Count == 0)
        {
            TrainingPlan defaultPlan = TrainingPlan.CreateDefault();
            library.Plans.Add(defaultPlan);
            library.ActivePlanId = defaultPlan.Id;
        }

        for (int index = 0; index < library.Plans.Count; index++)
        {
            library.Plans[index] = EnsurePlanIdentity(library.Plans[index], $"训练方案 {index + 1}");
        }

        if (!library.Plans.Any(plan => plan.Id == library.ActivePlanId))
        {
            library.ActivePlanId = library.Plans[0].Id;
        }
    }

    private static TrainingPlan EnsurePlanIdentity(TrainingPlan plan, string fallbackName)
    {
        plan.Id = plan.Id == Guid.Empty ? Guid.NewGuid() : plan.Id;
        plan.Name = string.IsNullOrWhiteSpace(plan.Name) ? fallbackName : plan.Name.Trim();
        return plan;
    }

    private void CreateIfMissing<T>(string path, T value)
    {
        if (File.Exists(path))
        {
            return;
        }

        WriteJson(path, value);
    }

    private void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(dataRoot);
        string tempPath = Path.Combine(dataRoot, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions));

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static double? SanitizeNullableDimension(double? value)
    {
        return value is double number && !double.IsNaN(number) && !double.IsInfinity(number)
            ? Math.Round(number, 0)
            : null;
    }

    private static double? SanitizeCompactSize(double? value, double min, double max)
    {
        return value is double number && !double.IsNaN(number) && !double.IsInfinity(number)
            ? Math.Round(Math.Clamp(number, min, max), 0)
            : null;
    }
}
