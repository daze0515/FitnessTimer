using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using FitnessTimer.App.Models;
using FitnessTimer.App.Services;

namespace FitnessTimer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly JsonDataStore store;
    private readonly HistoryService historyService;
    private readonly StatisticsService statisticsService = new();
    private readonly List<string> dataLoadWarnings = [];
    private StatisticsSummary currentStatistics = new();
    private bool showMonthStatistics;
    private TrainingPlan selectedPlan = new();

    public MainViewModel(JsonDataStore store, ISoundService soundService)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(soundService);

        this.store.EnsureDefaults();
        historyService = new HistoryService(this.store);

        PlanLibrary = LoadTrainingPlanLibrarySafely();
        Plans = new ObservableCollection<TrainingPlan>(PlanLibrary.Plans);
        selectedPlan = Plans.FirstOrDefault(plan => plan.Id == PlanLibrary.ActivePlanId) ?? Plans[0];
        Groups = new ObservableCollection<TrainingGroup>(selectedPlan.Groups);
        Settings = LoadSettingsSafely();
        History = new ObservableCollection<WorkoutRecord>(LoadHistorySafely());
        RefreshStatistics();

        AddPlanCommand = new RelayCommand(_ => AddPlan());
        DeletePlanCommand = new RelayCommand(DeletePlan, CanDeletePlan);
        AddGroupCommand = new RelayCommand(_ => AddGroup());
        DeleteGroupCommand = new RelayCommand(DeleteGroup, CanDeleteGroup);
        DeleteHistoryCommand = new RelayCommand(DeleteHistory, CanDeleteHistory);
        MoveGroupUpCommand = new RelayCommand(MoveGroupUp, CanMoveGroupUp);
        MoveGroupDownCommand = new RelayCommand(MoveGroupDown, CanMoveGroupDown);
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
    }

    public TrainingPlanLibrary PlanLibrary { get; }

    public ObservableCollection<TrainingPlan> Plans { get; }

    public TrainingPlan SelectedPlan
    {
        get => selectedPlan;
        set
        {
            if (ReferenceEquals(selectedPlan, value) || value is null || !Plans.Contains(value))
            {
                return;
            }

            SavePlan();
            selectedPlan = value;
            PlanLibrary.ActivePlanId = selectedPlan.Id;
            ReplaceGroupsFromSelectedPlan();
            SavePlanLibrary();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Plan));
        }
    }

    public TrainingPlan Plan => SelectedPlan;

    public ObservableCollection<TrainingGroup> Groups { get; }

    public AppSettings Settings { get; }

    public ObservableCollection<WorkoutRecord> History { get; }

    public string DataLoadWarning => string.Join(Environment.NewLine, dataLoadWarnings);

    public StatisticsSummary CurrentStatistics
    {
        get => currentStatistics;
        private set
        {
            currentStatistics = value;
            OnPropertyChanged();
        }
    }

    public bool ShowMonthStatistics
    {
        get => showMonthStatistics;
        set
        {
            if (showMonthStatistics == value)
            {
                return;
            }

            showMonthStatistics = value;
            OnPropertyChanged();
            RefreshStatistics();
        }
    }

    public ICommand AddGroupCommand { get; }

    public ICommand AddPlanCommand { get; }

    public ICommand DeletePlanCommand { get; }

    public ICommand DeleteGroupCommand { get; }

    public ICommand DeleteHistoryCommand { get; }

    public ICommand MoveGroupUpCommand { get; }

    public ICommand MoveGroupDownCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    private void AddPlan()
    {
        SavePlan();

        int number = Plans.Count + 1;
        TrainingPlan plan = new()
        {
            Name = $"训练方案 {number}",
            Groups =
            [
                new TrainingGroup
                {
                    Order = 1,
                    ActionName = "动作 1",
                    TrainingSeconds = 30,
                    RestSeconds = 15
                }
            ]
        };

        Plans.Add(plan);
        PlanLibrary.Plans = Plans.ToList();
        SelectedPlan = plan;
        SavePlanLibrary();
        RaisePlanCanExecuteChanged();
    }

    private void DeletePlan(object? parameter)
    {
        if (parameter is not TrainingPlan plan || !Plans.Contains(plan) || Plans.Count <= 1)
        {
            return;
        }

        int index = Plans.IndexOf(plan);
        Plans.Remove(plan);
        PlanLibrary.Plans = Plans.ToList();

        if (ReferenceEquals(selectedPlan, plan))
        {
            int nextIndex = Math.Clamp(index, 0, Plans.Count - 1);
            selectedPlan = Plans[nextIndex];
            PlanLibrary.ActivePlanId = selectedPlan.Id;
            ReplaceGroupsFromSelectedPlan();
            OnPropertyChanged(nameof(SelectedPlan));
            OnPropertyChanged(nameof(Plan));
        }

        SavePlanLibrary();
        RaisePlanCanExecuteChanged();
    }

    private void AddGroup()
    {
        int order = Groups.Count + 1;
        Groups.Add(new TrainingGroup
        {
            Order = order,
            ActionName = $"动作 {order}",
            TrainingSeconds = 30,
            RestSeconds = 15
        });

        SavePlan();
    }

    private void DeleteGroup(object? parameter)
    {
        if (parameter is not TrainingGroup group)
        {
            return;
        }

        if (!Groups.Remove(group))
        {
            return;
        }

        ReplaceGroupsWithRenumberedCopies();
        SavePlan();
    }

    private void MoveGroupUp(object? parameter)
    {
        if (parameter is not TrainingGroup group)
        {
            return;
        }

        int index = Groups.IndexOf(group);
        if (index <= 0)
        {
            return;
        }

        Groups.Move(index, index - 1);
        ReplaceGroupsWithRenumberedCopies();
        SavePlan();
    }

    private void MoveGroupDown(object? parameter)
    {
        if (parameter is not TrainingGroup group)
        {
            return;
        }

        int index = Groups.IndexOf(group);
        if (index < 0 || index >= Groups.Count - 1)
        {
            return;
        }

        Groups.Move(index, index + 1);
        ReplaceGroupsWithRenumberedCopies();
        SavePlan();
    }

    public void SavePlan()
    {
        NormalizeGroups();
        selectedPlan.Groups = Groups.ToList();
        selectedPlan.Name = string.IsNullOrWhiteSpace(selectedPlan.Name)
            ? "未命名方案"
            : selectedPlan.Name.Trim();
        PlanLibrary.ActivePlanId = selectedPlan.Id;
        PlanLibrary.Plans = Plans.ToList();
        SavePlanLibrary();
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(SelectedPlan));
        OnPropertyChanged(nameof(Plans));
        OnPropertyChanged(nameof(Groups));
        RaiseMovementCanExecuteChanged();
        RaisePlanCanExecuteChanged();
    }

    public void SaveWorkoutRecord(WorkoutRecord record)
    {
        historyService.AddRecord(record);
        ReloadHistory();
    }

    public void DeleteHistoryRecord(WorkoutRecord record)
    {
        if (!historyService.DeleteRecord(record.Id))
        {
            return;
        }

        History.Remove(record);
        RefreshStatistics();
        RaiseCanExecuteChanged(DeleteHistoryCommand);
    }

    public void ReloadHistory()
    {
        History.Clear();

        foreach (WorkoutRecord record in historyService.LoadHistory())
        {
            History.Add(record);
        }

        RefreshStatistics();
    }

    private void SaveSettings()
    {
        store.SaveSettings(Settings);
        Settings.PreparationSeconds = store.LoadSettings().PreparationSeconds;
        OnPropertyChanged(nameof(Settings));
    }

    private TrainingPlanLibrary LoadTrainingPlanLibrarySafely()
    {
        try
        {
            return store.LoadTrainingPlanLibrary();
        }
        catch (InvalidDataException exception)
        {
            dataLoadWarnings.Add($"current-plan.json 读取失败，已保留原文件并临时使用默认训练计划。{exception.Message}");
            return TrainingPlanLibrary.CreateDefault();
        }
    }

    private AppSettings LoadSettingsSafely()
    {
        try
        {
            return store.LoadSettings();
        }
        catch (InvalidDataException exception)
        {
            dataLoadWarnings.Add($"settings.json 读取失败，已保留原文件并临时使用默认设置。{exception.Message}");
            return AppSettings.CreateDefault();
        }
    }

    private List<WorkoutRecord> LoadHistorySafely()
    {
        try
        {
            return historyService.LoadHistory();
        }
        catch (InvalidDataException exception)
        {
            dataLoadWarnings.Add($"workout-history.json 读取失败，已保留原文件并临时使用空历史。{exception.Message}");
            return [];
        }
    }

    private bool CanDeleteGroup(object? parameter)
    {
        return parameter is TrainingGroup group && Groups.Contains(group);
    }

    private bool CanDeleteHistory(object? parameter)
    {
        return parameter is WorkoutRecord record && History.Contains(record);
    }

    private bool CanDeletePlan(object? parameter)
    {
        return parameter is TrainingPlan plan && Plans.Contains(plan) && Plans.Count > 1;
    }

    private bool CanMoveGroupUp(object? parameter)
    {
        return parameter is TrainingGroup group && Groups.IndexOf(group) > 0;
    }

    private bool CanMoveGroupDown(object? parameter)
    {
        return parameter is TrainingGroup group &&
            Groups.IndexOf(group) >= 0 &&
            Groups.IndexOf(group) < Groups.Count - 1;
    }

    private void ReplaceGroupsWithRenumberedCopies()
    {
        for (int index = 0; index < Groups.Count; index++)
        {
            TrainingGroup current = Groups[index];
            Groups[index] = new TrainingGroup
            {
                Order = index + 1,
                ActionName = current.ActionName,
                TrainingSeconds = current.TrainingSeconds,
                RestSeconds = current.RestSeconds
            };
        }
    }

    private void NormalizeGroups()
    {
        for (int index = 0; index < Groups.Count; index++)
        {
            TrainingGroup group = Groups[index];
            group.Order = index + 1;
            group.ActionName = string.IsNullOrWhiteSpace(group.ActionName)
                ? $"动作 {index + 1}"
                : group.ActionName.Trim();
            group.TrainingSeconds = Math.Max(0, group.TrainingSeconds);
            group.RestSeconds = Math.Max(0, group.RestSeconds);
        }
    }

    private void RefreshStatistics()
    {
        List<WorkoutRecord> history = History.ToList();
        CurrentStatistics = ShowMonthStatistics
            ? statisticsService.BuildMonthStats(history, DateTime.Today)
            : statisticsService.BuildWeekStats(history, DateTime.Today);
    }

    private void RaiseMovementCanExecuteChanged()
    {
        RaiseCanExecuteChanged(DeleteGroupCommand);
        RaiseCanExecuteChanged(DeleteHistoryCommand);
        RaiseCanExecuteChanged(MoveGroupUpCommand);
        RaiseCanExecuteChanged(MoveGroupDownCommand);
    }

    private void DeleteHistory(object? parameter)
    {
        if (parameter is WorkoutRecord record)
        {
            DeleteHistoryRecord(record);
        }
    }

    private void RaisePlanCanExecuteChanged()
    {
        RaiseCanExecuteChanged(DeletePlanCommand);
    }

    private void ReplaceGroupsFromSelectedPlan()
    {
        Groups.Clear();

        foreach (TrainingGroup group in selectedPlan.Groups)
        {
            Groups.Add(group);
        }

        RaiseMovementCanExecuteChanged();
    }

    private void SavePlanLibrary()
    {
        PlanLibrary.Plans = Plans.ToList();
        store.SaveTrainingPlanLibrary(PlanLibrary);
    }

    private static void RaiseCanExecuteChanged(ICommand command)
    {
        if (command is RelayCommand relayCommand)
        {
            relayCommand.RaiseCanExecuteChanged();
        }
    }
}
