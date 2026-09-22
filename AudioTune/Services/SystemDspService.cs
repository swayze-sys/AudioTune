using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AudioTune.Models;
using Microsoft.Win32;

namespace AudioTune.Services;

public sealed class SystemDspService
{
    public sealed record AutoApplyResult(bool Attempted, bool Applied, string Message);
    public event Action? DspStateChanged;

    public sealed record Status(
        bool EqualizerApoDetected,
        bool AudioTuneApplied,
        string? ConfigDirectory,
        string Message,
        string? SelectedDeviceId,
        string? SelectedDeviceName,
        string? EndpointGuid,
        bool? ApoInstalledOnSelectedDevice,
        bool? EnhancementsEnabled,
        bool AudioTuneTargetsSelectedDevice,
        bool AudioTuneProfileMatchesActiveProfile,
        Guid? AppliedProfileId,
        string DeviceMessage,
        bool PersistentConfigured,
        bool LevelMatchedBypass,
        string PersistenceMessage);

    private sealed record DeviceApoRegistration(bool Installed, bool EnhancementsEnabled, string Message);

    private const string AudioTuneFileName = "AudioTune.txt";
    private const string AudioTuneManagedFolderName = "AudioTune";
    private const string IncludeLine = "Include: AudioTune.txt";
    private const string EqualizerApoRegistryPath = @"SOFTWARE\EqualizerAPO";
    private const string ChildAposRegistryPath = @"SOFTWARE\EqualizerAPO\Child APOs";
    private const string RenderEndpointsRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
    private const string AudioProcessingObjectsRegistryPath = @"SOFTWARE\Classes\AudioEngine\AudioProcessingObjects";
    private const string ClsidRegistryPath = @"SOFTWARE\Classes\CLSID";
    private const string DisableEnhancementsValueName = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5";

    private static readonly string[] ApoGuidValueNames =
    [
        "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},1",
        "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},2",
        "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5",
        "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6",
        "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},7"
    ];

    public AutoApplyResult TryAutoApplyCurrentPreset()
    {
        var session = AppServices.Profiles.LastSession;
        if (session?.CompletedAt is null)
            return new AutoApplyResult(false, false, "No completed active hearing profile is available.");

        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return TryAutoApplyPreset(session, preset);
    }

    /// <summary>
    /// Immediately writes the current preset to the selected persistent DSP target by using
    /// the same Apply() path as the manual Apply / Update DSP action. A stale correction
    /// signature is expected after a preset change and must never block auto-apply.
    /// </summary>
    public AutoApplyResult TryAutoApplyPreset(HearingSession session, CorrectionPreset preset)
    {
        if (!AppServices.Settings.Current.AutoApplyDspChanges)
            return new AutoApplyResult(false, false, "Auto-apply DSP changes is disabled.");
        if (session.CompletedAt is null)
            return new AutoApplyResult(false, false, "The active hearing profile is not complete.");

        string? selectedDeviceId = AppServices.Settings.Current.SelectedDspDeviceId;
        if (string.IsNullOrWhiteSpace(selectedDeviceId))
            return new AutoApplyResult(false, false, "No DSP target device is selected.");

        var device = AppServices.AudioDevices.EnumerateOutputs()
            .FirstOrDefault(d => string.Equals(d.Id, selectedDeviceId, StringComparison.OrdinalIgnoreCase));
        if (device is null)
            return new AutoApplyResult(false, false, "The selected DSP target device is not currently available.");

        // Auto-apply intentionally uses the exact same write path as the manual
        // Apply / Update DSP button.  Do not reject the update merely because the
        // correction signature is stale: a stale signature is exactly what a Fine
        // Tune/intensity/centering change is supposed to replace.
        //
        // Preserve an explicitly selected level-matched bypass, however.  Changing
        // a preset while bypassed must not silently turn processing back on.
        try
        {
            var configDir = FindEqualizerApoConfigDirectory();
            string? endpointGuid = ExtractEndpointGuid(device.Id);
            if (configDir is not null && !string.IsNullOrWhiteSpace(endpointGuid))
            {
                string deviceFile = GetManagedDeviceFilePath(configDir, endpointGuid);
                if (File.Exists(deviceFile))
                {
                    string existing = File.ReadAllText(deviceFile);
                    if (existing.Contains("# AudioTune level-matched bypass", StringComparison.Ordinal))
                        return new AutoApplyResult(false, false, "Persistent DSP is currently bypassed; auto-apply will not re-enable it.");
                }
            }

            Apply(session, preset, device.Id, device.Name);
            return new AutoApplyResult(true, true, $"DSP updated automatically on '{device.Name}'.");
        }
        catch (Exception ex)
        {
            AppServices.Log.Log($"Auto-apply DSP failed: {ex.Message}", LogLevel.Warning);
            return new AutoApplyResult(true, false, ex.Message);
        }
    }

