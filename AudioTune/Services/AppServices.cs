namespace AudioTune.Services;

public static class AppServices
{
    public static SettingsService Settings { get; } = new();
    public static DebugLogService Log { get; } = new();
    public static AudioDeviceService AudioDevices { get; } = new();
    public static HeadphoneProfileService Headphones { get; } = new();
    public static ProfileRepository Profiles { get; } = new();
    public static CorrectionPresetRepository CorrectionPresets { get; } = new();
    public static TonePlaybackService TonePlayer { get; } = new();
    public static MusicPreviewService MusicPreview { get; } = new();
    public static SystemDspService SystemDsp { get; } = new();
    public static EqualizerApoInstallerService EqualizerApoInstaller { get; } = new();
    public static HearingTestEngine HearingTest { get; } = new();
    public static FineTuneEngine FineTune { get; } = new();

    public static void Initialize()
    {
        Settings.Load();
        Profiles.LoadLatest();
        CorrectionPresets.Load();
        Log.Log("AudioTune initialized", Models.LogLevel.Success);
    }

    public static void Dispose()
    {
        try { TonePlayer.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        try { MusicPreview.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        AudioDevices.Dispose();
    }
}
