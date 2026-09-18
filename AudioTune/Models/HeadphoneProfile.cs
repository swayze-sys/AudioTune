namespace AudioTune.Models;

public sealed class HeadphoneProfile
{
    public string Id { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string FrequencyRange { get; init; } = string.Empty;
    public string Impedance { get; init; } = string.Empty;
    public string ImageUri { get; init; } = string.Empty;
    public bool HasVerifiedReferenceCorrection { get; init; }
    public string ReferenceStatus { get; init; } = string.Empty;
}