    public Status GetStatus(
        string? selectedDeviceId = null,
        string? selectedDeviceName = null,
        Guid? activeProfileId = null,
        double? activeStrengthPercent = null,
        string? activeSignature = null)
    {
        var configDir = FindEqualizerApoConfigDirectory();
        var endpointGuid = ExtractEndpointGuid(selectedDeviceId);

        if (configDir is null)
        {
            return new Status(
                false, false, null, "Equalizer APO not detected",
                selectedDeviceId, selectedDeviceName, endpointGuid,
                null, null, false, false, null,
                string.IsNullOrWhiteSpace(selectedDeviceName) ? "No output device selected" : $"Current device: {selectedDeviceName}",
                false, false, "Persistent DSP is unavailable until Equalizer APO is installed.");
        }

        var mainConfig = Path.Combine(configDir, "config.txt");
        bool included = IsManagedIncludePresent(mainConfig);

        string managedText = string.Empty;
        bool hasManagedDeviceFile = false;
        if (!string.IsNullOrWhiteSpace(endpointGuid))
        {
            var deviceFile = GetManagedDeviceFilePath(configDir, endpointGuid);
            if (File.Exists(deviceFile))
            {
                hasManagedDeviceFile = true;
                try { managedText = File.ReadAllText(deviceFile); } catch { }
            }
        }

        // Compatibility with v0.3.6 and earlier: if no per-device file exists yet,
        // inspect the old single AudioTune.txt configuration.
        if (string.IsNullOrWhiteSpace(managedText))
        {
            try
            {
                var legacyMaster = Path.Combine(configDir, AudioTuneFileName);
                if (File.Exists(legacyMaster))
                {
                    var legacyText = File.ReadAllText(legacyMaster);
                    var legacyTarget = ParseTargetEndpointGuid(legacyText);
                    if (!string.IsNullOrWhiteSpace(endpointGuid) &&
                        endpointGuid.Equals(legacyTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        managedText = legacyText;
                    }
                }
            }
            catch { }
        }

        bool enabled = managedText.Contains("# AudioTune enabled", StringComparison.Ordinal);
        bool levelMatchedBypass = managedText.Contains("# AudioTune level-matched bypass", StringComparison.Ordinal);
        bool persistentConfigured = included && !string.IsNullOrWhiteSpace(managedText) && (enabled || levelMatchedBypass);

        Guid? appliedProfileId = ParseAppliedProfileId(managedText);
        string? targetedEndpointGuid = ParseTargetEndpointGuid(managedText);
        bool targetsSelectedDevice = endpointGuid is not null && targetedEndpointGuid is not null &&
                                     endpointGuid.Equals(targetedEndpointGuid, StringComparison.OrdinalIgnoreCase);

        double appliedStrength = ParseAppliedStrengthPercent(managedText) ?? 100.0;
        int appliedAlgorithmVersion = ParseAppliedAlgorithmVersion(managedText) ?? 1;
        string? appliedSignature = ParseAppliedSignature(managedText);
        string? appliedEnhancementSignature = ParseAppliedEnhancementSignature(managedText);
        bool appliedFxSoundHostEnabled = managedText.Contains("# FxSound native host: ON", StringComparison.Ordinal);
        string currentFxSoundHostPath = AppServices.FxSoundEnhancements.GetApoHostPath();
        bool enhancementHostPathMatches = !appliedFxSoundHostEnabled ||
                                          (File.Exists(currentFxSoundHostPath) &&
                                           managedText.Contains($"Library \"{currentFxSoundHostPath}\"", StringComparison.OrdinalIgnoreCase));
        bool enhancementMatches = (appliedEnhancementSignature is null
            ? !AppServices.FxSoundEnhancements.Enabled
            : string.Equals(appliedEnhancementSignature, AppServices.FxSoundEnhancements.CreateSignature(), StringComparison.OrdinalIgnoreCase)) &&
                                  enhancementHostPathMatches;
        bool strengthMatches = activeStrengthPercent is null || Math.Abs(appliedStrength - activeStrengthPercent.Value) < 0.1;
        bool algorithmMatches = appliedAlgorithmVersion == CorrectionPreviewService.AlgorithmVersion;
        bool signatureMatches = string.IsNullOrWhiteSpace(activeSignature) || string.Equals(appliedSignature, activeSignature, StringComparison.OrdinalIgnoreCase);
        bool profileMatches = activeProfileId is null
            ? appliedProfileId is not null && algorithmMatches && signatureMatches && enhancementMatches
            : appliedProfileId == activeProfileId && strengthMatches && algorithmMatches && signatureMatches && enhancementMatches;

        bool? apoOnDevice = null;
        bool? enhancementsEnabled = null;
        string deviceMessage;

        if (endpointGuid is null)
        {
            deviceMessage = string.IsNullOrWhiteSpace(selectedDeviceName)
                ? "No output device selected"
                : "The selected Windows endpoint GUID could not be resolved.";
        }
        else
        {
            var registration = GetDeviceApoRegistration(endpointGuid);
            apoOnDevice = registration.Installed;
            enhancementsEnabled = registration.EnhancementsEnabled;
            deviceMessage = registration.Message;
        }

        bool applied = persistentConfigured && enabled && targetsSelectedDevice && profileMatches &&
                       apoOnDevice == true && enhancementsEnabled != false;

        string message;
        if (apoOnDevice == false)
            message = "Equalizer APO installed · current DSP target is not configured in the Device Selector";
        else if (enhancementsEnabled == false)
            message = "Equalizer APO is registered, but Windows audio enhancements are disabled for this device";
        else if (!persistentConfigured)
            message = "No persistent AudioTune DSP is configured for this target";
        else if (!targetsSelectedDevice)
            message = "AudioTune DSP data exists, but it targets a different output device";
        else if (!profileMatches)
            message = appliedProfileId == activeProfileId
                ? "AudioTune profile settings changed · apply changes to update persistent DSP"
                : "Persistent DSP contains a different hearing profile";
        else if (levelMatchedBypass)
            message = "Persistent DSP is in level-matched bypass · clipping headroom remains active";
        else if (applied)
            message = "Persistent AudioTune DSP is active for the selected output device";
        else
            message = "Equalizer APO detected · persistent DSP status could not be fully verified";

        string persistenceMessage;
        if (!persistentConfigured)
            persistenceMessage = "Disabled for this device";
        else if (levelMatchedBypass)
            persistenceMessage = "Enabled · level-matched bypass persists after reboot";
        else if (hasManagedDeviceFile)
            persistenceMessage = "Enabled · correction is loaded by Equalizer APO after Windows starts";
        else
            persistenceMessage = "Enabled using legacy AudioTune configuration · apply once to migrate to per-device storage";

        return new Status(
            true, applied, configDir, message,
            selectedDeviceId, selectedDeviceName, endpointGuid,
            apoOnDevice, enhancementsEnabled, targetsSelectedDevice, profileMatches,
            appliedProfileId, deviceMessage,
            persistentConfigured, levelMatchedBypass, persistenceMessage);
    }

    public string BuildConfiguration(
        HearingSession session,
        string? deviceId = null,
        string? deviceName = null,
        bool enabled = true)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return BuildConfiguration(session, preset, deviceId, deviceName, enabled);
    }

