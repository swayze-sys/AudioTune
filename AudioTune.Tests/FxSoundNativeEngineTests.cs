using AudioTune.Services;

namespace AudioTune.Tests;

public sealed class FxSoundNativeEngineTests
{
    [Fact]
    public void NeutralSettingsAreValid()
    {
        FxSoundEffectSettings.Neutral.Validate();
    }

    [Theory]
    [InlineData(-0.01f)]
    [InlineData(10.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void EffectSettingsRejectValuesOutsideFxSoundRange(float invalidValue)
    {
        var settings = FxSoundEffectSettings.Neutral with { Clarity = invalidValue };

        Assert.Throws<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void ApoHostConfigurationUsesNamedNormalizedParameters()
    {
        var settings = new FxSoundEffectSettings(6, 4, 5, 7, 6);

        string line = FxSoundEnhancementService.BuildApoConfigLine(
            settings,
            @"C:\Program Files\AudioTune\AudioTune.FxSound.Apo.dll");

        Assert.Equal(
            "VSTPlugin: Library \"C:\\Program Files\\AudioTune\\AudioTune.FxSound.Apo.dll\" Power 1 Clarity 0.600 Ambience 0.400 Surround 0.500 Dynamic 0.700 Bass 0.600",
            line);
    }

    [Fact]
    public void DisabledApoHostSignatureIgnoresStoredInactiveValues()
    {
        string neutral = FxSoundEnhancementService.CreateSignature(false, FxSoundEffectSettings.Neutral);
        string stored = FxSoundEnhancementService.CreateSignature(false, new FxSoundEffectSettings(10, 9, 8, 7, 6));

        Assert.Equal(neutral, stored);
    }
}
