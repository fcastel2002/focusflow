using System.Windows;
using System.Windows.Input;
using FocusAnchor.Core;

namespace FocusAnchor.App;

public partial class FloatingTimerWindow : Window
{
    private readonly FocusSessionController _controller;
    private readonly MainWindow _hubWindow;

    public FloatingTimerWindow(FocusSessionController controller, MainWindow hubWindow)
    {
        InitializeComponent();

        _controller = controller;
        _hubWindow = hubWindow;
        _controller.StateChanged += Controller_StateChanged;
        UpdateUi();
    }

    public void ShowNearDesktopCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;

        Show();
        Activate();
    }

    private void Controller_StateChanged(object? sender, EventArgs e)
    {
        UpdateUi();
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        _controller.TogglePause();
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        _controller.EndSession();
    }

    private void OpenHub_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        _hubWindow.Show();
        _hubWindow.Activate();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState is MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _controller.StateChanged -= Controller_StateChanged;
    }

    private void UpdateUi()
    {
        var session = _controller.CurrentSession;

        if (session is null)
        {
            Hide();
            return;
        }

        FloatingIntentText.Text = session.Intent.Description;
        FloatingTimeText.Text = FocusSessionController.FormatRemainingTime(_controller.RemainingTime);
        FloatingProgressBar.Value = _controller.ProgressPercentage;

        switch (session.Status)
        {
            case FocusSessionStatus.Active:
                FloatingStatusText.Text = "SESIÓN ACTIVA";
                FloatingPauseResumeButton.Content = "Pausar";
                FloatingPauseResumeButton.IsEnabled = true;
                FloatingEndButton.IsEnabled = true;
                break;
            case FocusSessionStatus.Paused:
                FloatingStatusText.Text = "SESIÓN EN PAUSA";
                FloatingPauseResumeButton.Content = "Reanudar";
                FloatingPauseResumeButton.IsEnabled = true;
                FloatingEndButton.IsEnabled = true;
                break;
            case FocusSessionStatus.Completed:
                FloatingStatusText.Text = "BLOQUE FINALIZADO";
                FloatingPauseResumeButton.IsEnabled = false;
                FloatingEndButton.IsEnabled = false;
                break;
        }
    }
}
