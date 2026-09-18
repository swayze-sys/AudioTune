using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioTune.Services;

public sealed class TonePlaybackService : IAsyncDisposable
{
    // Fixed per-application Windows mixer reference. This does NOT touch the endpoint/master volume.
    public const float ReferenceSessionVolumeScalar = 0.50f;
    public const int ReferenceSessionVolumePercent = 50;

    private WasapiPlayer? _player;
    private MMDevice? _device;
    private StereoCenteringSampleProvider? _centeringProvider;
    private readonly object _sync = new();

    public bool IsPlaying => _player?.PlaybackState == PlaybackState.Playing;

    public async Task StartRepeatingAsync(
        double frequencyHz,
        double levelDbFs,
        Models.EarChannel ear,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopAsync();

        var device = AppServices.AudioDevices.GetDevice(AppServices.Settings.Current.SelectedOutputDeviceId);
        var builder = new WasapiPlayerBuilder()
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(Math.Clamp(AppServices.Settings.Current.OutputLatencyMs, 20, 500));

        if (device is not null) builder = builder.WithDevice(device);
        if (AppServices.Settings.Current.RawWasapiMode) builder = builder.WithRawMode();

        WasapiPlayer? player = null;
        try
        {
            player = builder.Build();
            var provider = new PulseToneSampleProvider(
                frequencyHz,
                levelDbFs,
                ear == Models.EarChannel.Left ? 0 : 1,
                repeatForever: true);

            player.Init(provider);

            // NAudio 3 exposes the Windows per-session mixer volume here. Setting this value
            // changes AudioTune only; the device master volume and other applications are untouched.
            player.Volume = ReferenceSessionVolumeScalar;
            player.IsMuted = false;

            lock (_sync)
            {
                _player = player;
                _device = device;
            }

            player.Play();

            AppServices.Log.Log(
                $"Repeating tone: {frequencyHz:0.##} Hz, {levelDbFs:0.0} dBFS, {ear}; AudioTune session volume {ReferenceSessionVolumePercent}%",
                Models.LogLevel.Trace);
        }
        catch
        {
            if (player is not null)
            {
                try { await player.DisposeAsync(); } catch { }
            }
            device?.Dispose();
            throw;
        }
    }


    public async Task StartFineTuneAlternatingAsync(
        double referenceFrequencyHz,
        double referenceLevelDbFs,
        double testFrequencyHz,
        double testLevelDbFs,
        Models.EarChannel ear,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopAsync();

        var device = AppServices.AudioDevices.GetDevice(AppServices.Settings.Current.SelectedOutputDeviceId);
        var builder = new WasapiPlayerBuilder()
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(Math.Clamp(AppServices.Settings.Current.OutputLatencyMs, 20, 500));
        if (device is not null) builder = builder.WithDevice(device);
        if (AppServices.Settings.Current.RawWasapiMode) builder = builder.WithRawMode();

        WasapiPlayer? player = null;
        try
        {
            player = builder.Build();
            var provider = new AlternatingFineTuneSampleProvider(
                referenceFrequencyHz, referenceLevelDbFs, testFrequencyHz, testLevelDbFs,
                ear == Models.EarChannel.Left ? 0 : 1);
            player.Init(provider);
            player.Volume = ReferenceSessionVolumeScalar;
            player.IsMuted = false;
            lock (_sync) { _player = player; _device = device; }
            player.Play();
        }
        catch
        {
            if (player is not null) { try { await player.DisposeAsync(); } catch { } }
            device?.Dispose();
            throw;
        }
    }
    public async Task StartStereoCenteringAsync(
        Models.HearingSession session,
        double initialBalanceDb,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopAsync();

        var device = AppServices.AudioDevices.GetDevice(AppServices.Settings.Current.SelectedOutputDeviceId);
        var builder = new WasapiPlayerBuilder()
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(Math.Clamp(AppServices.Settings.Current.OutputLatencyMs, 20, 500));
        if (device is not null) builder = builder.WithDevice(device);
        if (AppServices.Settings.Current.RawWasapiMode) builder = builder.WithRawMode();

        WasapiPlayer? player = null;
        try
        {
            player = builder.Build();
            var filterSet = DspFilterService.BuildFilterSet(session);
            var provider = new StereoCenteringSampleProvider(48000, filterSet, initialBalanceDb);
            player.Init(provider);
            player.Volume = ReferenceSessionVolumeScalar;
            player.IsMuted = false;
            lock (_sync)
            {
                _player = player;
                _device = device;
                _centeringProvider = provider;
            }
            player.Play();
            AppServices.Log.Log($"Stereo centering test started at {initialBalanceDb:+0.00;-0.00;0.00} dB balance", Models.LogLevel.Info);
        }
        catch
        {
            if (player is not null) { try { await player.DisposeAsync(); } catch { } }
            device?.Dispose();
            throw;
        }
    }

    public void SetStereoCenterBalance(double balanceDb)
    {
        lock (_sync)
        {
            if (_centeringProvider is not null) _centeringProvider.BalanceDb = balanceDb;
        }
    }

    public async Task StopAsync()
    {
        WasapiPlayer? player;
        MMDevice? device;
        lock (_sync)
        {
            player = _player;
            device = _device;
            _player = null;
            _device = null;
            _centeringProvider = null;
        }

        if (player is not null)
        {
            try { player.Stop(); } catch { }
            try { await player.DisposeAsync(); } catch { }
        }
        device?.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
