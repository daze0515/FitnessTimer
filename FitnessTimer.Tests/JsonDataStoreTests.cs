using FitnessTimer.App.Models;
using FitnessTimer.App.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FitnessTimer.Tests;

[TestClass]
public sealed class JsonDataStoreTests
{
    [TestMethod]
    public void EnsureDefaults_CreatesMissingFiles()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);

        store.EnsureDefaults();

        Assert.IsTrue(File.Exists(Path.Combine(projectRoot, "data", "current-plan.json")));
        Assert.IsTrue(File.Exists(Path.Combine(projectRoot, "data", "settings.json")));
        Assert.IsTrue(File.Exists(Path.Combine(projectRoot, "data", "workout-history.json")));

        TrainingPlanLibrary library = store.LoadTrainingPlanLibrary();
        Assert.AreEqual(1, library.Plans.Count);
        Assert.AreEqual(library.Plans[0].Id, library.ActivePlanId);

        TrainingPlan plan = library.Plans[0];
        Assert.AreEqual("默认方案", plan.Name);
        Assert.AreEqual(3, plan.Groups.Count);
        Assert.AreEqual("动作 1", plan.Groups[0].ActionName);
        Assert.AreEqual(30, plan.Groups[0].TrainingSeconds);
        Assert.AreEqual(15, plan.Groups[0].RestSeconds);

        AppSettings settings = store.LoadSettings();
        Assert.AreEqual(5, settings.PreparationSeconds);
        Assert.AreEqual("Timer", settings.LastPage);
        Assert.IsFalse(settings.CompactAlwaysOnTop);
        Assert.IsFalse(settings.UseDarkMode);
        Assert.IsNull(settings.CompactLeft);
        Assert.IsNull(settings.CompactTop);
        Assert.IsNull(settings.CompactWidth);
        Assert.IsNull(settings.CompactHeight);
        Assert.IsFalse(settings.IsCompactMode);

        CollectionAssert.AreEqual(Array.Empty<WorkoutRecord>(), store.LoadHistory());
    }

    [TestMethod]
    public void LoadTrainingPlanLibrary_ReadsSavedPlans()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);
        TrainingPlan savedPlan = new()
        {
            Name = "HIIT",
            Groups =
            [
                new TrainingGroup
                {
                    Order = 1,
                    ActionName = "开合跳",
                    TrainingSeconds = 45,
                    RestSeconds = 20
                }
            ]
        };
        TrainingPlanLibrary savedLibrary = new()
        {
            ActivePlanId = savedPlan.Id,
            Plans = [savedPlan]
        };

        store.SaveTrainingPlanLibrary(savedLibrary);

        TrainingPlanLibrary loadedLibrary = store.LoadTrainingPlanLibrary();
        Assert.AreEqual(savedPlan.Id, loadedLibrary.ActivePlanId);
        Assert.AreEqual(1, loadedLibrary.Plans.Count);
        Assert.AreEqual("HIIT", loadedLibrary.Plans[0].Name);
        Assert.AreEqual("开合跳", loadedLibrary.Plans[0].Groups[0].ActionName);

        string json = File.ReadAllText(Path.Combine(projectRoot, "data", "current-plan.json"));
        StringAssert.Contains(json, "\"Plans\"");
        StringAssert.Contains(json, "HIIT");
    }

    [TestMethod]
    public void LoadTrainingPlanLibrary_MigratesOldSinglePlanJson()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(Path.Combine(dataRoot, "current-plan.json"), """
            {
              "Groups": [
                {
                  "Order": 1,
                  "ActionName": "俯卧撑",
                  "TrainingSeconds": 50,
                  "RestSeconds": 10
                }
              ]
            }
            """);
        JsonDataStore store = new(projectRoot);

        TrainingPlanLibrary library = store.LoadTrainingPlanLibrary();

        Assert.AreEqual(1, library.Plans.Count);
        Assert.AreEqual(library.Plans[0].Id, library.ActivePlanId);
        Assert.AreEqual("默认方案", library.Plans[0].Name);
        Assert.AreEqual("俯卧撑", library.Plans[0].Groups[0].ActionName);
    }

    [TestMethod]
    public void LoadTrainingPlan_ReadsSavedPlan()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);
        TrainingPlan savedPlan = new()
        {
            Groups =
            [
                new TrainingGroup
                {
                    Order = 1,
                    ActionName = "俯卧撑",
                    TrainingSeconds = 45,
                    RestSeconds = 20
                }
            ]
        };

        store.SaveTrainingPlan(savedPlan);

        TrainingPlan loadedPlan = store.LoadTrainingPlan();
        Assert.AreEqual(1, loadedPlan.Groups.Count);
        Assert.AreEqual("俯卧撑", loadedPlan.Groups[0].ActionName);
        Assert.AreEqual(45, loadedPlan.Groups[0].TrainingSeconds);
        Assert.AreEqual(20, loadedPlan.Groups[0].RestSeconds);

        string json = File.ReadAllText(Path.Combine(projectRoot, "data", "current-plan.json"));
        StringAssert.Contains(json, "\"Plans\"");
        StringAssert.Contains(json, "俯卧撑");
    }

    [TestMethod]
    public void EnsureDefaults_DoesNotOverwriteExistingFiles()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(dataRoot);
        string existingPlanJson = """
            {
              "Groups": [
                {
                  "Order": 7,
                  "ActionName": "俯卧撑",
                  "TrainingSeconds": 50,
                  "RestSeconds": 10
                }
              ]
            }
            """;
        File.WriteAllText(Path.Combine(dataRoot, "current-plan.json"), existingPlanJson);

        JsonDataStore store = new(projectRoot);
        store.EnsureDefaults();

        Assert.AreEqual(existingPlanJson, File.ReadAllText(Path.Combine(dataRoot, "current-plan.json")));
    }

    [TestMethod]
    public void SaveSettings_ClampsPreparationSecondsToRange()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);

        store.SaveSettings(new AppSettings
        {
            PreparationSeconds = 99,
            LastPage = "History",
            CompactAlwaysOnTop = true,
            UseDarkMode = true,
            CompactLeft = 120.4,
            CompactTop = 88.6,
            CompactWidth = 460.2,
            CompactHeight = 530.8,
            IsCompactMode = true
        });

        AppSettings highClamped = store.LoadSettings();
        Assert.AreEqual(60, highClamped.PreparationSeconds);
        Assert.AreEqual("History", highClamped.LastPage);
        Assert.IsTrue(highClamped.CompactAlwaysOnTop);
        Assert.IsTrue(highClamped.UseDarkMode);
        Assert.AreEqual(120, highClamped.CompactLeft);
        Assert.AreEqual(89, highClamped.CompactTop);
        Assert.AreEqual(460, highClamped.CompactWidth);
        Assert.AreEqual(531, highClamped.CompactHeight);
        Assert.IsTrue(highClamped.IsCompactMode);

        store.SaveSettings(new AppSettings
        {
            PreparationSeconds = -3,
            LastPage = "Timer",
            CompactAlwaysOnTop = false,
            UseDarkMode = false,
            CompactWidth = 2000,
            CompactHeight = 2000
        });

        AppSettings lowClamped = store.LoadSettings();
        Assert.AreEqual(0, lowClamped.PreparationSeconds);
        Assert.IsFalse(lowClamped.UseDarkMode);
        Assert.AreEqual(1200, lowClamped.CompactWidth);
        Assert.AreEqual(1400, lowClamped.CompactHeight);
    }

    [TestMethod]
    public void LoadSettings_CreatesOnlySettingsFileWhenMissing()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        JsonDataStore store = new(projectRoot);

        AppSettings settings = store.LoadSettings();

        Assert.AreEqual(5, settings.PreparationSeconds);
        Assert.IsFalse(settings.UseDarkMode);
        Assert.IsNull(settings.CompactWidth);
        Assert.IsTrue(File.Exists(Path.Combine(dataRoot, "settings.json")));
        Assert.IsFalse(File.Exists(Path.Combine(dataRoot, "current-plan.json")));
        Assert.IsFalse(File.Exists(Path.Combine(dataRoot, "workout-history.json")));
    }

    [TestMethod]
    public void LoadTrainingPlan_ReturnsActivePlanFromLibrary()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);
        TrainingPlan firstPlan = new()
        {
            Name = "上肢",
            Groups =
            [
                new TrainingGroup { Order = 1, ActionName = "俯卧撑", TrainingSeconds = 30, RestSeconds = 15 }
            ]
        };
        TrainingPlan secondPlan = new()
        {
            Name = "下肢",
            Groups =
            [
                new TrainingGroup { Order = 1, ActionName = "深蹲", TrainingSeconds = 40, RestSeconds = 20 }
            ]
        };
        store.SaveTrainingPlanLibrary(new TrainingPlanLibrary
        {
            ActivePlanId = secondPlan.Id,
            Plans = [firstPlan, secondPlan]
        });

        TrainingPlan activePlan = store.LoadTrainingPlan();

        Assert.AreEqual("下肢", activePlan.Name);
        Assert.AreEqual("深蹲", activePlan.Groups[0].ActionName);
    }

    [TestMethod]
    public void EnsureDefaults_WritesPlanLibraryFormatForNewData()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);

        store.EnsureDefaults();

        string json = File.ReadAllText(Path.Combine(projectRoot, "data", "current-plan.json"));
        StringAssert.Contains(json, "\"Plans\"");
        StringAssert.Contains(json, "\"ActivePlanId\"");
    }

    [TestMethod]
    public void SaveMethods_DoNotLeaveTempFilesAfterSuccessfulWrites()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        JsonDataStore store = new(projectRoot);

        store.SaveTrainingPlan(new TrainingPlan
        {
            Groups =
            [
                new TrainingGroup
                {
                    Order = 1,
                    ActionName = "俯卧撑",
                    TrainingSeconds = 45,
                    RestSeconds = 20
                }
            ]
        });
        store.SaveSettings(new AppSettings
        {
            PreparationSeconds = 12,
            LastPage = "Timer",
            CompactAlwaysOnTop = false,
            UseDarkMode = true,
            CompactLeft = 12,
            CompactTop = 34,
            CompactWidth = 520,
            CompactHeight = 680
        });
        store.SaveHistory([]);

        Assert.AreEqual(0, Directory.GetFiles(dataRoot, "*.tmp").Length);
    }

    [TestMethod]
    public void InvalidJson_DoesNotOverwriteFileAndThrowsInvalidDataException()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(dataRoot);
        string planPath = Path.Combine(dataRoot, "current-plan.json");
        const string invalidJson = "{ invalid json";
        File.WriteAllText(planPath, invalidJson);

        JsonDataStore store = new(projectRoot);
        InvalidDataException exception = Assert.ThrowsException<InvalidDataException>(() => store.LoadTrainingPlan());

        StringAssert.Contains(exception.Message, planPath);
        Assert.AreEqual(invalidJson, File.ReadAllText(planPath));
    }

    [TestMethod]
    public void SaveHistory_And_LoadHistory_RoundTripWorkoutRecords()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = new(projectRoot);
        Guid recordId = Guid.Parse("89cb5c63-2192-4d45-a6d7-3a849efb0f0d");
        List<WorkoutRecord> records =
        [
            new WorkoutRecord
            {
                Id = recordId,
                StartedAt = DateTimeOffset.Parse("2026-05-25T20:00:00+08:00"),
                EndedAt = DateTimeOffset.Parse("2026-05-25T20:02:00+08:00"),
                IsCompleted = true,
                TotalElapsedSeconds = 120,
                ActualTrainingSeconds = 90,
                RestSeconds = 30,
                PauseSeconds = 0,
                CompletedGroups = 2,
                TotalPlannedGroups = 2,
                PreparationSeconds = 5,
                PlanSnapshot = new TrainingPlan
                {
                    Groups =
                    [
                        new TrainingGroup
                        {
                            Order = 1,
                            ActionName = "俯卧撑",
                            TrainingSeconds = 45,
                            RestSeconds = 15
                        }
                    ]
                },
                Groups =
                [
                    new WorkoutGroupRecord
                    {
                        GroupNumber = 1,
                        ActionName = "俯卧撑",
                        PlannedTrainingSeconds = 45,
                        ActualTrainingSeconds = 45,
                        PlannedRestSeconds = 15,
                        ActualRestSeconds = 15,
                        TrainingSkipped = false,
                        RestSkipped = false
                    }
                ]
            }
        ];

        store.SaveHistory(records);

        List<WorkoutRecord> loadedRecords = store.LoadHistory();
        Assert.AreEqual(1, loadedRecords.Count);
        Assert.AreEqual(recordId, loadedRecords[0].Id);
        Assert.AreEqual("俯卧撑", loadedRecords[0].Groups[0].ActionName);
        Assert.AreEqual(45, loadedRecords[0].Groups[0].ActualTrainingSeconds);
        Assert.IsTrue(loadedRecords[0].IsCompleted);

        string json = File.ReadAllText(Path.Combine(projectRoot, "data", "workout-history.json"));
        StringAssert.Contains(json, "俯卧撑");
    }

    private static string CreateProjectRoot()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "FitnessTimer.JsonDataStoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        return projectRoot;
    }
}
