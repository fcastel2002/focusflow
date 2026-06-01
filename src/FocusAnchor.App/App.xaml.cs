using System.IO;
using System.Windows;
using FocusAnchor.Data;

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
        SessionController = new FocusSessionController(SessionRepository);
    }

    public ISessionHistoryRepository SessionRepository { get; }

    public FocusSessionController SessionController { get; }
}
