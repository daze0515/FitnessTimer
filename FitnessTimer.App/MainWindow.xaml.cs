using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using FitnessTimer.App.Models;
using FitnessTimer.App.Services;
using FitnessTimer.App.Views;
using FitnessTimer.App.ViewModels;

namespace FitnessTimer.App;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
    private const int DwmWindowAttributeUseImmersiveDarkModeBefore20H1 = 19;
    private const double DefaultCompactWidth = 420;
    private const double DefaultCompactHeight = 520;
    private const double MinimumCompactWidth = 380;
    private const double MinimumCompactHeight = 480;
    private static readonly Regex DigitsOnlyRegex = new("^[0-9]+$", RegexOptions.Compiled);

    private readonly ISoundService soundService;
    private readonly MainViewModel viewModel;
    private readonly DispatcherTimer timer;
    private TimerEngine? engine;
    private TimerSnapshot snapshot = new();
    private bool completedRecordSaved;
    private bool isCompact;
    private double normalWidth;
    private double normalHeight;
    private WindowState normalWindowState;

    private static readonly GridLength NormalTimerColumnWidth = new(1.1, GridUnitType.Star);
    private static readonly GridLength NormalSpacerColumnWidth = new(22);
    private static readonly GridLength NormalDetailsColumnWidth = new(0.95, GridUnitType.Star);
    private static readonly Thickness NormalTimerBodyMargin = new(0, 18, 0, 18);

    public MainWindow()
    {
        InitializeComponent();

        JsonDataStore store = new(FindProjectRoot());
        soundService = new SystemSoundService();
        viewModel = new MainViewModel(store, soundService);
        DataContext = viewModel;

        timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += Timer_Tick;

        snapshot = new TimerSnapshot
        {
            Phase = TimerPhase.NotStarted,
            TotalGroups = viewModel.Groups.Count
        };
        normalWidth = Width;
        normalHeight = Height;
        normalWindowState = WindowState;
        Closed += MainWindow_Closed;
        SourceInitialized += MainWindow_SourceInitialized;
        ApplyTheme(viewModel.Settings.UseDarkMode);

        if (viewModel.Settings.IsCompactMode)
        {
            EnterCompactMode();
        }

        RestoreLastPage();
        UpdateTimerText();

        if (!string.IsNullOrWhiteSpace(viewModel.DataLoadWarning))
        {
            Dispatcher.BeginInvoke(new Action(ShowDataLoadWarning), DispatcherPriority.ApplicationIdle);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ClockText { get; private set; } = "00:00";

    public double TimerProgress { get; private set; }

    public string ClockHintText { get; private set; } = "准备开始";

    public string PhaseText { get; private set; } = "未开始";

    public string StatusText { get; private set; } = "设置计划后点击开始";

    public string GroupText { get; private set; } = "0 / 0";

    public string ActionText { get; private set; } = "等待训练";

    public string NextPhaseText { get; private set; } = "训练";

    public string FooterText { get; private set; } = "保持节奏，完成每一组。";

    public string StartPauseText { get; private set; } = "开始";

    public string CompactModeText { get; private set; } = "小窗模式";

    private void StartPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || snapshot.Phase is TimerPhase.NotStarted or TimerPhase.Completed or TimerPhase.Stopped)
        {
            StartWorkout();
            return;
        }

        if (snapshot.Phase == TimerPhase.Paused)
        {
            engine.Resume();
            snapshot = engine.Snapshot;
            timer.Start();
            UpdateTimerText();
            return;
        }

        engine.Pause();
        snapshot = engine.Snapshot;
        timer.Stop();
        UpdateTimerText();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || snapshot.Phase is TimerPhase.NotStarted or TimerPhase.Completed or TimerPhase.Stopped)
        {
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            "要停止当前训练吗？\n\n选择“是”保存为未完成记录；选择“否”直接丢弃；选择“取消”继续训练。",
            "停止训练",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
        {
            return;
        }

        timer.Stop();

        engine.Stop();
        snapshot = engine.Snapshot;

        if (result == MessageBoxResult.Yes)
        {
            SaveWorkoutRecord(isCompleted: false);
        }

        UpdateTimerText();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (engine is null || snapshot.Phase is not (TimerPhase.Training or TimerPhase.Resting))
        {
            return;
        }

        TimerPhase before = snapshot.Phase;
        engine.SkipCurrentPhase();
        snapshot = engine.Snapshot;
        PlayPhaseTransitionSounds(before, snapshot);
        HandleCompletedIfNeeded();
        UpdateTimerText();
    }

    private void CompactButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleCompactMode();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        viewModel.Settings.IsCompactMode = isCompact;
        SaveCompactWindowPlacementIfNeeded();
        timer.Stop();
        timer.Tick -= Timer_Tick;
        SourceInitialized -= MainWindow_SourceInitialized;
        soundService.Dispose();
        Topmost = false;
        engine = null;
        Application.Current.Shutdown();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplySystemTitleBarTheme(viewModel.Settings.UseDarkMode);
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        viewModel.SaveSettingsCommand.Execute(null);
        ApplyTheme(viewModel.Settings.UseDarkMode);
        Topmost = isCompact && viewModel.Settings.CompactAlwaysOnTop;
        MessageBox.Show("设置已保存。", "健身间歇计时器", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PlanGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(viewModel.SavePlan), DispatcherPriority.Background);
    }

    private void SecondsTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !DigitsOnlyRegex.IsMatch(e.Text);
    }

    private void SecondsTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        string pastedText = (string)e.DataObject.GetData(DataFormats.Text);
        if (!DigitsOnlyRegex.IsMatch(pastedText))
        {
            e.CancelCommand();
        }
    }

    private void PlanNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        viewModel.SavePlan();
    }

    private void DeleteHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WorkoutRecord record)
        {
            return;
        }

        string startedAt = record.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        MessageBoxResult result = MessageBox.Show(
            $"确定删除 {startedAt} 的训练记录吗？",
            "删除训练记录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        viewModel.DeleteHistoryRecord(record);
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabs ||
            MainTabs.SelectedItem is not TabItem tabItem ||
            tabItem.Tag is not string page)
        {
            return;
        }

        if (DataContext is not MainViewModel currentViewModel)
        {
            return;
        }

        currentViewModel.Settings.LastPage = page;
        currentViewModel.SaveSettingsCommand.Execute(null);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (engine is null)
        {
            return;
        }

        TimerPhase before = snapshot.Phase;
        engine.Tick();
        snapshot = engine.Snapshot;
        PlayPhaseTransitionSounds(before, snapshot);
        PlayFinalTickIfNeeded(snapshot);
        HandleCompletedIfNeeded();
        UpdateTimerText();
    }

    private void StartWorkout()
    {
        CommitPlanForTimer();

        engine = new TimerEngine(viewModel.Plan, viewModel.Settings.PreparationSeconds);
        completedRecordSaved = false;
        engine.Start(DateTimeOffset.Now);
        snapshot = engine.Snapshot;

        PlayPhaseSoundIfActive(snapshot.Phase);
        HandleCompletedIfNeeded();

        if (snapshot.Phase is not TimerPhase.Completed)
        {
            timer.Start();
        }

        UpdateTimerText();
    }

    private void CommitPlanForTimer()
    {
        PlanGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PlanGrid.CommitEdit(DataGridEditingUnit.Row, true);
        BindingOperations.GetBindingExpression(PlanGrid, ItemsControl.ItemsSourceProperty)?.UpdateSource();
        viewModel.SavePlan();
    }

    private void HandleCompletedIfNeeded()
    {
        if (engine is null || snapshot.Phase != TimerPhase.Completed || completedRecordSaved)
        {
            return;
        }

        timer.Stop();
        SaveWorkoutRecord(isCompleted: true);
        completedRecordSaved = true;
        soundService.PlayCompleted();
    }

    private void SaveWorkoutRecord(bool isCompleted)
    {
        if (engine is null)
        {
            return;
        }

        WorkoutRecord record = engine.CreateRecord(isCompleted, DateTimeOffset.Now);
        viewModel.SaveWorkoutRecord(record);
    }

    private void PlayPhaseTransitionSounds(TimerPhase before, TimerSnapshot current)
    {
        if (before != current.Phase)
        {
            PlayPhaseSoundIfActive(current.Phase);
        }
    }

    private void PlayPhaseSoundIfActive(TimerPhase phase)
    {
        switch (phase)
        {
            case TimerPhase.Preparation:
                soundService.PlayPreparationStart();
                break;
            case TimerPhase.Training:
                soundService.PlayTrainingStart();
                break;
            case TimerPhase.Resting:
                soundService.PlayRestStart();
                break;
        }
    }

    private void PlayFinalTickIfNeeded(TimerSnapshot current)
    {
        if (current.Phase is TimerPhase.Preparation or TimerPhase.Training or TimerPhase.Resting &&
            current.RemainingSeconds is > 0 and <= 3)
        {
            soundService.PlayTick();
        }
    }

    private void ToggleCompactMode()
    {
        if (!isCompact)
        {
            EnterCompactMode();
            return;
        }

        ExitCompactMode();
    }

    private void EnterCompactMode()
    {
        normalWidth = Width;
        normalHeight = Height;
        normalWindowState = WindowState;
        WindowState = WindowState.Normal;
        ApplyCompactLayout();
        RestoreCompactWindowPlacement();
        Topmost = viewModel.Settings.CompactAlwaysOnTop;
        isCompact = true;
        UpdateCompactModeText();
        viewModel.Settings.IsCompactMode = true;
        viewModel.SaveSettingsCommand.Execute(null);
    }

    private void ExitCompactMode()
    {
        viewModel.Settings.IsCompactMode = false;
        SaveCompactWindowPlacementIfNeeded();
        ApplyNormalLayout();
        Width = normalWidth;
        Height = normalHeight;
        WindowState = normalWindowState;
        Topmost = false;
        isCompact = false;
        UpdateCompactModeText();
    }

    private void ApplyCompactLayout()
    {
        DetailsPanel.Visibility = Visibility.Collapsed;
        TimerColumn.Width = new GridLength(1, GridUnitType.Star);
        TimerColumn.MinWidth = 0;
        SpacerColumn.Width = new GridLength(0);
        DetailsColumn.Width = new GridLength(0);
        DetailsColumn.MinWidth = 0;

        MinWidth = MinimumCompactWidth;
        MinHeight = MinimumCompactHeight;
        Width = DefaultCompactWidth;
        Height = DefaultCompactHeight;

        LayoutRoot.Margin = new Thickness(10);
        TimerPanel.Padding = new Thickness(18);
        HeaderTextPanel.Visibility = Visibility.Collapsed;
        PhaseBadge.HorizontalAlignment = HorizontalAlignment.Center;
        PhaseBadge.Padding = new Thickness(12, 7, 12, 7);
        TimerBodyGrid.Margin = new Thickness(0, 10, 0, 12);
        SummaryGrid.Visibility = Visibility.Collapsed;
        TimerCircle.Width = 270;
        TimerCircle.Height = 270;
        TimerProgressRing.Width = 270;
        TimerProgressRing.Height = 270;
        ClockTextBlock.FontSize = 64;
        ClockHintTextBlock.FontSize = 14;
        FooterTextBlock.Visibility = Visibility.Collapsed;
        TimerButtonsGrid.Columns = 2;
        StopButton.Visibility = Visibility.Collapsed;
        SkipButton.Visibility = Visibility.Collapsed;
    }

    private void ApplyNormalLayout()
    {
        DetailsPanel.Visibility = Visibility.Visible;
        TimerColumn.Width = NormalTimerColumnWidth;
        TimerColumn.MinWidth = 500;
        SpacerColumn.Width = NormalSpacerColumnWidth;
        DetailsColumn.Width = NormalDetailsColumnWidth;
        DetailsColumn.MinWidth = 460;

        MinWidth = 1040;
        MinHeight = 680;

        LayoutRoot.Margin = new Thickness(22);
        TimerPanel.Padding = new Thickness(30);
        HeaderTextPanel.Visibility = Visibility.Visible;
        TitleTextBlock.FontSize = 28;
        PhaseBadge.HorizontalAlignment = HorizontalAlignment.Right;
        PhaseBadge.Padding = new Thickness(14, 8, 14, 8);
        TimerBodyGrid.Margin = NormalTimerBodyMargin;
        SummaryGrid.Visibility = Visibility.Visible;
        SummaryGrid.Margin = new Thickness(0, 0, 0, 14);
        SetSummaryFontSize(20);
        TimerCircle.Width = 390;
        TimerCircle.Height = 390;
        TimerProgressRing.Width = 390;
        TimerProgressRing.Height = 390;
        ClockTextBlock.FontSize = 82;
        ClockHintTextBlock.FontSize = 16;
        FooterTextBlock.Visibility = Visibility.Visible;
        FooterTextBlock.Margin = new Thickness(0, 12, 0, 0);
        TimerButtonsGrid.Columns = 4;
        StopButton.Visibility = Visibility.Visible;
        SkipButton.Visibility = Visibility.Visible;
    }

    private void SetSummaryFontSize(double fontSize)
    {
        GroupSummaryTextBlock.FontSize = fontSize;
        ActionSummaryTextBlock.FontSize = fontSize;
        NextPhaseSummaryTextBlock.FontSize = fontSize;
    }

    private void RestoreCompactWindowPlacement()
    {
        AppSettings settings = viewModel.Settings;
        Width = Math.Max(MinWidth, settings.CompactWidth ?? DefaultCompactWidth);
        Height = Math.Max(MinHeight, settings.CompactHeight ?? DefaultCompactHeight);

        if (settings.CompactLeft.HasValue && settings.CompactTop.HasValue)
        {
            Left = settings.CompactLeft.Value;
            Top = settings.CompactTop.Value;
        }

        EnsureWindowIsOnScreen();
    }

    private void SaveCompactWindowPlacementIfNeeded()
    {
        if (!isCompact || WindowState != WindowState.Normal)
        {
            return;
        }

        viewModel.Settings.CompactLeft = Left;
        viewModel.Settings.CompactTop = Top;
        viewModel.Settings.CompactWidth = ActualWidth > 0 ? ActualWidth : Width;
        viewModel.Settings.CompactHeight = ActualHeight > 0 ? ActualHeight : Height;
        viewModel.SaveSettingsCommand.Execute(null);
    }

    private void EnsureWindowIsOnScreen()
    {
        Rect virtualScreen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        Rect windowBounds = new(Left, Top, Width, Height);
        if (virtualScreen.IntersectsWith(windowBounds))
        {
            return;
        }

        Left = virtualScreen.Left + 40;
        Top = virtualScreen.Top + 40;
    }

    private void UpdateCompactModeText()
    {
        CompactModeText = isCompact ? "退出小窗" : "小窗模式";
        OnPropertyChanged(nameof(CompactModeText));
    }

    private void UpdateTimerText()
    {
        ClockText = FormatClock(snapshot.RemainingSeconds);
        TimerProgress = CalculateTimerProgress(snapshot);
        PhaseText = GetPhaseText(snapshot.Phase);
        StatusText = GetStatusText(snapshot);
        GroupText = snapshot.TotalGroups == 0 ? "0 / 0" : $"{snapshot.CurrentGroupIndex} / {snapshot.TotalGroups}";
        ActionText = string.IsNullOrWhiteSpace(snapshot.CurrentActionName) ? "等待训练" : snapshot.CurrentActionName;
        NextPhaseText = string.IsNullOrWhiteSpace(snapshot.NextPhaseLabel) ? "训练" : snapshot.NextPhaseLabel;
        ClockHintText = GetClockHint(snapshot.Phase);
        FooterText = GetFooterText(snapshot);
        StartPauseText = snapshot.Phase switch
        {
            TimerPhase.Paused => "继续",
            TimerPhase.Preparation or TimerPhase.Training or TimerPhase.Resting => "暂停",
            _ => "开始"
        };

        OnPropertyChanged(nameof(ClockText));
        OnPropertyChanged(nameof(TimerProgress));
        OnPropertyChanged(nameof(PhaseText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(GroupText));
        OnPropertyChanged(nameof(ActionText));
        OnPropertyChanged(nameof(NextPhaseText));
        OnPropertyChanged(nameof(ClockHintText));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(StartPauseText));
    }

    private static double CalculateTimerProgress(TimerSnapshot current)
    {
        if (current.Phase is not (TimerPhase.Preparation or TimerPhase.Training or TimerPhase.Resting or TimerPhase.Paused) ||
            current.PhaseDurationSeconds <= 0)
        {
            return 0;
        }

        return Math.Clamp(current.RemainingSeconds / (double)current.PhaseDurationSeconds, 0, 1);
    }

    private static string FormatClock(int seconds)
    {
        int safeSeconds = Math.Max(0, seconds);
        return $"{safeSeconds / 60:00}:{safeSeconds % 60:00}";
    }

    private static string GetPhaseText(TimerPhase phase)
    {
        return phase switch
        {
            TimerPhase.Preparation => "准备",
            TimerPhase.Training => "训练",
            TimerPhase.Resting => "休息",
            TimerPhase.Paused => "已暂停",
            TimerPhase.Completed => "已完成",
            TimerPhase.Stopped => "已停止",
            _ => "未开始"
        };
    }

    private static string GetStatusText(TimerSnapshot current)
    {
        return current.Phase switch
        {
            TimerPhase.Preparation => "准备进入训练",
            TimerPhase.Training => "训练进行中",
            TimerPhase.Resting => "休息调整中",
            TimerPhase.Paused => "训练已暂停",
            TimerPhase.Completed => "本次训练完成",
            TimerPhase.Stopped => "训练已停止",
            _ => "设置计划后点击开始"
        };
    }

    private static string GetClockHint(TimerPhase phase)
    {
        return phase switch
        {
            TimerPhase.Preparation => "准备倒计时",
            TimerPhase.Training => "当前动作剩余",
            TimerPhase.Resting => "休息剩余",
            TimerPhase.Paused => "暂停中",
            TimerPhase.Completed => "做得很好",
            TimerPhase.Stopped => "已结束",
            _ => "准备开始"
        };
    }

    private static string GetFooterText(TimerSnapshot current)
    {
        return current.Phase switch
        {
            TimerPhase.Training => "专注动作质量，保持呼吸。",
            TimerPhase.Resting => "放松肩颈，准备下一组。",
            TimerPhase.Preparation => "确认空间安全，马上开始。",
            TimerPhase.Paused => "调整好后继续。",
            TimerPhase.Completed => "记录已保存。",
            TimerPhase.Stopped => "可以调整计划后重新开始。",
            _ => "保持节奏，完成每一组。"
        };
    }

    private void ApplyTheme(bool useDarkMode)
    {
        ResourceDictionary resources = Application.Current.Resources;

        if (useDarkMode)
        {
            SetBrush(resources, "AppBackgroundBrush", 0x11, 0x18, 0x27);
            SetBrush(resources, "PanelBackgroundBrush", 0x18, 0x21, 0x2F);
            SetBrush(resources, "InkBrush", 0xE5, 0xE7, 0xEB);
            SetBrush(resources, "MutedTextBrush", 0x9C, 0xA3, 0xAF);
            SetBrush(resources, "AccentBrush", 0x34, 0xD3, 0x99);
            SetBrush(resources, "AccentDarkBrush", 0x10, 0xB9, 0x81);
            SetBrush(resources, "BorderBrushSoft", 0x33, 0x41, 0x55);
            SetBrush(resources, "PrimaryButtonForegroundBrush", 0x06, 0x18, 0x12);
            SetBrush(resources, "ButtonBackgroundBrush", 0x1F, 0x29, 0x37);
            SetBrush(resources, "ButtonHoverBrush", 0x27, 0x35, 0x48);
            SetBrush(resources, "ButtonPressedBrush", 0x31, 0x41, 0x58);
            SetBrush(resources, "PrimaryPressedBrush", 0x05, 0x96, 0x69);
            SetBrush(resources, "TextInputBackgroundBrush", 0x10, 0x18, 0x26);
            SetBrush(resources, "BadgeBackgroundBrush", 0x16, 0x35, 0x28);
            SetBrush(resources, "BadgeBorderBrush", 0x25, 0x6B, 0x4A);
            SetBrush(resources, "TimerCircleBrush", 0x15, 0x1E, 0x2B);
            SetBrush(resources, "ChartSoftAccentBrush", 0x2E, 0x34, 0xD3, 0x99);
            SetBrush(resources, "SelectionBackgroundBrush", 0x25, 0x6B, 0x4A);
            SetBrush(resources, "SelectionForegroundBrush", 0xEC, 0xFE, 0xF5);
        }
        else
        {
            SetBrush(resources, "AppBackgroundBrush", 0xF7, 0xF2, 0xEA);
            SetBrush(resources, "PanelBackgroundBrush", 0xFF, 0xFF, 0xFF);
            SetBrush(resources, "InkBrush", 0x1F, 0x29, 0x33);
            SetBrush(resources, "MutedTextBrush", 0x6B, 0x72, 0x80);
            SetBrush(resources, "AccentBrush", 0x2F, 0x85, 0x5A);
            SetBrush(resources, "AccentDarkBrush", 0x27, 0x67, 0x49);
            SetBrush(resources, "BorderBrushSoft", 0xE5, 0xDE, 0xD3);
            SetBrush(resources, "PrimaryButtonForegroundBrush", 0xFF, 0xFF, 0xFF);
            SetBrush(resources, "ButtonBackgroundBrush", 0xFD, 0xF9, 0xF2);
            SetBrush(resources, "ButtonHoverBrush", 0xF7, 0xEF, 0xE3);
            SetBrush(resources, "ButtonPressedBrush", 0xEC, 0xE2, 0xD4);
            SetBrush(resources, "PrimaryPressedBrush", 0x22, 0x54, 0x3D);
            SetBrush(resources, "TextInputBackgroundBrush", 0xFF, 0xFF, 0xFF);
            SetBrush(resources, "BadgeBackgroundBrush", 0xEE, 0xF7, 0xF0);
            SetBrush(resources, "BadgeBorderBrush", 0xCD, 0xE7, 0xD4);
            SetBrush(resources, "TimerCircleBrush", 0xFD, 0xF9, 0xF2);
            SetBrush(resources, "ChartSoftAccentBrush", 0x28, 0x2F, 0x85, 0x5A);
            SetBrush(resources, "SelectionBackgroundBrush", 0xDC, 0xEF, 0xE5);
            SetBrush(resources, "SelectionForegroundBrush", 0x1F, 0x29, 0x33);
        }

        Background = resources["AppBackgroundBrush"] as Brush ?? Background;
        ApplySystemTitleBarTheme(useDarkMode);
        InvalidateCharts(this);
    }

    private void ApplySystemTitleBarTheme(bool useDarkMode)
    {
        if (PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        int enabled = useDarkMode ? 1 : 0;
        if (DwmSetWindowAttribute(handle, DwmWindowAttributeUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(handle, DwmWindowAttributeUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private static void SetBrush(ResourceDictionary resources, string key, byte red, byte green, byte blue)
    {
        resources[key] = new SolidColorBrush(Color.FromRgb(red, green, blue));
    }

    private static void SetBrush(ResourceDictionary resources, string key, byte alpha, byte red, byte green, byte blue)
    {
        resources[key] = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
    }

    private static void InvalidateCharts(DependencyObject root)
    {
        if (root is BarChartControl chart)
        {
            chart.InvalidateVisual();
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            InvalidateCharts(VisualTreeHelper.GetChild(root, index));
        }
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FitnessTimer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void RestoreLastPage()
    {
        foreach (object item in MainTabs.Items)
        {
            if (item is TabItem tabItem &&
                string.Equals(tabItem.Tag as string, viewModel.Settings.LastPage, StringComparison.Ordinal))
            {
                MainTabs.SelectedItem = tabItem;
                return;
            }
        }
    }

    private void ShowDataLoadWarning()
    {
        MessageBox.Show(
            viewModel.DataLoadWarning,
            "数据文件读取失败",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
