using AudioTune.Models;

namespace AudioTune.Services;

public sealed class HeadphoneProfileService
{
    private readonly List<HeadphoneProfile> _profiles =
    [
        new HeadphoneProfile
        {
            Id = "beyerdynamic-amiron-home",
            Manufacturer = "Beyerdynamic",
            Model = "Amiron Home",
            Type = "Open-back",
            FrequencyRange = "5 Hz – 40 kHz",
            Impedance = "250 Ω",
            ImageUri = "pack://application:,,,/Assets/Headphones/AmironHome.png",
            HasVerifiedReferenceCorrection = false,
            ReferenceStatus = "Measurement sources identified; correction dataset pending verification"
        }
    ];

    public IReadOnlyList<HeadphoneProfile> Profiles => _profiles;
    public HeadphoneProfile Selected => _profiles.First();
}
