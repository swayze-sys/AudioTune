using System.IO;
using AudioTune.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioTune.Services;

public sealed class MusicPreviewService : IAsyncDisposable
{
    private WasapiPlayer? _player;
    private AudioFileReader? _reader;
    private CalibrationAbSampleProvider? _abProvider;
    private MMDevice? _device;
    private readonly object _sync = new();

    public string? FilePath { get; private set; }
    public string? FileName => string.IsNullOrWhiteSpace(FilePath) ? null : Path.GetFileName(FilePath);
    public bool IsLoaded => _reader is not null && _player is not null && _abProvider is not null;
    public bool IsPlaying => _player?.PlaybackState == PlaybackState.Playing;
    public bool IsCalibrated => _abProvider?.CalibratedSelected == true;
    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;
    public double HeadroomDb => _abProvider?.HeadroomDb ?? 0.0;
    public int SampleRate => _reader?.WaveFormat.SampleRate ?? 0;
    public int Channels => _reader?.WaveFormat.Channels ?? 0;

    public event Action? StateChanged;

    public async Task LoadAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("No music file selected.", nameof(filePath));
        if (!File.Exists(filePath)) throw new FileNotFoundException("Music file not found.", filePath);

        await DisposePlaybackAsync();

        AudioFileReader? reader = null;
        MMDevice? device = null;
        WasapiPlayer? player = null;
        try
        {
            reader = new AudioFileReader(filePath);
            if (reader.WaveFormat.Channels != 2)
                throw new NotSupportedException($"The A/B listening test currently requires stereo audio. This file has {reader.WaveFormat.Channels} channel(s).");

            var session = AppServices.Profiles.LastSession;
            if (session is null || session.CompletedAt is null || session.Measurements.Count == 0)
                throw new InvalidOperationException("Complete a hearing calibration before using the calibrated A/B listening test.");

            var filterSet = DspFilterService.BuildFilterSet(session);
            if (filterSet.Left.Count == 0 || filterSet.Right.Count == 0)
                throw new InvalidOperationException("The latest calibration does not contain measurements for both ears.");

            var provider = new CalibrationAbSampleProvider(
                reader,
                filterSet.Left,
                filterSet.Right,
                Math.Abs(Math.Min(0.0, filterSet.AppliedPreampDb)),
                filterSet.LeftTrimDb,
                filterSet.RightTrimDb);

            device = AppServices.AudioDevices.GetDevice(AppServices.Settings.Current.SelectedOutputDeviceId);
            var builder = new WasapiPlayerBuilder()
                .WithSharedMode()
                .WithEventSync()
                .WithLatency(Math.Clamp(AppServices.Settings.Current.OutputLatencyMs, 20, 500));

            if (device is not null) builder = builder.WithDevice(device);
            if (AppServices.Settings.Current.RawWasapiMode) builder = builder.WithRawMode();

            player = builder.Build();
            player.Init(provider);
            player.Volume = TonePlaybackService.ReferenceSessionVolumeScalar;
            player.IsMuted = false;
            player.PlaybackStopped += Player_PlaybackStopped;

            lock (_sync)
            {
                _reader = reader;
                _abProvider = provider;
                _device = device;
                _player = player;
                FilePath = filePath;
            }

            AppServices.Log.Log($"A/B music loaded: {Path.GetFileName(filePath)}", LogLevel.Success);
            AppServices.Log.Log($"A/B common headroom: {provider.HeadroomDb:0.0} dB", LogLevel.Info);
            StateChanged?.Invoke();
        }
        catch
        {
            if (player is not null)
            {
                try { player.PlaybackStopped -= Player_PlaybackStopped; } catch { }
                try { await player.DisposeAsync(); } catch { }
            }
            reader?.Dispose();
            device?.Dispose();
            throw;
        }
    }

    public async Task ReloadCalibrationAsync()
    {
        string? path;
        TimeSpan position;
        bool wasPlaying;
        bool wasCalibrated;

        lock (_sync)
        {
            path = FilePath;
            position = _reader?.CurrentTime ?? TimeSpan.Zero;
            wasPlaying = _player?.PlaybackState == PlaybackState.Playing;
            wasCalibrated = _abProvider?.CalibratedSelected == true;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        await LoadAsync(path);
        lock (_sync)
        {
            if (_reader is not null)
            {
                var max = _reader.TotalTime > TimeSpan.FromMilliseconds(100)
                    ? _reader.TotalTime - TimeSpan.FromMilliseconds(100)
                    : TimeSpan.Zero;
                _reader.CurrentTime = position > max ? max : position;
            }
            _abProvider?.SetCalibrated(wasCalibrated);
        }

        if (wasPlaying) _player?.Play();
        StateChanged?.Invoke();
    }

    public void Play()
    {
        if (_player is null) return;
        _player.Play();
        AppServices.Log.Log("A/B music playback started", LogLevel.Info);
        StateChanged?.Invoke();
    }

    public void Pause()
    {
        if (_player is null) return;
        _player.Pause();
        AppServices.Log.Log("A/B music playback paused", LogLevel.Info);
        StateChanged?.Invoke();
    }

    public void Restart()
    {
        if (_reader is null || _abProvider is null) return;
        _reader.CurrentTime = TimeSpan.Zero;
        _abProvider.ResetFilterState();
        AppServices.Log.Log("A/B music restarted", LogLevel.Trace);
        StateChanged?.Invoke();
    }

    public void SetCalibrated(bool enabled)
    {
        if (_abProvider is null) return;
        _abProvider.SetCalibrated(enabled);
        AppServices.Log.Log(enabled ? "A/B switched to CALIBRATED" : "A/B switched to ORIGINAL", LogLevel.Info);
        StateChanged?.Invoke();
    }

    public async Task StopAsync()
    {
        if (_player is null || _reader is null || _abProvider is null) return;
        try { _player.Stop(); } catch { }
        _reader.CurrentTime = TimeSpan.Zero;
        _abProvider.ResetFilterState();
        StateChanged?.Invoke();
        await Task.CompletedTask;
    }

    private void Player_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            AppServices.Log.Log($"A/B playback error: {e.Exception.Message}", LogLevel.Error);
        StateChanged?.Invoke();
    }

    private async Task DisposePlaybackAsync()
    {
        WasapiPlayer? player;
        AudioFileReader? reader;
        MMDevice? device;

        lock (_sync)
        {
            player = _player;
            reader = _reader;
            device = _device;
            _player = null;
            _reader = null;
            _abProvider = null;
            _device = null;
            FilePath = null;
        }

        if (player is not null)
        {
            try { player.PlaybackStopped -= Player_PlaybackStopped; } catch { }
            try { player.Stop(); } catch { }
            try { await player.DisposeAsync(); } catch { }
        }
        reader?.Dispose();
        device?.Dispose();
    }

    public async ValueTask DisposeAsync() => await DisposePlaybackAsync();
}
