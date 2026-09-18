namespace AudioTune.Models;

public sealed class AppSettings
{
    public bool DebugEnabled { get; set; } = true;
    public bool DebugExpanded { get; set; } = true;
    public bool RawWasapiMode { get; set; } = false;
    public int OutputLatencyMs { get; set; } = 50;
    public string? SelectedOutputDeviceId { get; set; }
    public string? SelectedDspDeviceId { get; set; }
    public string SelectedHeadphoneId { get; set; } = "beyerdynamic-amiron-home";
    public string? ActiveHearingProfileId { get; set; }
    public string? ActiveCorrectionPresetId { get; set; }
    public bool AutoApplyDspChanges { get; set; } = true;

    // DSP headroom / preamp control. Automatic mode preserves the previous safe behavior.
    public bool UseAutomaticPreamp { get; set; } = true;
    public double ManualPreampDb { get; set; } = -6.0;
    public bool LimitPositiveBoostsToPreamp { get; set; } = false;

    // Stereo-image protection. The value is the maximum total L/R correction difference
    // in the localization-critical midrange. Low bass and upper treble are allowed more.
    public bool StereoPreservationEnabled { get; set; } = true;
    public double MaxInterauralCorrectionDifferenceDb { get; set; } = 2.0;
}