    /// <summary>
    /// Builds the persistent APO configuration from the exact preset supplied by the caller.
    /// This avoids a second repository lookup between UI confirmation and file generation.
    /// </summary>
    public string BuildConfiguration(
        HearingSession session,
        CorrectionPreset preset,
        string? deviceId = null,
        string? deviceName = null,
        bool enabled = true,
        bool? fxSoundEnabled = null,
        FxSoundEffectSettings? fxSoundEffects = null,
        string? fxSoundHostPath = null)
    {
        bool includeFxSound = fxSoundEnabled ?? AppServices.FxSoundEnhancements.Enabled;
        FxSoundEffectSettings selectedFxSoundEffects = fxSoundEffects ?? AppServices.FxSoundEnhancements.CurrentEffects;
        string selectedFxSoundHostPath = fxSoundHostPath ?? AppServices.FxSoundEnhancements.GetApoHostPath();
        selectedFxSoundEffects.Validate();
        var filterSet = DspFilterService.BuildFilterSet(session, preset);
        var left = filterSet.Left;
        var right = filterSet.Right;
        if (left.Count == 0 || right.Count == 0)
            throw new InvalidOperationException("The profile does not contain usable thresholds for both ears.");
        var signature = DspFilterService.CreateSignature(session, preset);

        string? endpointGuid = ExtractEndpointGuid(deviceId);
        if (string.IsNullOrWhiteSpace(endpointGuid))
            throw new InvalidOperationException("The selected Windows output device could not be resolved to an endpoint GUID.");

        var sb = new StringBuilder();
        sb.AppendLine("# AudioTune persistent system-wide DSP");
        sb.AppendLine($"# Profile: {session.Name}");
        sb.AppendLine($"# Profile ID: {session.Id:D}");
        sb.AppendLine($"# Correction strength: {Math.Clamp(preset.StrengthPercent, 0.0, 200.0).ToString("0.0", CultureInfo.InvariantCulture)}%");
        sb.AppendLine($"# Correction preset ID: {preset.Id:D}");
        sb.AppendLine($"# Hearing Profile stage: {(preset.HearingProfileEnabled ? "ON" : "OFF")}");
        sb.AppendLine($"# Fine Tune state: {(preset.FineTuneEnabled ? "ON" : "OFF")}");
        sb.AppendLine($"# Fine Tune points: {preset.FineTuneAdjustments.Count}");
        sb.AppendLine($"# Stereo Centering state: {(preset.StereoCenteringEnabled ? "ON" : "OFF")}");
        sb.AppendLine($"# Saved Stereo Centering balance: {preset.StereoCenterBalanceDb.ToString("0.00", CultureInfo.InvariantCulture)} dB");
        sb.AppendLine($"# Correction signature: {signature}");
        sb.AppendLine($"# Correction algorithm: {CorrectionPreviewService.AlgorithmVersion}");
        sb.AppendLine($"# FxSound native host: {(includeFxSound ? "ON" : "OFF")}");
        sb.AppendLine($"# FxSound signature: {FxSoundEnhancementService.CreateSignature(includeFxSound, selectedFxSoundEffects)}");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("# Experimental personal hearing correction. Headphone compensation is disabled.");

        if (!string.IsNullOrWhiteSpace(deviceName))
            sb.AppendLine($"# Target device: {deviceName}");
        sb.AppendLine($"# Target endpoint GUID: {endpointGuid}");

        double preampDb = filterSet.AppliedPreampDb;
        double targetCurvePeak = DspFilterService.CalculateTargetCurvePeakDb(session, preset);
        sb.AppendLine($"# Target curve maximum boost: {targetCurvePeak.ToString("0.00", CultureInfo.InvariantCulture)} dB");
        sb.AppendLine($"# Actual fitted DSP peak / required headroom: {filterSet.RequiredHeadroomDb.ToString("0.00", CultureInfo.InvariantCulture)} dB");
        sb.AppendLine($"# Applied preamp: {preampDb.ToString("0.00", CultureInfo.InvariantCulture)} dB");
        sb.AppendLine($"# Positive boost scale: {(filterSet.PositiveGainScale * 100.0).ToString("0.0", CultureInfo.InvariantCulture)}%");
        if (filterSet.PotentialClippingDb > 0.01)
            sb.AppendLine($"# WARNING: potential digital overshoot: +{filterSet.PotentialClippingDb.ToString("0.00", CultureInfo.InvariantCulture)} dB");

        if (!enabled)
        {
            sb.AppendLine("# AudioTune level-matched bypass");
            sb.AppendLine($"Device: {endpointGuid}");
            sb.AppendLine($"Preamp: {preampDb:0.0} dB".Replace(',', '.'));
            sb.AppendLine("Channel: ALL");
            return sb.ToString();
        }

        AppendResponseExplanation(sb, session, preset, filterSet);
        sb.AppendLine("# AudioTune enabled");
        sb.AppendLine($"Device: {endpointGuid}");
        sb.AppendLine($"Preamp: {preampDb:0.00} dB".Replace(',', '.'));
        sb.AppendLine("Channel: L");
        if (filterSet.LeftTrimDb < -0.001) sb.AppendLine($"Preamp: {filterSet.LeftTrimDb:0.00} dB".Replace(',', '.'));
        foreach (var line in DspFilterService.ToEqualizerApoLines(left)) sb.AppendLine(line);
        sb.AppendLine("Channel: R");
        if (filterSet.RightTrimDb < -0.001) sb.AppendLine($"Preamp: {filterSet.RightTrimDb:0.00} dB".Replace(',', '.'));
        foreach (var line in DspFilterService.ToEqualizerApoLines(right)) sb.AppendLine(line);
        sb.AppendLine("Channel: ALL");
        if (includeFxSound)
        {
            sb.AppendLine("# FxSound parameters use normalized VST values; 1.000 equals 10.0 on the AudioTune scale.");
            sb.AppendLine(FxSoundEnhancementService.BuildApoConfigLine(selectedFxSoundEffects, selectedFxSoundHostPath));
        }
        return sb.ToString();
    }

