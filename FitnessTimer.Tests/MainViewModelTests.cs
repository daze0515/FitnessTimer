using FitnessTimer.App.Models;
using FitnessTimer.App.Services;
using FitnessTimer.App.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Specialized;

namespace FitnessTimer.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void AddGroupCommand_AppendsEditableGroupWithExpectedLabelAndPersists()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());

        viewModel.AddGroupCommand.Execute(null);

        Assert.AreEqual(4, viewModel.Plan.Groups.Count);
        TrainingGroup addedGroup = viewModel.Plan.Groups[3];
        Assert.AreEqual(4, addedGroup.Order);
        Assert.AreEqual("动作 4", addedGroup.ActionName);
        Assert.AreEqual(30, addedGroup.TrainingSeconds);
        Assert.AreEqual(15, addedGroup.RestSeconds);

        TrainingPlan persistedPlan = store.LoadTrainingPlan();
        Assert.AreEqual(4, persistedPlan.Groups.Count);
        Assert.AreEqual("动作 4", persistedPlan.Groups[3].ActionName);
    }

    [TestMethod]
    public void AddGroupCommand_RaisesBindableGroupsCollectionChange()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        List<NotifyCollectionChangedAction> actions = [];
        viewModel.Groups.CollectionChanged += (_, args) => actions.Add(args.Action);

        viewModel.AddGroupCommand.Execute(null);

        CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Add);
        Assert.AreEqual(4, viewModel.Groups.Count);
        Assert.AreSame(viewModel.Groups[3], viewModel.Plan.Groups[3]);
    }

    [TestMethod]
    public void DeleteGroupCommand_RemovesSelectedGroupRenumbersAndPersists()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());

        List<NotifyCollectionChangedAction> actions = [];
        viewModel.Groups.CollectionChanged += (_, args) => actions.Add(args.Action);

        viewModel.DeleteGroupCommand.Execute(viewModel.Groups[1]);

        AssertGroupOrder(viewModel.Plan, "动作 1", "动作 3");
        AssertGroupOrder(viewModel.Groups, "动作 1", "动作 3");
        CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Remove);
        AssertContainsReplaceOrReset(actions);

        TrainingPlan persistedPlan = store.LoadTrainingPlan();
        AssertGroupOrder(persistedPlan, "动作 1", "动作 3");
    }

    [TestMethod]
    public void MoveGroupUpCommand_ReordersSelectedGroupRenumbersAndPersists()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());

        List<NotifyCollectionChangedAction> actions = [];
        viewModel.Groups.CollectionChanged += (_, args) => actions.Add(args.Action);

        viewModel.MoveGroupUpCommand.Execute(viewModel.Groups[2]);

        AssertGroupOrder(viewModel.Plan, "动作 1", "动作 3", "动作 2");
        AssertGroupOrder(viewModel.Groups, "动作 1", "动作 3", "动作 2");
        CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Move);
        AssertContainsReplaceOrReset(actions);

        TrainingPlan persistedPlan = store.LoadTrainingPlan();
        AssertGroupOrder(persistedPlan, "动作 1", "动作 3", "动作 2");
    }

    [TestMethod]
    public void MoveGroupDownCommand_ReordersSelectedGroupRenumbersAndPersists()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());

        viewModel.MoveGroupDownCommand.Execute(viewModel.Groups[0]);

        AssertGroupOrder(viewModel.Plan, "动作 2", "动作 1", "动作 3");
        AssertGroupOrder(viewModel.Groups, "动作 2", "动作 1", "动作 3");

        TrainingPlan persistedPlan = store.LoadTrainingPlan();
        AssertGroupOrder(persistedPlan, "动作 2", "动作 1", "动作 3");
    }

    [TestMethod]
    public void SaveSettingsCommand_PersistsClampedPreparationSeconds()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        viewModel.Settings.PreparationSeconds = 99;
        viewModel.Settings.UseDarkMode = true;
        viewModel.Settings.CompactLeft = 128;
        viewModel.Settings.CompactTop = 96;
        viewModel.Settings.CompactWidth = 520;
        viewModel.Settings.CompactHeight = 680;

        viewModel.SaveSettingsCommand.Execute(null);

        AppSettings persistedSettings = store.LoadSettings();
        Assert.AreEqual(60, persistedSettings.PreparationSeconds);
        Assert.AreEqual(60, viewModel.Settings.PreparationSeconds);
        Assert.IsTrue(persistedSettings.UseDarkMode);
        Assert.IsTrue(viewModel.Settings.UseDarkMode);
        Assert.AreEqual(128, persistedSettings.CompactLeft);
        Assert.AreEqual(96, persistedSettings.CompactTop);
        Assert.AreEqual(520, persistedSettings.CompactWidth);
        Assert.AreEqual(680, persistedSettings.CompactHeight);
    }

    [TestMethod]
    public void AddPlanCommand_AddsPlanSelectsItAndPersists()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());

        viewModel.AddPlanCommand.Execute(null);

        Assert.AreEqual(2, viewModel.Plans.Count);
        Assert.AreSame(viewModel.Plans[1], viewModel.SelectedPlan);
        Assert.AreEqual("训练方案 2", viewModel.SelectedPlan.Name);
        Assert.AreEqual(1, viewModel.Groups.Count);
        Assert.AreEqual("动作 1", viewModel.Groups[0].ActionName);

        TrainingPlanLibrary persistedLibrary = store.LoadTrainingPlanLibrary();
        Assert.AreEqual(2, persistedLibrary.Plans.Count);
        Assert.AreEqual(viewModel.SelectedPlan.Id, persistedLibrary.ActivePlanId);
    }

    [TestMethod]
    public void SwitchingSelectedPlanKeepsGroupListsSeparate()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        TrainingPlan firstPlan = viewModel.SelectedPlan;

        viewModel.AddPlanCommand.Execute(null);
        TrainingPlan secondPlan = viewModel.SelectedPlan;
        viewModel.Groups[0].ActionName = "深蹲";
        viewModel.SavePlan();

        viewModel.SelectedPlan = firstPlan;

        Assert.AreEqual(3, viewModel.Groups.Count);
        Assert.AreEqual("动作 1", viewModel.Groups[0].ActionName);

        viewModel.SelectedPlan = secondPlan;

        Assert.AreEqual(1, viewModel.Groups.Count);
        Assert.AreEqual("深蹲", viewModel.Groups[0].ActionName);
    }

    [TestMethod]
    public void DeletePlanCommand_RemovesSelectedPlanAndSelectsFallback()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        TrainingPlan firstPlan = viewModel.SelectedPlan;
        viewModel.AddPlanCommand.Execute(null);
        TrainingPlan secondPlan = viewModel.SelectedPlan;

        viewModel.DeletePlanCommand.Execute(secondPlan);

        Assert.AreEqual(1, viewModel.Plans.Count);
        Assert.AreSame(firstPlan, viewModel.SelectedPlan);
        Assert.IsFalse(viewModel.DeletePlanCommand.CanExecute(firstPlan));
        Assert.AreEqual(3, viewModel.Groups.Count);

        TrainingPlanLibrary persistedLibrary = store.LoadTrainingPlanLibrary();
        Assert.AreEqual(1, persistedLibrary.Plans.Count);
        Assert.AreEqual(firstPlan.Id, persistedLibrary.ActivePlanId);
    }

    [TestMethod]
    public void GroupCommands_UpdateCanExecuteBoundariesAfterMutations()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        int moveUpChanged = 0;
        viewModel.MoveGroupUpCommand.CanExecuteChanged += (_, _) => moveUpChanged++;

        TrainingGroup firstGroup = viewModel.Groups[0];
        Assert.IsFalse(viewModel.MoveGroupUpCommand.CanExecute(firstGroup));

        viewModel.MoveGroupDownCommand.Execute(firstGroup);

        Assert.IsTrue(viewModel.MoveGroupUpCommand.CanExecute(viewModel.Groups[1]));
        Assert.IsTrue(moveUpChanged > 0);
    }

    [TestMethod]
    public void DeleteGroupCommand_CanExecuteIsFalseForGroupNotInBindableCollection()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        TrainingGroup outsideGroup = new()
        {
            Order = 99,
            ActionName = "动作 99",
            TrainingSeconds = 30,
            RestSeconds = 15
        };

        Assert.IsFalse(viewModel.DeleteGroupCommand.CanExecute(outsideGroup));
    }

    [TestMethod]
    public void Constructor_LoadsHistoryNewestFirst()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        Guid olderId = Guid.Parse("2f6f58a0-80f8-4181-8817-b20335d99c9a");
        Guid newerId = Guid.Parse("6977d4b2-4774-4946-973a-3d3320bc68d9");
        store.SaveHistory(
        [
            new WorkoutRecord
            {
                Id = olderId,
                StartedAt = DateTimeOffset.Parse("2026-05-25T09:00:00+08:00")
            },
            new WorkoutRecord
            {
                Id = newerId,
                StartedAt = DateTimeOffset.Parse("2026-05-25T10:00:00+08:00")
            }
        ]);

        MainViewModel viewModel = new(store, new RecordingSoundService());

        Assert.AreEqual(2, viewModel.History.Count);
        Assert.AreEqual(newerId, viewModel.History[0].Id);
        Assert.AreEqual(olderId, viewModel.History[1].Id);
    }

    [TestMethod]
    public void Constructor_UsesDefaultPlanAndKeepsInvalidPlanFile()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(dataRoot);
        const string invalidJson = "{ invalid plan";
        File.WriteAllText(Path.Combine(dataRoot, "current-plan.json"), invalidJson);
        JsonDataStore store = new(projectRoot);

        MainViewModel viewModel = new(store, new RecordingSoundService());

        Assert.AreEqual(3, viewModel.Groups.Count);
        Assert.AreEqual("动作 1", viewModel.Groups[0].ActionName);
        StringAssert.Contains(viewModel.DataLoadWarning, "current-plan.json");
        Assert.AreEqual(invalidJson, File.ReadAllText(Path.Combine(dataRoot, "current-plan.json")));
    }

    [TestMethod]
    public void Constructor_UsesDefaultSettingsAndEmptyHistoryWhenJsonFilesAreInvalid()
    {
        string projectRoot = CreateProjectRoot();
        string dataRoot = Path.Combine(projectRoot, "data");
        Directory.CreateDirectory(dataRoot);
        File.WriteAllText(Path.Combine(dataRoot, "settings.json"), "{ invalid settings");
        File.WriteAllText(Path.Combine(dataRoot, "workout-history.json"), "{ invalid history");
        JsonDataStore store = CreateStoreWithPlan(projectRoot);

        MainViewModel viewModel = new(store, new RecordingSoundService());

        Assert.AreEqual(5, viewModel.Settings.PreparationSeconds);
        Assert.AreEqual(0, viewModel.History.Count);
        StringAssert.Contains(viewModel.DataLoadWarning, "settings.json");
        StringAssert.Contains(viewModel.DataLoadWarning, "workout-history.json");
        Assert.AreEqual("{ invalid settings", File.ReadAllText(Path.Combine(dataRoot, "settings.json")));
        Assert.AreEqual("{ invalid history", File.ReadAllText(Path.Combine(dataRoot, "workout-history.json")));
    }

    [TestMethod]
    public void Constructor_BuildsCurrentWeekStatisticsFromHistory()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        store.SaveHistory(
        [
            new WorkoutRecord
            {
                StartedAt = DateTimeOffset.Now,
                EndedAt = DateTimeOffset.Now.AddMinutes(10),
                IsCompleted = true,
                ActualTrainingSeconds = 600,
                RestSeconds = 120,
                TotalElapsedSeconds = 720
            }
        ]);

        MainViewModel viewModel = new(store, new RecordingSoundService());

        Assert.AreEqual(1, viewModel.CurrentStatistics.WorkoutCount);
        Assert.AreEqual(600, viewModel.CurrentStatistics.ActualTrainingSeconds);
        Assert.AreEqual(120, viewModel.CurrentStatistics.RestSeconds);
        Assert.AreEqual(7, viewModel.CurrentStatistics.Bars.Count);
    }

    [TestMethod]
    public void ShowMonthStatistics_RefreshesStatisticsWithMonthBars()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        store.SaveHistory(
        [
            new WorkoutRecord
            {
                StartedAt = DateTimeOffset.Now,
                EndedAt = DateTimeOffset.Now.AddMinutes(3),
                IsCompleted = true,
                ActualTrainingSeconds = 180,
                TotalElapsedSeconds = 180
            }
        ]);

        MainViewModel viewModel = new(store, new RecordingSoundService());

        viewModel.ShowMonthStatistics = true;

        Assert.AreEqual(DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month), viewModel.CurrentStatistics.Bars.Count);
        Assert.AreEqual(1, viewModel.CurrentStatistics.WorkoutCount);
    }

    [TestMethod]
    public void SaveWorkoutRecord_AddsHistoryAndRefreshesStatistics()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        WorkoutRecord record = new()
        {
            StartedAt = DateTimeOffset.Now,
            EndedAt = DateTimeOffset.Now.AddMinutes(12),
            IsCompleted = true,
            ActualTrainingSeconds = 600,
            RestSeconds = 120,
            TotalElapsedSeconds = 720
        };

        viewModel.SaveWorkoutRecord(record);

        Assert.AreEqual(1, viewModel.History.Count);
        Assert.AreEqual(1, viewModel.CurrentStatistics.WorkoutCount);
        Assert.AreEqual(600, viewModel.CurrentStatistics.ActualTrainingSeconds);
    }

    [TestMethod]
    public void DeleteHistoryRecord_RemovesRecordPersistsAndRefreshesStatistics()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        WorkoutRecord first = new()
        {
            Id = Guid.Parse("193c7d8a-329f-4b84-8d04-bc6fe00daa5a"),
            StartedAt = DateTimeOffset.Now.AddDays(-1),
            ActualTrainingSeconds = 120,
            TotalElapsedSeconds = 180
        };
        WorkoutRecord second = new()
        {
            Id = Guid.Parse("f2049279-5584-4a7f-a66d-74a8a71278ff"),
            StartedAt = DateTimeOffset.Now,
            ActualTrainingSeconds = 300,
            TotalElapsedSeconds = 360
        };
        store.SaveHistory([first, second]);
        MainViewModel viewModel = new(store, new RecordingSoundService());

        viewModel.DeleteHistoryRecord(viewModel.History.Single(record => record.Id == first.Id));

        Assert.AreEqual(1, viewModel.History.Count);
        Assert.AreEqual(second.Id, viewModel.History[0].Id);
        Assert.AreEqual(1, viewModel.CurrentStatistics.WorkoutCount);
        Assert.AreEqual(300, viewModel.CurrentStatistics.ActualTrainingSeconds);
        List<WorkoutRecord> persisted = store.LoadHistory();
        Assert.AreEqual(1, persisted.Count);
        Assert.AreEqual(second.Id, persisted[0].Id);
    }

    [TestMethod]
    public void SavePlan_RenumbersClampsAndPersistsGridEdits()
    {
        string projectRoot = CreateProjectRoot();
        JsonDataStore store = CreateStoreWithPlan(projectRoot);
        MainViewModel viewModel = new(store, new RecordingSoundService());
        viewModel.Groups[0].ActionName = " 俯卧撑 ";
        viewModel.Groups[0].TrainingSeconds = -4;
        viewModel.Groups[0].RestSeconds = -2;

        viewModel.SavePlan();

        TrainingPlan persistedPlan = store.LoadTrainingPlan();
        Assert.AreEqual("俯卧撑", persistedPlan.Groups[0].ActionName);
        Assert.AreEqual(0, persistedPlan.Groups[0].TrainingSeconds);
        Assert.AreEqual(0, persistedPlan.Groups[0].RestSeconds);
        Assert.AreEqual(1, persistedPlan.Groups[0].Order);
    }

    private static JsonDataStore CreateStoreWithPlan(string projectRoot)
    {
        JsonDataStore store = new(projectRoot);
        store.EnsureDefaults();
        store.SaveTrainingPlan(new TrainingPlan
        {
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
        });

        return store;
    }

    private static void AssertGroupOrder(TrainingPlan plan, params string[] actionNames)
    {
        AssertGroupOrder(plan.Groups, actionNames);
    }

    private static void AssertGroupOrder(IReadOnlyList<TrainingGroup> groups, params string[] actionNames)
    {
        Assert.AreEqual(actionNames.Length, groups.Count);

        for (int index = 0; index < actionNames.Length; index++)
        {
            Assert.AreEqual(index + 1, groups[index].Order);
            Assert.AreEqual(actionNames[index], groups[index].ActionName);
        }
    }

    private static void AssertContainsReplaceOrReset(IReadOnlyCollection<NotifyCollectionChangedAction> actions)
    {
        Assert.IsTrue(
            actions.Contains(NotifyCollectionChangedAction.Replace) ||
            actions.Contains(NotifyCollectionChangedAction.Reset),
            "Expected a Replace or Reset notification so row bindings refresh after renumbering.");
    }

    private static string CreateProjectRoot()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "FitnessTimer.MainViewModelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        return projectRoot;
    }

    private sealed class RecordingSoundService : ISoundService
    {
        public void PlayTick()
        {
        }

        public void PlayPreparationStart()
        {
        }

        public void PlayTrainingStart()
        {
        }

        public void PlayRestStart()
        {
        }

        public void PlayCompleted()
        {
        }

        public void Dispose()
        {
        }
    }
}
