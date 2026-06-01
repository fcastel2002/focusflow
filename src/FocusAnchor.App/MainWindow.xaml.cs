using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusAnchor.Core;
using FocusAnchor.Data;
using FocusAnchor.Data.Google;

namespace FocusAnchor.App;

public partial class MainWindow : Window
{
    private readonly FocusSessionController _controller;
    private FloatingTimerWindow? _floatingWindow;
    private int _selectedDurationMinutes = 25;
    private bool _showingHistory;
    private bool _showingCalendar;
    private long _editingCalendarId;
    private long _editingPlanId;

    public MainWindow()
    {
        InitializeComponent();

        _controller = App.Current.SessionController;
        _controller.StateChanged += Controller_StateChanged;
        App.Current.ThemeService.ThemeChanged += ThemeService_ThemeChanged;
        App.Current.RainAudioService.StateChanged += RainAudioService_StateChanged;
        RainVolumeSlider.Value = App.Current.RainAudioService.Volume;
        PlanningCalendar.SelectedDate = DateTime.Today;
        RefreshCalendars();
        UpdateGoogleConnectionUi();
        UpdateUi();
    }

    private async void ConnectGoogle_Click(object sender, RoutedEventArgs e)
    {
        await TryGoogleActionAsync(async () =>
        {
            await App.Current.GoogleCalendarConnectionService.ConnectAsync();
            UpdateGoogleConnectionUi();
        });
    }

