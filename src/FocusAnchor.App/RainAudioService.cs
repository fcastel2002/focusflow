using System.IO;
using System.Windows.Media;
using FocusAnchor.Data;

namespace FocusAnchor.App;

public sealed class RainAudioService : IDisposable
{
    private const string VolumePreferenceKey = "rain-volume";
    private readonly MediaPlayer _player = new();
    private readonly ISessionHistoryRepository _repository;
    private bool _disposed;

    public RainAudioService(ISessionHistoryRepository repository)
    {
        _repository = repository;
        Volume = ParseVolume(repository.GetPreference(VolumePreferenceKey));
        _player.Volume = Volume;
        _player.MediaEnded += Player_MediaEnded;
    }

    public event EventHandler? StateChanged;

    public bool IsPlaying { get; private set; }

    public double Volume { get; private set; }

    public void TogglePlayback()
    {
        ThrowIfDisposed();

        if (IsPlaying)
        {
            _player.Pause();
            IsPlaying = false;
        }
        else
        {
            if (_player.Source is null)
            {
                var audioPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "rain-base.mp3");
                _player.Open(new Uri(audioPath, UriKind.Absolute));
            }

            _player.Play();
            IsPlaying = true;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetVolume(double volume)
    {
        ThrowIfDisposed();

        Volume = Math.Clamp(volume, 0, 1);
        _player.Volume = Volume;
        _repository.SetPreference(VolumePreferenceKey, Volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _player.MediaEnded -= Player_MediaEnded;
        _player.Close();
    }

    private void Player_MediaEnded(object? sender, EventArgs e)
    {
        _player.Position = TimeSpan.Zero;
        _player.Play();
    }

    private static double ParseVolume(string? storedVolume)
    {
        return double.TryParse(
            storedVolume,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var volume)
            ? Math.Clamp(volume, 0, 1)
            : 0.45;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