    public bool IsFxSoundHostActiveForDevice(string? deviceId)
    {
        try
        {
            string? configDir = FindEqualizerApoConfigDirectory();
            string? endpointGuid = ExtractEndpointGuid(deviceId);
            if (configDir is null || endpointGuid is null || !IsManagedIncludePresent(Path.Combine(configDir, "config.txt")))
                return false;
            string path = GetManagedDeviceFilePath(configDir, endpointGuid);
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path);
            string hostPath = AppServices.FxSoundEnhancements.GetApoHostPath();
            return File.Exists(hostPath) &&
                   text.Contains("# AudioTune enabled", StringComparison.Ordinal) &&
                   text.Contains("# FxSound native host: ON", StringComparison.Ordinal) &&
                   text.Contains($"Library \"{hostPath}\"", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static void AppendResponseExplanation(
        StringBuilder sb,
        HearingSession session,
        CorrectionPreset preset,
        DspFilterSet filterSet)
    {
        bool includeFineTune = preset.FineTuneEnabled;
        var leftComponents = CorrectionPreviewService.CreateComponentsBeforeStereoPreservation(
            session, preset, EarChannel.Left, includeFineTune);
        var rightComponents = CorrectionPreviewService.CreateComponentsBeforeStereoPreservation(
            session, preset, EarChannel.Right, includeFineTune);
        var finalTargets = CorrectionPreviewService.CreateStereoPair(session, preset, includeFineTune);

        sb.AppendLine("#");
        sb.AppendLine("# DSP response explanation (all values are dB):");
        sb.AppendLine("# Hearing  = hearing-test/model contribution before correction strength.");
        sb.AppendLine("# FineTune = saved/interpolated Fine Tune contribution used by this preset (0.00 when OFF).");
        sb.AppendLine($"# Target   = clamp((Hearing + FineTune) * {Math.Clamp(preset.StrengthPercent, 0.0, 200.0).ToString("0.0", CultureInfo.InvariantCulture)}%, -12.00, +12.00), then stereo preservation.");
        sb.AppendLine("# RealDSP  = summed response of every PK filter at this frequency, before preamp/centering.");
        sb.AppendLine("# Output   = RealDSP + global preamp + per-channel Stereo Centering trim.");
        sb.AppendLine("# A Filter line's Gain is only that filter's own center gain, not the final response.");
        AppendChannelResponseExplanation(
            sb, "L", leftComponents, finalTargets.Left, filterSet.Left,
            filterSet.AppliedPreampDb, filterSet.LeftTrimDb);
        AppendChannelResponseExplanation(
            sb, "R", rightComponents, finalTargets.Right, filterSet.Right,
            filterSet.AppliedPreampDb, filterSet.RightTrimDb);
        sb.AppendLine("#");
    }

    private static void AppendChannelResponseExplanation(
        StringBuilder sb,
        string channel,
        IReadOnlyList<CorrectionPreviewService.CorrectionPointComponents> components,
        IReadOnlyList<(double Frequency, double GainDb)> finalTarget,
        IReadOnlyList<ParametricEqFilter> filters,
        double preampDb,
        double channelTrimDb)
    {
        var hearing = components.Select(x => (x.Frequency, Value: x.HearingModelDb)).ToList();
        var fineTune = components.Select(x => (x.Frequency, Value: x.FineTuneDb)).ToList();
        var target = finalTarget.Select(x => (x.Frequency, Value: x.GainDb)).ToList();

        sb.AppendLine($"# Channel {channel} band response:");
        sb.AppendLine("#       Hz | Hearing | FineTune |   Target |  RealDSP |   Output");
        foreach (double frequency in DspFilterService.Bands)
        {
            double hearingDb = InterpolateLog(hearing, frequency);
            double fineTuneDb = InterpolateLog(fineTune, frequency);
            double targetDb = InterpolateLog(target, frequency);
            double realDspDb = filters.Sum(filter => DspFilterService.PeakingMagnitudeDb(filter, frequency, 48000));
            double outputDb = realDspDb + preampDb + channelTrimDb;
            sb.AppendLine(
                $"# {frequency.ToString("0.##", CultureInfo.InvariantCulture).PadLeft(8)} |" +
                $" {FormatSignedDb(hearingDb).PadLeft(7)} |" +
                $" {FormatSignedDb(fineTuneDb).PadLeft(8)} |" +
                $" {FormatSignedDb(targetDb).PadLeft(8)} |" +
                $" {FormatSignedDb(realDspDb).PadLeft(8)} |" +
                $" {FormatSignedDb(outputDb).PadLeft(8)}");
        }
    }

    private static string FormatSignedDb(double value)
        => value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

    private static double InterpolateLog(
        IReadOnlyList<(double Frequency, double Value)> points,
        double frequency)
    {
        if (points.Count == 0) return 0.0;
        if (frequency <= points[0].Frequency) return points[0].Value;
        if (frequency >= points[^1].Frequency) return points[^1].Value;
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            if (frequency < a.Frequency || frequency > b.Frequency) continue;
            double t = (Math.Log(frequency) - Math.Log(a.Frequency)) /
                       (Math.Log(b.Frequency) - Math.Log(a.Frequency));
            return a.Value + ((b.Value - a.Value) * t);
        }
        return 0.0;
    }

    public double CalculateHeadroomDb(HearingSession session) => DspFilterService.CalculateHeadroomDb(session);
    public double CalculateRequiredHeadroomDb(HearingSession session) => DspFilterService.CalculateRequiredHeadroomDb(session);

    public void Export(HearingSession session, string path, string? deviceId = null, string? deviceName = null) =>
        File.WriteAllText(path, BuildConfiguration(session, deviceId, deviceName), Encoding.UTF8);

    public void Export(HearingSession session, CorrectionPreset preset, string path, string? deviceId = null, string? deviceName = null) =>
        File.WriteAllText(path, BuildConfiguration(session, preset, deviceId, deviceName), Encoding.UTF8);

    public Status Apply(HearingSession session, string deviceId, string deviceName)
    {
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return Apply(session, preset, deviceId, deviceName);
    }

    public Status Apply(HearingSession session, CorrectionPreset preset, string deviceId, string deviceName)
    {
        var configDir = FindEqualizerApoConfigDirectory()
            ?? throw new InvalidOperationException("Equalizer APO was not found. Install it and configure the Windows output device first.");

        string endpointGuid = ValidateTargetDevice(deviceId, deviceName);
        PreparePersistentConfig(configDir);
        if (AppServices.FxSoundEnhancements.Enabled && !File.Exists(AppServices.FxSoundEnhancements.GetApoHostPath()))
            throw new FileNotFoundException("The native AudioTune FxSound APO host is missing from the application folder.", AppServices.FxSoundEnhancements.GetApoHostPath());

        string deviceFile = GetManagedDeviceFilePath(configDir, endpointGuid);
        Directory.CreateDirectory(Path.GetDirectoryName(deviceFile)!);
        File.WriteAllText(deviceFile, BuildConfiguration(session, preset, deviceId, deviceName, enabled: true), Encoding.UTF8);
        RebuildMasterConfiguration(configDir);
        EnsureManagedInclude(configDir);

        AppServices.Log.Log($"Persistent system DSP applied to '{deviceName}' for profile '{session.Name}' / preset '{preset.Name}' / Fine Tune {(preset.FineTuneEnabled ? "ON" : "OFF") }.", LogLevel.Success);
        DspStateChanged?.Invoke();
        return GetStatus(deviceId, deviceName, session.Id, preset.StrengthPercent, DspFilterService.CreateSignature(session, preset));
    }

    public Status Disable(HearingSession session, string deviceId, string deviceName)
    {
        var configDir = FindEqualizerApoConfigDirectory()
            ?? throw new InvalidOperationException("Equalizer APO was not found.");

        string endpointGuid = ValidateTargetDevice(deviceId, deviceName);
        PreparePersistentConfig(configDir);

        string deviceFile = GetManagedDeviceFilePath(configDir, endpointGuid);
        Directory.CreateDirectory(Path.GetDirectoryName(deviceFile)!);
        File.WriteAllText(deviceFile, BuildConfiguration(session, deviceId, deviceName, enabled: false), Encoding.UTF8);
        RebuildMasterConfiguration(configDir);
        EnsureManagedInclude(configDir);

        AppServices.Log.Log($"Persistent DSP switched to level-matched bypass on '{deviceName}'.", LogLevel.Info);
        DspStateChanged?.Invoke();
        var preset = AppServices.CorrectionPresets.GetActiveForProfile(session);
        return GetStatus(deviceId, deviceName, session.Id, preset.StrengthPercent, DspFilterService.CreateSignature(session));
    }

    public Status DisablePersistent(string deviceId, string deviceName, Guid? activeProfileId = null, double? activeStrengthPercent = null)
    {
        var configDir = FindEqualizerApoConfigDirectory()
            ?? throw new InvalidOperationException("Equalizer APO was not found.");

        string endpointGuid = ExtractEndpointGuid(deviceId)
            ?? throw new InvalidOperationException("The selected Windows output device could not be resolved to an endpoint GUID.");

        string deviceFile = GetManagedDeviceFilePath(configDir, endpointGuid);
        if (File.Exists(deviceFile)) File.Delete(deviceFile);

        RebuildMasterConfiguration(configDir);
        if (!EnumerateManagedDeviceFiles(configDir).Any())
            RemoveManagedInclude(configDir);

        AppServices.Log.Log($"Persistent AudioTune DSP disabled for '{deviceName}'.", LogLevel.Info);
        DspStateChanged?.Invoke();
        return GetStatus(deviceId, deviceName, activeProfileId, activeStrengthPercent);
    }

    public static string? ExtractEndpointGuid(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return null;

        var matches = Regex.Matches(deviceId, @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}");
        if (matches.Count == 0) return null;

        var candidate = matches[matches.Count - 1].Value;
        return Guid.TryParse(candidate, out var guid) ? guid.ToString("B").ToUpperInvariant() : null;
    }

    private static string ValidateTargetDevice(string deviceId, string deviceName)
    {
        string endpointGuid = ExtractEndpointGuid(deviceId)
            ?? throw new InvalidOperationException("The selected Windows output device could not be resolved to an endpoint GUID.");

        var registration = GetDeviceApoRegistration(endpointGuid);
        if (!registration.Installed)
            throw new InvalidOperationException($"Equalizer APO is not configured for '{deviceName}'. Open the Equalizer APO Device Selector and enable this playback device first.");
        if (!registration.EnhancementsEnabled)
            throw new InvalidOperationException($"Windows audio enhancements are disabled for '{deviceName}'. Equalizer APO cannot process this endpoint until enhancements are enabled.");

        return endpointGuid;
    }

    private static void PreparePersistentConfig(string configDir)
    {
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(Path.Combine(configDir, AudioTuneManagedFolderName));

        var mainConfig = Path.Combine(configDir, "config.txt");
        if (!File.Exists(mainConfig))
            File.WriteAllText(mainConfig, string.Empty, Encoding.UTF8);

        var backup = Path.Combine(configDir, "config.audiotune-backup.txt");
        if (!File.Exists(backup))
            File.Copy(mainConfig, backup, false);
    }

    private static string GetManagedDeviceFilePath(string configDir, string endpointGuid)
    {
        var id = endpointGuid.Trim('{', '}').Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return Path.Combine(configDir, AudioTuneManagedFolderName, $"device-{id}.txt");
    }

    private static IEnumerable<string> EnumerateManagedDeviceFiles(string configDir)
    {
        var folder = Path.Combine(configDir, AudioTuneManagedFolderName);
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        return Directory.GetFiles(folder, "device-*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void RebuildMasterConfiguration(string configDir)
    {
        var masterPath = Path.Combine(configDir, AudioTuneFileName);
        var files = EnumerateManagedDeviceFiles(configDir).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("# AudioTune managed persistent DSP master");
        sb.AppendLine("# This file is generated by AudioTune. Per-device files are stored in .\\AudioTune\\");

        if (files.Count == 0)
        {
            sb.AppendLine("# No persistent AudioTune DSP targets are currently enabled.");
        }
        else
        {
            foreach (var file in files)
            {
                sb.AppendLine();
                sb.AppendLine($"# ----- {Path.GetFileName(file)} -----");
                try { sb.AppendLine(File.ReadAllText(file).TrimEnd()); }
                catch { }
            }
        }

        File.WriteAllText(masterPath, sb.ToString(), Encoding.UTF8);
    }

    private static bool IsManagedIncludePresent(string mainConfig)
    {
        try
        {
            return File.Exists(mainConfig) && File.ReadAllLines(mainConfig)
                .Any(line => line.Trim().Equals(IncludeLine, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    private static void EnsureManagedInclude(string configDir)
    {
        var mainConfig = Path.Combine(configDir, "config.txt");
        if (!File.Exists(mainConfig)) File.WriteAllText(mainConfig, string.Empty, Encoding.UTF8);
        var lines = File.ReadAllLines(mainConfig).ToList();
        if (lines.Any(line => line.Trim().Equals(IncludeLine, StringComparison.OrdinalIgnoreCase))) return;

        lines.Add(string.Empty);
        lines.Add("# AudioTune managed persistent DSP include");
        lines.Add(IncludeLine);
        File.WriteAllLines(mainConfig, lines, Encoding.UTF8);
    }

    private static void RemoveManagedInclude(string configDir)
    {
        var mainConfig = Path.Combine(configDir, "config.txt");
        if (!File.Exists(mainConfig)) return;

        var lines = File.ReadAllLines(mainConfig).ToList();
        lines.RemoveAll(line => line.Trim().Equals(IncludeLine, StringComparison.OrdinalIgnoreCase));
        lines.RemoveAll(line => line.Trim().Equals("# AudioTune managed persistent DSP include", StringComparison.OrdinalIgnoreCase));
        lines.RemoveAll(line => line.Trim().Equals("# AudioTune managed include", StringComparison.OrdinalIgnoreCase));
        File.WriteAllLines(mainConfig, lines, Encoding.UTF8);
    }

    private static string FormatGraphicEq(IReadOnlyList<(double Frequency, double GainDb)> points)
    {
        return string.Join("; ", points.Select(p =>
            $"{p.Frequency.ToString("0.##", CultureInfo.InvariantCulture)} {p.GainDb.ToString("0.00", CultureInfo.InvariantCulture)}"));
    }

    private static Guid? ParseAppliedProfileId(string text)
    {
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "# Profile ID:";
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var value = trimmed[prefix.Length..].Trim();
            if (Guid.TryParse(value, out var id)) return id;
        }
        return null;
    }

    private static double? ParseAppliedStrengthPercent(string text)
    {
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "# Correction strength:";
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var value = trimmed[prefix.Length..].Trim().TrimEnd('%');
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var strength))
                return strength;
        }
        return null;
    }

    private static int? ParseAppliedAlgorithmVersion(string text)
    {
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "# Correction algorithm:";
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var value = trimmed[prefix.Length..].Trim();
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
                return version;
        }
        return null;
    }

    private static string? ParseAppliedSignature(string text)
    {
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "# Correction signature:";
            var trimmed = line.Trim();
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            return trimmed[prefix.Length..].Trim();
        }
        return null;
    }

    private static string? ParseAppliedEnhancementSignature(string text)
    {
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            const string prefix = "# FxSound signature:";
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return trimmed[prefix.Length..].Trim();
        }
        return null;
    }

    private static string? ParseTargetEndpointGuid(string text)
    {
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            const string commentPrefix = "# Target endpoint GUID:";
            if (trimmed.StartsWith(commentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[commentPrefix.Length..].Trim();
                if (Guid.TryParse(value, out var id)) return id.ToString("B").ToUpperInvariant();
            }

            const string devicePrefix = "Device:";
            if (trimmed.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[devicePrefix.Length..].Trim();
                var extracted = ExtractEndpointGuid(value);
                if (extracted is not null) return extracted;
            }
        }
        return null;
    }

    private static DeviceApoRegistration GetDeviceApoRegistration(string endpointGuid)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new DeviceApoRegistration(false, true, "Device APO detection is available on Windows only.");

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var endpointKey = hklm.OpenSubKey($@"{RenderEndpointsRegistryPath}\{endpointGuid}");
                if (endpointKey is null) continue;

                using var fxKey = endpointKey.OpenSubKey("FxProperties");
                if (fxKey is null)
                    return new DeviceApoRegistration(false, true, "The selected endpoint has no Windows audio-effects registration.");

                bool enhancementsEnabled = true;
                try
                {
                    var disabled = fxKey.GetValue(DisableEnhancementsValueName);
                    if (disabled is int dword && dword != 0) enhancementsEnabled = false;
                }
                catch { }

                bool registered = false;
                foreach (var valueName in ApoGuidValueNames)
                {
                    var value = fxKey.GetValue(valueName)?.ToString();
                    if (string.IsNullOrWhiteSpace(value) || !Guid.TryParse(value, out var apoGuid)) continue;
                    if (IsEqualizerApoClsid(hklm, apoGuid.ToString("B")))
                    {
                        registered = true;
                        break;
                    }
                }

                bool childKeyExists = false;
                try
                {
                    using var child = hklm.OpenSubKey($@"{ChildAposRegistryPath}\{endpointGuid}");
                    childKeyExists = child is not null;
                }
                catch { }

                if (registered)
                {
                    return new DeviceApoRegistration(
                        true,
                        enhancementsEnabled,
                        enhancementsEnabled
                            ? "Equalizer APO is registered on the selected playback endpoint."
                            : "Equalizer APO is registered, but Windows has disabled audio enhancements for this endpoint.");
                }

                return new DeviceApoRegistration(
                    false,
                    enhancementsEnabled,
                    childKeyExists
                        ? "Equalizer APO has metadata for this endpoint, but its APO is not currently registered in Windows FxProperties. Re-open the Device Selector."
                        : "Equalizer APO is not enabled for the selected playback endpoint.");
            }
            catch
            {
                // Try the other registry view before giving up.
            }
        }