    private void LinkGoogleCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedCalendar(out var calendar)
            || GoogleCalendarCombo.SelectedItem is not GoogleRemoteCalendar remoteCalendar)
        {
            return;
        }

        App.Current.CalendarRepository.SaveGoogleCalendarLink(
            new GoogleCalendarLink(calendar.Id, remoteCalendar.Id, remoteCalendar.Name));
        UpdateGoogleConnectionUi();
    }

    private void UnlinkGoogleCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedCalendar(out var calendar))
        {
            return;
        }

        App.Current.CalendarRepository.DeleteGoogleCalendarLink(calendar.Id);
        UpdateGoogleConnectionUi();
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        App.Current.ThemeService.ToggleTheme();
    }

    private void ThemeService_ThemeChanged(object? sender, EventArgs e)
    {
        ThemeButton.Content = App.Current.ThemeService.IsDarkTheme
            ? "Tema claro"
            : "Tema oscuro";
    }

    private void ToggleRain_Click(object sender, RoutedEventArgs e)
    {
        App.Current.RainAudioService.TogglePlayback();
    }

    private void RainVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsInitialized)
        {
            App.Current.RainAudioService.SetVolume(e.NewValue);
        }
    }

    private void RainAudioService_StateChanged(object? sender, EventArgs e)
    {
        RainButton.Content = App.Current.RainAudioService.IsPlaying
            ? "Lluvia: pausar"
            : "Lluvia: reproducir";
    }

    private void DurationOption_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string duration }
            && int.TryParse(duration, out var durationMinutes))
        {
            _selectedDurationMinutes = durationMinutes;
        }
    }

    private void HubSection_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (sender is not RadioButton { Tag: string section })
        {
            return;
        }

        _showingHistory = section is "History";
        _showingCalendar = section is "Calendar";

        if (_showingHistory)
        {
            UpdateHistory();
        }

        if (_showingCalendar)
        {
            UpdateCalendar();
        }

        UpdateUi();
    }

    private void PlanningCalendar_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateCalendar();
        }
    }

    private void FocusCalendarCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FocusCalendarCombo.SelectedItem is FocusCalendar calendar)
        {
            _editingCalendarId = calendar.Id;
            CalendarNameTextBox.Text = calendar.Name;
            CalendarColorTextBox.Text = calendar.ColorHex;
        }

        if (IsInitialized)
        {
            UpdateGoogleConnectionUi();
            UpdateCalendar();
        }
    }

    private void NewCalendar_Click(object sender, RoutedEventArgs e)
    {
        _editingCalendarId = 0;
        FocusCalendarCombo.SelectedItem = null;
        CalendarNameTextBox.Clear();
        CalendarColorTextBox.Text = "#295C4D";
    }

    private void SaveCalendar_Click(object sender, RoutedEventArgs e)
    {
        TryCalendarAction(() =>
        {
            var saved = App.Current.CalendarRepository.SaveCalendar(
                new FocusCalendar(_editingCalendarId, CalendarNameTextBox.Text, CalendarColorTextBox.Text));
            RefreshCalendars(saved.Id);
        });
    }

    private void DeleteCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (_editingCalendarId == 0)
        {
            return;
        }

        App.Current.CalendarRepository.DeleteCalendar(_editingCalendarId);
        _editingCalendarId = 0;
        RefreshCalendars();
    }

    private void SaveDailyGoal_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetSelectedCalendar(out var calendar))
        {
            App.Current.CalendarRepository.SetDailyGoal(
                new DailyGoal(calendar.Id, GetSelectedDate(), DailyGoalTextBox.Text));
            UpdateCalendar();
        }
    }

    private void PlanList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlanList.SelectedItem is not PlanListItem item)
        {
            return;
        }

        _editingPlanId = item.Plan.Id;
        PlanIntentTextBox.Text = item.Plan.IntentDescription;
        PlanTimeTextBox.Text = item.Plan.StartsAt.ToLocalTime().ToString("HH:mm");
        PlanDurationTextBox.Text = ((int)item.Plan.Duration.TotalMinutes).ToString();
    }

    private void NewPlan_Click(object sender, RoutedEventArgs e)
    {
        _editingPlanId = 0;
        PlanList.SelectedItem = null;
        PlanIntentTextBox.Clear();
        PlanTimeTextBox.Text = "09:00";
        PlanDurationTextBox.Text = "25";
    }

    private void SavePlan_Click(object sender, RoutedEventArgs e)
    {
        TryCalendarAction(() =>
        {
            if (!TryGetSelectedCalendar(out var calendar))
            {
                return;
            }

            var startsAt = ParsePlanStartsAt();
            var duration = ParsePlanDuration();
            App.Current.CalendarRepository.SavePlan(
                new FocusPlan(_editingPlanId, calendar.Id, PlanIntentTextBox.Text, startsAt, duration));
            NewPlan_Click(this, new RoutedEventArgs());
            UpdateCalendar();
        });
    }

    private void DeletePlan_Click(object sender, RoutedEventArgs e)
    {
        if (_editingPlanId == 0)
        {
            return;
        }

        App.Current.CalendarRepository.DeletePlan(_editingPlanId);
        NewPlan_Click(this, new RoutedEventArgs());
        UpdateCalendar();
    }

    private void StartPlan_Click(object sender, RoutedEventArgs e)
    {
        if (PlanList.SelectedItem is not PlanListItem item)
        {
            return;
        }

        TryCalendarAction(() =>
        {
            _controller.StartSession(item.Plan.IntentDescription, item.Plan.Duration, item.Plan.Id);
            FocusSectionButton.IsChecked = true;
            _showingCalendar = false;
            _showingHistory = false;
            UpdateUi();
        });
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _controller.StartSession(IntentTextBox.Text, TimeSpan.FromMinutes(_selectedDurationMinutes));
            StartErrorText.Text = string.Empty;
        }
        catch (ArgumentException exception)
        {
            StartErrorText.Text = exception.Message;
        }
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        _controller.TogglePause();
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        _controller.EndSession();
    }

    private void CaptureDistraction_Click(object sender, RoutedEventArgs e)
    {
        CaptureDistraction();
    }

    private void DistractionTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            CaptureDistraction();
            e.Handled = true;
        }
    }

    private void SaveReview_Click(object sender, RoutedEventArgs e)
    {
        _controller.CreateReview(ReviewTextBox.Text);
    }

    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        _controller.ResetSession();
        IntentTextBox.Clear();
        IntentTextBox.Focus();
    }

    private void ShowFloating_Click(object sender, RoutedEventArgs e)
    {
        _floatingWindow ??= new FloatingTimerWindow(_controller, this);
        _floatingWindow.ShowNearDesktopCorner();
        Hide();
    }

    private void Controller_StateChanged(object? sender, EventArgs e)
    {
        UpdateUi();
    }

    private void CaptureDistraction()
    {
        if (string.IsNullOrWhiteSpace(DistractionTextBox.Text))
        {
            return;
        }

        _controller.AddDistraction(DistractionTextBox.Text);
        DistractionTextBox.Clear();
        DistractionTextBox.Focus();
    }

    private void UpdateUi()
    {
        ThemeService_ThemeChanged(this, EventArgs.Empty);
        RainAudioService_StateChanged(this, EventArgs.Empty);
        var session = _controller.CurrentSession;

        HistoryPanel.Visibility = _showingHistory ? Visibility.Visible : Visibility.Collapsed;
        CalendarPanel.Visibility = _showingCalendar ? Visibility.Visible : Visibility.Collapsed;
        StartPanel.Visibility = !_showingHistory && !_showingCalendar && session is null ? Visibility.Visible : Visibility.Collapsed;
        SessionPanel.Visibility = !_showingHistory && !_showingCalendar && session is { Status: FocusSessionStatus.Active or FocusSessionStatus.Paused }
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReviewPanel.Visibility = !_showingHistory && !_showingCalendar && session is { Status: FocusSessionStatus.Completed }
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (session is null)
        {
            HeaderStatusText.Text = "Listo para enfocar";
            return;
        }

        ActiveIntentText.Text = session.Intent.Description;
        RemainingTimeText.Text = FocusSessionController.FormatRemainingTime(_controller.RemainingTime);
        SessionProgressBar.Value = _controller.ProgressPercentage;
        DistractionList.ItemsSource = session.Distractions
            .Select(entry => $"• {entry.Description}")
            .ToArray();

        switch (session.Status)
        {
            case FocusSessionStatus.Active:
                HeaderStatusText.Text = "Sesión activa";
                SessionStatusText.Text = "Tu atención está protegida";
                PauseResumeButton.Content = "Pausar";
                DistractionTextBox.IsEnabled = true;
                CaptureDistractionButton.IsEnabled = true;
                break;
            case FocusSessionStatus.Paused:
                HeaderStatusText.Text = "Sesión en pausa";
                SessionStatusText.Text = "Pausa activa. Retomá cuando estés listo.";
                PauseResumeButton.Content = "Reanudar";
                DistractionTextBox.IsEnabled = false;
                CaptureDistractionButton.IsEnabled = false;
                break;
            case FocusSessionStatus.Completed:
                HeaderStatusText.Text = "Bloque finalizado";
                ReviewSummaryText.Text =
                    $"Intención: {session.Intent.Description}\n" +
                    $"Tiempo enfocado: {FocusSessionController.FormatRemainingTime(session.Duration - _controller.RemainingTime)}\n" +
                    $"Distracciones anotadas: {session.Distractions.Count}";
                ReviewTextBox.IsEnabled = session.Review is null;
                SaveReviewButton.Visibility = session.Review is null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                ReviewSavedText.Text = session.Review is null
                    ? string.Empty
                    : "Revisión guardada.";
                break;
        }
    }

    private void RefreshCalendars(long? selectedCalendarId = null)
    {
        var calendars = App.Current.CalendarRepository.GetCalendars();
        FocusCalendarCombo.ItemsSource = calendars;
        FocusCalendarCombo.SelectedItem = calendars.FirstOrDefault(calendar => calendar.Id == selectedCalendarId)
            ?? calendars.FirstOrDefault();
    }

    private void UpdateCalendar()
    {
        var date = GetSelectedDate();
        SelectedDateText.Text = date.ToString("dddd d 'de' MMMM");
        var summary = App.Current.CalendarRepository.GetDailySummary(date);
        DailySummaryText.Text =
            $"{summary.PlannedBlockCount} bloques planeados · {summary.ReviewedSessionCount} revisados · " +
            $"{FormatFocusedDuration(summary.FocusedDuration)} enfocados · {summary.DistractionCount} distracciones";

        if (!TryGetSelectedCalendar(out var calendar))
        {
            DailyGoalTextBox.Clear();
            PlanList.ItemsSource = Array.Empty<PlanListItem>();
            return;
        }

        DailyGoalTextBox.Text = App.Current.CalendarRepository.GetDailyGoal(calendar.Id, date)?.Description ?? string.Empty;
        PlanList.ItemsSource = App.Current.CalendarRepository.GetPlans(date)
            .Where(plan => plan.CalendarId == calendar.Id)
            .Select(plan => new PlanListItem(plan, $"{plan.StartsAt.ToLocalTime():HH:mm} · {plan.IntentDescription} · {(int)plan.Duration.TotalMinutes} min"))
            .ToArray();
    }

    private bool TryGetSelectedCalendar(out FocusCalendar calendar)
    {
        calendar = FocusCalendarCombo.SelectedItem as FocusCalendar ?? null!;

        if (calendar is not null)
        {
            return true;
        }

        CalendarErrorText.Text = "Creá o elegí un calendario local.";
        return false;
    }

    private DateOnly GetSelectedDate()
    {
        return DateOnly.FromDateTime(PlanningCalendar.SelectedDate ?? DateTime.Today);
    }

    private DateTimeOffset ParsePlanStartsAt()
    {
        if (!TimeOnly.TryParse(PlanTimeTextBox.Text, out var time))
        {
            throw new ArgumentException("La hora debe usar formato HH:mm.");
        }

        var localStart = GetSelectedDate().ToDateTime(time);
        return new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart));
    }

    private TimeSpan ParsePlanDuration()
    {
        if (!int.TryParse(PlanDurationTextBox.Text, out var minutes) || minutes <= 0)
        {
            throw new ArgumentException("La duración debe expresarse en minutos positivos.");
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private void TryCalendarAction(Action action)
    {
        try
        {
            CalendarErrorText.Text = string.Empty;
            action();
        }
        catch (ArgumentException exception)
        {
            CalendarErrorText.Text = exception.Message;
        }
    }

    private void UpdateGoogleConnectionUi()
    {
        var service = App.Current.GoogleCalendarConnectionService;
        GoogleCalendarCombo.ItemsSource = service.RemoteCalendars;

        if (!TryGetSelectedCalendarSilently(out var calendar))
        {
            GoogleStatusText.Text = "Elegí un calendario local para configurar el vínculo.";
            return;
        }

        var link = App.Current.CalendarRepository.GetGoogleCalendarLink(calendar.Id);
        GoogleStatusText.Text = link is not null
            ? $"Vinculado con Google: {link.GoogleCalendarName}"
            : service.IsConfigured
                ? "Sin vínculo remoto. Tu calendario local sigue disponible."
                : "Definí FOCUSANCHOR_GOOGLE_CLIENT_ID para habilitar el vínculo.";
    }

    private async Task TryGoogleActionAsync(Func<Task> action)
    {
        try
        {
            CalendarErrorText.Text = string.Empty;
            await action();
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            CalendarErrorText.Text = exception.Message;
        }
    }

    private bool TryGetSelectedCalendarSilently(out FocusCalendar calendar)
    {
        calendar = FocusCalendarCombo.SelectedItem as FocusCalendar ?? null!;
        return calendar is not null;
    }

    private void UpdateHistory()
    {
        var summary = _controller.GetAttentionSummary();
        HistorySessionCountText.Text = summary.SessionCount.ToString();
        HistoryFocusedTimeText.Text = FormatFocusedDuration(summary.TotalFocusedDuration);
        HistoryDistractionCountText.Text = summary.TotalDistractionCount.ToString();

        HistoryList.ItemsSource = _controller.GetRecentHistory()
            .Select(entry => new HistoryListItem(
                entry.IntentDescription,
                $"{FormatFocusedDuration(entry.FocusedDuration)} de foco · {entry.DistractionCount} distracciones",
                entry.Reflection ?? string.Empty,
                entry.EndedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")))
            .ToArray();
    }

    private static string FormatFocusedDuration(TimeSpan duration)
    {
        var totalMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return $"{totalMinutes} min";
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_controller.HasOpenSession)
        {
            e.Cancel = true;
            ShowFloating_Click(this, new RoutedEventArgs());
            return;
        }

        _floatingWindow?.Close();
    }

    private sealed record HistoryListItem(
        string IntentDescription,
        string Details,
        string Reflection,
        string EndedAt);

    private sealed record PlanListItem(FocusPlan Plan, string DisplayText);
}
