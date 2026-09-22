using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NAudio.Wave;

namespace AudioTune.Services;

public sealed class FxSoundEnhancementService
{
    public const string ApoHostFileName = "AudioTune.FxSound.Apo.dll";
    public const string InstalledApoHostFileName = "AudioTune.FxSound.Apo.0.4.18.dll";

    public event Action? StateChanged;

    public bool Enabled => AppServices.Settings.Current.FxSoundEnhancementsEnabled;

    public FxSoundEffectSettings CurrentEffects => new(
        (float)AppServices.Settings.Current.FxSoundClarity,
        (float)AppServices.Settings.Current.FxSoundAmbience,
        (float)AppServices.Settings.Current.FxSoundSurround,
        (float)AppServices.Settings.Current.FxSoundDynamicBoost,
        (float)AppServices.Settings.Current.FxSoundBass);

    public SystemDspService.AutoApplyResult Update(bool enabled, FxSoundEffectSettings effects)
    {
        effects.Validate();
        var settings = AppServices.Settings.Current;
        settings.FxSoundEnhancementsEnabled = enabled;
        settings.FxSoundClarity = effects.Clarity;
        settings.FxSoundAmbience = effects.Ambience;
        settings.FxSoundSurround = effects.Surround;
        settings.FxSoundDynamicBoost = effects.DynamicBoost;
        settings.FxSoundBass = effects.Bass;
        AppServices.Settings.Save();
        StateChanged?.Invoke();
        return AppServices.SystemDsp.TryAutoApplyCurrentPreset();
    }

    public string CreateSignature() => CreateSignature(Enabled, CurrentEffects);

    internal static string CreateSignature(bool enabled, FxSoundEffectSettings effects)
    {
        string value = enabled
            ? string.Join("|", "on", effects.Enumerate().Select(x => x.Value.ToString("0.000", CultureInfo.InvariantCulture)))
            : "off";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
    }

    public string GetApoHostPath()
    {
        string installedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AudioTune",
            "Native",
            InstalledApoHostFileName);
        return File.Exists(installedPath)
            ? installedPath
            : Path.Combine(AppContext.BaseDirectory, ApoHostFileName);
    }

    public string BuildApoConfigLine() => BuildApoConfigLine(CurrentEffects, GetApoHostPath());

    internal static string BuildApoConfigLine(FxSoundEffectSettings effects, string hostPath)
    {
        static string Normalize(float value) => (value / 10.0f).ToString("0.000", CultureInfo.InvariantCulture);
        return $"VSTPlugin: Library \"{hostPath}\" Power 1 Clarity {Normalize(effects.Clarity)} Ambience {Normalize(effects.Ambience)} Surround {Normalize(effects.Surround)} Dynamic {Normalize(effects.DynamicBoost)} Bass {Normalize(effects.Bass)}";
    }

    public bool TryProbe(out string details)
    {
        try
        {
            using var engine = new FxSoundNativeEngine();
            engine.Configure(48000, 2);
            engine.SetEqualizerEnabled(false);
            engine.SetMasterGain(0);
            engine.SetVolumeLeveling(0);
            engine.SetEffects(FxSoundEffectSettings.Neutral);
            engine.SetPower(true);
            Span<float> output = stackalloc float[32];
            engine.Process(new float[32], output);
            bool hostPresent = File.Exists(GetApoHostPath());
            details = hostPresent
                ? $"Native engine + APO host ready · upstream {FxSoundNativeEngine.UpstreamCommit[..8]}"
                : $"Native engine ready, APO host missing · upstream {FxSoundNativeEngine.UpstreamCommit[..8]}";
            return hostPresent;
        }
        catch (Exception ex)
        {
            details = $"Native engine unavailable: {ex.Message}";
            return false;
        }
    }

    public ISampleProvider CreateListeningTestProvider(ISampleProvider source)
        => new FxSoundSampleProvider(source, this);
}
