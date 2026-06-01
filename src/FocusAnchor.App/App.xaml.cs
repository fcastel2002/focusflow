using System.IO;
using System.Net.Http;
using System.Windows;
using FocusAnchor.Data;
using FocusAnchor.Data.Google;

namespace FocusAnchor.App;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;

    public App()
    {
        var applicationDataPath = Environment.GetEnvironmentVariable("FOCUSANCHOR_DATA_PATH");

        if (string.IsNullOrWhiteSpace(applicationDataPath))
        {
            applicationDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FocusAnchor");
        }

        SessionRepository = new SqliteSessionHistoryRepository(Path.Combine(applicationDataPath, "focus-anchor.db"));
        CalendarRepository = (ICalendarRepository)SessionRepository;
        ThemeService = new ThemeService(SessionRepository);
        RainAudioService = new RainAudioService(SessionRepository);
        SessionController = new FocusSessionController(SessionRepository);
        GoogleCalendarConnectionService = new GoogleCalendarConnectionService(
            new HttpClient(),
            new WindowsGoogleCredentialStore(),
            Environment.GetEnvironmentVariable("FOCUSANCHOR_GOOGLE_CLIENT_ID"));
    }

    public ISessionHistoryRepository SessionRepository { get; }

    public ICalendarRepository CalendarRepository { get; }

    public FocusSessionController SessionController { get; }

    public ThemeService ThemeService { get; }

    public RainAudioService RainAudioService { get; }

    public GoogleCalendarConnectionService GoogleCalendarConnectionService { get; }

    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeService.ApplySavedTheme();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RainAudioService.Dispose();
        base.OnExit(e);
    }
}
