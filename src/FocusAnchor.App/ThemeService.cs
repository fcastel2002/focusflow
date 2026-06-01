using System.Windows;
using FocusAnchor.Data;

namespace FocusAnchor.App;

public sealed class ThemeService
{
    private const string ThemePreferenceKey = "theme";
    private readonly ISessionHistoryRepository _repository;

    public ThemeService(ISessionHistoryRepository repository)
    {
        _repository = repository;
    }

    public bool IsDarkTheme { get; private set; }

    public event EventHandler? ThemeChanged;

    public void ApplySavedTheme()
    {
        ApplyTheme(_repository.GetPreference(ThemePreferenceKey) == "dark", persist: false);
    }

    public void ToggleTheme()
    {
        ApplyTheme(!IsDarkTheme, persist: true);
    }

    private void ApplyTheme(bool useDarkTheme, bool persist)
    {
        var resources = Application.Current.Resources.MergedDictionaries;
        var currentTheme = resources.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Theme.xaml", StringComparison.Ordinal) == true);

        if (currentTheme is not null)
        {
            resources.Remove(currentTheme);
        }

        resources.Insert(0, new ResourceDictionary
        {
            Source = new Uri(useDarkTheme ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative)
        });

        IsDarkTheme = useDarkTheme;

        if (persist)
        {
            _repository.SetPreference(ThemePreferenceKey, useDarkTheme ? "dark" : "light");
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
