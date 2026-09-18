namespace AudioTune.Models;

public enum EarChannel { Left, Right }

public enum HearingMeasurementStatus
{
    Detected = 0,
    NotDetectedAtCeiling = 1,
    Skipped = 2
}

public enum MeasurementConfidence
{
    Unknown = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public sealed class HearingTrial
{
    public double LevelDbFs { get; set; }
    public bool Heard { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public sealed class HearingMeasurement
{
    public double FrequencyHz { get; set; }
    public EarChannel Ear { get; set; }
    public double ThresholdDbFs { get; set; }
    public double? InitialThresholdDbFs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int Presentations { get; set; }
    public HearingMeasurementStatus Status { get; set; } = HearingMeasurementStatus.Detected;
    public List<HearingTrial> Trials { get; set; } = new();
    public List<double> VerificationThresholdsDbFs { get; set; } = new();
    public List<HearingMeasurementStatus> VerificationStatuses { get; set; } = new();
    public bool WasAutomaticallyFlagged { get; set; }
    public bool VerificationCompleted { get; set; }
    public MeasurementConfidence Confidence { get; set; } = MeasurementConfidence.Unknown;
}

public sealed class HearingSession
{
    public int SchemaVersion { get; set; } = 3;
    public string AppVersion { get; set; } = "0.4.16-alpha";
    public int TestProtocolVersion { get; set; } = 3;
    public int CorrectionAlgorithmVersionAtMeasurement { get; set; } = 4;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public string HeadphoneId { get; set; } = "beyerdynamic-amiron-home";
    public string? OutputDeviceId { get; set; }
    public string? OutputDeviceName { get; set; }
    public int ApplicationSessionVolumePercent { get; set; } = 50;
    public double? EndpointMasterVolumePercentAtStart { get; set; }
    public int SampleRateHz { get; set; } = 48000;
    public string AudioApi { get; set; } = "WASAPI shared";
    public string? MeasurementNotes { get; set; }
    public bool IsArchived { get; set; }

    // Legacy field kept for loading alpha profiles. New correction intensity lives in CorrectionPreset.
    public double CorrectionStrengthPercent { get; set; } = 100.0;
    public List<HearingMeasurement> Measurements { get; set; } = new();
}

public sealed class FineTuneAdjustment
{
    public double FrequencyHz { get; set; }
    public EarChannel Ear { get; set; }
    public double AdjustmentDb { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public sealed class CorrectionPreset
{
    public int SchemaVersion { get; set; } = 2;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HearingProfileId { get; set; }
    public string Name { get; set; } = "Personal correction";
    public double StrengthPercent { get; set; } = 100.0;
    // Fine-tune data is retained even when disabled so it can be toggled on/off instantly.
    public bool FineTuneEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<FineTuneAdjustment> FineTuneAdjustments { get; set; } = new();

    // Final post-EQ stereo-centering trim. Positive moves the perceived image to the right
    // by attenuating the left channel; negative moves it left by attenuating the right.
    // No channel is boosted by this control.
    public double StereoCenterBalanceDb { get; set; } = 0.0;
    public DateTime? StereoCenteringUpdatedAt { get; set; }

    public override string ToString() => Name;
}