        return new DeviceApoRegistration(false, true, "The selected Windows playback endpoint could not be found in the registry.");
    }

    private static bool IsEqualizerApoClsid(RegistryKey hklm, string clsid)
    {
        try
        {
            using var apoKey = hklm.OpenSubKey($@"{AudioProcessingObjectsRegistryPath}\{clsid}");
            if (apoKey is not null)
            {
                foreach (var valueName in apoKey.GetValueNames())
                {
                    var value = apoKey.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value) &&
                        (value.Contains("EqualizerAPO", StringComparison.OrdinalIgnoreCase) ||
                         value.Contains("Equalizer APO", StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
            }
        }
        catch { }

        try
        {
            using var inprocKey = hklm.OpenSubKey($@"{ClsidRegistryPath}\{clsid}\InprocServer32");
            var path = inprocKey?.GetValue(null)?.ToString();
            if (!string.IsNullOrWhiteSpace(path) &&
                Path.GetFileName(path).Equals("EqualizerAPO.dll", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }

        return false;
    }

    private static string? FindEqualizerApoConfigDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = hklm.OpenSubKey(EqualizerApoRegistryPath);
                    var configuredPath = key?.GetValue("ConfigPath")?.ToString();
                    if (!string.IsNullOrWhiteSpace(configuredPath))
                    {
                        configuredPath = Environment.ExpandEnvironmentVariables(configuredPath);
                        if (Directory.Exists(configuredPath)) return configuredPath;
                    }

                    var installPath = key?.GetValue("InstallPath")?.ToString();
                    if (!string.IsNullOrWhiteSpace(installPath))
                    {
                        installPath = Environment.ExpandEnvironmentVariables(installPath);
                        var configPath = Path.Combine(installPath, "config");
                        if (Directory.Exists(configPath)) return configPath;
                    }
                }
                catch { }
            }
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO", "config"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "EqualizerAPO", "config")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}
