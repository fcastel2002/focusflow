using System.Windows;

namespace FocusAnchor.App;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;

    public FocusSessionController SessionController { get; } = new();
}
