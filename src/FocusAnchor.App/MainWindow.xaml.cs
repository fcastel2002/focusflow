using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FocusAnchor.Core;

namespace FocusAnchor.App;

public partial class MainWindow : Window
{
    private readonly FocusSessionController _controller;
    private FloatingTimerWindow? _floatingWindow;
    private int _selectedDurationMinutes = 25;

    public MainWindow()
    {
        InitializeComponent();

        _controller = App.Current.SessionController;
        _controller.StateChanged += Controller_StateChanged;
        UpdateUi();
    }

    private void DurationOption_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string duration }
            && int.TryParse(duration, out var durationMinutes))
        {
            _selectedDurationMinutes = durationMinutes;
        }
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
        var session = _controller.CurrentSession;

        StartPanel.Visibility = session is null ? Visibility.Visible : Visibility.Collapsed;
        SessionPanel.Visibility = session is { Status: FocusSessionStatus.Active or FocusSessionStatus.Paused }
            ? Visibility.Visible
            : Visibility.Collapsed;
        ReviewPanel.Visibility = session is { Status: FocusSessionStatus.Completed }
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
}
