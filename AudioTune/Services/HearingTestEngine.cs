using AudioTune.Models;

namespace AudioTune.Services;

public sealed class HearingTestEngine
{
    public static readonly double[] Frequencies =
    [30, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500, 14000, 16000, 18000];

    private const double MinimumLevelDbFs = -90.0;
    private const double MaximumLevelDbFs = -3.0;
    private const double CoarseStepDb = 8.0;
    private const double HighLevelStepDb = 4.0;
    private const double HighLevelStepStartDbFs = -18.0;
    private const int CeilingConfirmationsRequired = 2;
    private const double FinalBracketWidthDb = 2.0;
    private const int MaximumPresentationsPerFrequency = 16;
    private const double OutlierThresholdDb = 8.0;
    private const int MaximumAutomaticVerificationsPerEar = 3;

    private readonly Dictionary<double, ThresholdState> _states = new();
    private readonly Queue<(EarChannel Ear, double Frequency)> _verificationQueue = new();
    private bool _singleFrequencyRetest;
    private HearingMeasurement? _retestOriginalMeasurement;
    private bool _automaticVerification;
    private HearingMeasurement? _verificationTarget;

    public HearingSession Session { get; private set; } = new();
    public EarChannel Ear { get; private set; } = EarChannel.Left;
    public int FrequencyIndex { get; private set; }
    public double CurrentFrequency => Frequencies[FrequencyIndex];
    public double CurrentLevelDbFs => State.LevelDbFs;
    public int CurrentPresentationCount => State.Presentations;
    public bool IsSingleFrequencyRetest => _singleFrequencyRetest;
    public bool IsAutomaticVerification => _automaticVerification;
    public int VerificationRemaining => _verificationQueue.Count + (_automaticVerification ? 1 : 0);
    public bool CanEditMeasurements => Session.CompletedAt is not null && !_singleFrequencyRetest && !_automaticVerification;

    public string CurrentSearchPhase => State.Phase switch
    {
        ThresholdPhase.Coarse => _automaticVerification ? "Verification · coarse" : _singleFrequencyRetest ? "Point retest · coarse" : "Coarse search",
        ThresholdPhase.Refine => _automaticVerification ? "Verification · refine" : _singleFrequencyRetest ? "Point retest · refine" : "Bracket refinement",
        ThresholdPhase.Confirm => _automaticVerification ? "Verification · confirm" : "Final confirmation",
        _ => "Complete"
    };

    public string CurrentBracketText => State.BracketText;
    public bool HasStarted { get; private set; }
    public bool IsComplete => HasStarted && Session.CompletedAt is not null && !_singleFrequencyRetest && !_automaticVerification;

    public event Action? StateChanged;
    public event Action<HearingMeasurement>? MeasurementCompleted;

    private ThresholdState State => _states[StateKey(CurrentFrequency, Ear)];

    public void Start()
    {
        HasStarted = true;
        _singleFrequencyRetest = false;
        _automaticVerification = false;
        _verificationTarget = null;
        _verificationQueue.Clear();
        _retestOriginalMeasurement = null;

        var selectedDevice = AppServices.AudioDevices.EnumerateOutputs()
            .FirstOrDefault(d => d.Id == AppServices.Settings.Current.SelectedOutputDeviceId);

        Session = new HearingSession
        {
            Name = $"Hearing Profile {DateTime.Now:dd.MM.yyyy HH:mm}",
            StartedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            HeadphoneId = AppServices.Settings.Current.SelectedHeadphoneId,
            OutputDeviceId = AppServices.Settings.Current.SelectedOutputDeviceId,
            OutputDeviceName = selectedDevice?.Name,
            ApplicationSessionVolumePercent = TonePlaybackService.ReferenceSessionVolumePercent,
            EndpointMasterVolumePercentAtStart = AppServices.AudioDevices.GetMasterVolumePercent(AppServices.Settings.Current.SelectedOutputDeviceId),
            SampleRateHz = 48000,
            AudioApi = AppServices.Settings.Current.RawWasapiMode ? "WASAPI shared / raw" : "WASAPI shared"
        };
        Ear = EarChannel.Left;
        FrequencyIndex = 0;
        _states.Clear();
        InitializeStates();
        AppServices.Profiles.Autosave(Session);

        AppServices.Log.Log(
            $"Hearing test started (adaptive bracket search); AudioTune session volume locked to {TonePlaybackService.ReferenceSessionVolumePercent}%" +
            (Session.EndpointMasterVolumePercentAtStart.HasValue
                ? $"; Windows master observed at {Session.EndpointMasterVolumePercentAtStart.Value:0}% (not changed)"
                : string.Empty),
            LogLevel.Success);
        StateChanged?.Invoke();
    }

    public void LoadSession(HearingSession session)
    {
        Session = session;
        HasStarted = true;
        _singleFrequencyRetest = false;
        _automaticVerification = false;
        _verificationTarget = null;
        _verificationQueue.Clear();
        _retestOriginalMeasurement = null;
        _states.Clear();
        InitializeStates();

        foreach (var measurement in Session.Measurements)
        {
            if (!_states.TryGetValue(StateKey(measurement.FrequencyHz, measurement.Ear), out var state)) continue;
            state.Completed = true;
            state.Phase = ThresholdPhase.Complete;
            state.LevelDbFs = measurement.ThresholdDbFs;
            state.Presentations = measurement.Presentations;
        }

        if (Session.CompletedAt is not null)
        {
            Ear = EarChannel.Left;
            FrequencyIndex = 0;
        }
        else MoveToFirstMissingMeasurement();

        AppServices.Profiles.SetActive(Session.Id);
        AppServices.Log.Log($"Loaded hearing profile for {(Session.CompletedAt is null ? "resume" : "editing")}: {Session.Name}", LogLevel.Info);
        StateChanged?.Invoke();
    }

    public void SelectReviewEar(EarChannel ear)
    {
        if (!IsComplete) return;
        Ear = ear;
        FrequencyIndex = 0;
        StateChanged?.Invoke();
    }

    public bool BeginSingleFrequencyRetest(EarChannel ear, double frequencyHz)
    {
        if (Session.CompletedAt is null) return false;
        int index = Array.FindIndex(Frequencies, f => Math.Abs(f - frequencyHz) < 0.01);
        if (index < 0) return false;

        Ear = ear;
        FrequencyIndex = index;
        _singleFrequencyRetest = true;
        _automaticVerification = false;
        _retestOriginalMeasurement = Session.Measurements.FirstOrDefault(m =>
            m.Ear == ear && Math.Abs(m.FrequencyHz - frequencyHz) < 0.01);

        var state = State;
        double start = _retestOriginalMeasurement is null
            ? -30.0
            : _retestOriginalMeasurement.Status == HearingMeasurementStatus.Detected
                ? Math.Clamp(_retestOriginalMeasurement.ThresholdDbFs + 4.0, -70.0, -8.0)
                : -11.0;
        state.Reset(start);

        AppServices.Log.Log($"Single-frequency retest started: {ear} {frequencyHz:0.##} Hz. Existing saved value is retained until the retest finishes.", LogLevel.Info);
        StateChanged?.Invoke();
        return true;
    }

    private void InitializeStates()
    {
        foreach (var f in Frequencies)
        {
            _states[StateKey(f, EarChannel.Left)] = new ThresholdState();
            _states[StateKey(f, EarChannel.Right)] = new ThresholdState();
        }
    }

    private static double StateKey(double frequency, EarChannel ear) => frequency + (ear == EarChannel.Right ? 100000 : 0);

    public void RegisterResponse(bool heard)
    {
        var s = State;
        if (s.Completed) return;

        double testedLevel = s.LevelDbFs;
        s.Presentations++;
        s.RecordTrial(testedLevel, heard);
        AppServices.Log.Log(
            $"{Ear} {CurrentFrequency:0.##} Hz @ {testedLevel:0.0} dBFS: {(heard ? "heard" : "not heard")} · {CurrentSearchPhase}",
            LogLevel.Trace);

        switch (s.Phase)
        {
            case ThresholdPhase.Coarse: HandleCoarseResponse(s, heard, testedLevel); break;
            case ThresholdPhase.Refine: HandleRefineResponse(s, heard, testedLevel); break;
            case ThresholdPhase.Confirm: HandleConfirmationResponse(s, heard, testedLevel); break;
        }

        if (s.Completed) return;
        if (s.Presentations >= MaximumPresentationsPerFrequency)
        {
            double estimate = s.BestEstimate();
            AppServices.Log.Log($"Maximum trial count reached at {CurrentFrequency:0.##} Hz; using best bracket estimate {estimate:0.0} dBFS.", LogLevel.Warning);
            s.Completed = true;
            CompleteCurrent(estimate, HearingMeasurementStatus.Detected);
            return;
        }
        StateChanged?.Invoke();
    }

    private void HandleCoarseResponse(ThresholdState s, bool heard, double testedLevel)
    {
        if (heard)
        {
            s.CeilingNotHeardCount = 0;
            if (!s.RegisterHeardBoundary(testedLevel))
            {
                AppServices.Log.Log("Inconsistent threshold response detected; rebuilding the local bracket.", LogLevel.Warning);
                s.ResetNotHeardBoundary();
                s.RegisterHeardBoundary(testedLevel);
            }
            if (s.HasValidBracket) { BeginRefinement(s); return; }
            if (testedLevel <= MinimumLevelDbFs)
            {
                s.Completed = true;
                CompleteCurrent(MinimumLevelDbFs, HearingMeasurementStatus.Detected);
                return;
            }
            s.LevelDbFs = ClampLevel(testedLevel - CoarseStepDb);
        }
        else
        {
            if (!s.RegisterNotHeardBoundary(testedLevel))
            {
                AppServices.Log.Log("Inconsistent threshold response detected; rebuilding the local bracket.", LogLevel.Warning);
                s.ResetHeardBoundary();
                s.RegisterNotHeardBoundary(testedLevel);
            }
            if (s.HasValidBracket) { BeginRefinement(s); return; }
            if (testedLevel >= MaximumLevelDbFs)
            {
                s.CeilingNotHeardCount++;
                if (s.CeilingNotHeardCount < CeilingConfirmationsRequired)
                {
                    s.LevelDbFs = MaximumLevelDbFs;
                    return;
                }
                s.Completed = true;
                CompleteCurrent(MaximumLevelDbFs, HearingMeasurementStatus.NotDetectedAtCeiling);
                return;
            }
            double upwardStep = testedLevel >= HighLevelStepStartDbFs ? HighLevelStepDb : CoarseStepDb;
            s.LevelDbFs = ClampLevel(testedLevel + upwardStep);
        }
    }

    private void HandleRefineResponse(ThresholdState s, bool heard, double testedLevel)
    {
        bool consistent = heard ? s.RegisterHeardBoundary(testedLevel) : s.RegisterNotHeardBoundary(testedLevel);
        if (!consistent || !s.HasValidBracket)
        {
            AppServices.Log.Log("Response contradicted the current bracket; returning to a short coarse search.", LogLevel.Warning);
            if (heard) { s.ResetNotHeardBoundary(); s.RegisterHeardBoundary(testedLevel); }
            else { s.ResetHeardBoundary(); s.RegisterNotHeardBoundary(testedLevel); }
            s.Phase = ThresholdPhase.Coarse;
            s.LevelDbFs = ClampLevel(testedLevel + (heard ? -4.0 : 4.0));
            return;
        }
        ContinueRefinementOrConfirm(s);
    }

    private void HandleConfirmationResponse(ThresholdState s, bool heard, double testedLevel)
    {
        if (heard)
        {
            s.Completed = true;
            CompleteCurrent(s.CandidateThresholdDbFs, HearingMeasurementStatus.Detected);
            return;
        }
        s.ResetHeardBoundary();
        s.RegisterNotHeardBoundary(testedLevel);
        s.Phase = ThresholdPhase.Coarse;
        s.LevelDbFs = ClampLevel(testedLevel + 2.0);
    }

    private void BeginRefinement(ThresholdState s)
    {
        s.Phase = ThresholdPhase.Refine;
        ContinueRefinementOrConfirm(s);
    }

    private void ContinueRefinementOrConfirm(ThresholdState s)
    {
        if (!s.HasValidBracket) { s.Phase = ThresholdPhase.Coarse; return; }
        if (s.BracketWidthDb <= FinalBracketWidthDb)
        {
            s.CandidateThresholdDbFs = s.HeardBoundaryDbFs!.Value;
            s.Phase = ThresholdPhase.Confirm;
            s.LevelDbFs = s.CandidateThresholdDbFs;
            return;
        }
        double midpoint = Math.Round((s.NotHeardBoundaryDbFs!.Value + s.HeardBoundaryDbFs!.Value) / 2.0);
        if (midpoint <= s.NotHeardBoundaryDbFs.Value) midpoint = s.NotHeardBoundaryDbFs.Value + 1.0;
        if (midpoint >= s.HeardBoundaryDbFs.Value) midpoint = s.HeardBoundaryDbFs.Value - 1.0;
        s.LevelDbFs = ClampLevel(midpoint);
    }

    private static double ClampLevel(double level) => Math.Clamp(level, MinimumLevelDbFs, MaximumLevelDbFs);

    private void CompleteCurrent(double threshold, HearingMeasurementStatus status)
    {
        var completedTrials = State.SnapshotTrials();

        if (_automaticVerification && _verificationTarget is not null)
        {
            CompleteAutomaticVerification(threshold, status, completedTrials);
            return;
        }

        if (_singleFrequencyRetest)
        {
            var existing = _retestOriginalMeasurement;
            if (existing is null)
            {
                existing = CreateMeasurement(threshold, status, completedTrials);
                Session.Measurements.Add(existing);
            }
            else
            {
                existing.VerificationThresholdsDbFs.Add(threshold);
                existing.VerificationStatuses.Add(status);
                existing.ThresholdDbFs = threshold;
                existing.Status = status;
                existing.Timestamp = DateTime.Now;
                existing.Presentations += State.Presentations;
                existing.Trials.AddRange(completedTrials);
                existing.VerificationCompleted = true;
                existing.Confidence = status == HearingMeasurementStatus.Detected ? MeasurementConfidence.High : MeasurementConfidence.Low;
            }

            Session.UpdatedAt = DateTime.Now;
            AppServices.Profiles.Save(Session);
            _singleFrequencyRetest = false;
            _retestOriginalMeasurement = null;
            State.Phase = ThresholdPhase.Complete;
            AppServices.Log.Log($"Updated saved point: {Ear} {CurrentFrequency:0.##} Hz = {FormatMeasurement(existing)}", LogLevel.Success);
            MeasurementCompleted?.Invoke(existing);
            StateChanged?.Invoke();
            return;
        }

        var measurement = CreateMeasurement(threshold, status, completedTrials);
        Session.Measurements.RemoveAll(m => m.Ear == Ear && Math.Abs(m.FrequencyHz - CurrentFrequency) < 0.01);
        Session.Measurements.Add(measurement);
        Session.UpdatedAt = DateTime.Now;
        AppServices.Profiles.Autosave(Session);
        MeasurementCompleted?.Invoke(measurement);

        if (FrequencyIndex < Frequencies.Length - 1)
        {
            FrequencyIndex++;
            State.LevelDbFs = Math.Clamp(threshold + 8.0, -70.0, -18.0);
        }
        else if (Ear == EarChannel.Left)
        {
            Ear = EarChannel.Right;
            FrequencyIndex = 0;
            State.LevelDbFs = -30.0;
        }
        else
        {
            BuildVerificationQueue();
            if (_verificationQueue.Count > 0)
            {
                AppServices.Log.Log($"Primary test complete. Verifying {_verificationQueue.Count} unusual local point(s) before finalizing the profile.", LogLevel.Info);
                StartNextAutomaticVerification();
                StateChanged?.Invoke();
                return;
            }
            FinishSession();
        }
        StateChanged?.Invoke();
    }

    private HearingMeasurement CreateMeasurement(double threshold, HearingMeasurementStatus status, List<HearingTrial> trials) => new()
    {
        FrequencyHz = CurrentFrequency,
        Ear = Ear,
        ThresholdDbFs = threshold,
        InitialThresholdDbFs = threshold,
        Presentations = State.Presentations,
        Status = status,
        Timestamp = DateTime.Now,
        Trials = trials,
        Confidence = status == HearingMeasurementStatus.Detected ? MeasurementConfidence.High : MeasurementConfidence.Low
    };

    private void BuildVerificationQueue()
    {
        _verificationQueue.Clear();
        foreach (var ear in new[] { EarChannel.Left, EarChannel.Right })
        {
            var data = Session.Measurements
                .Where(m => m.Ear == ear && m.Status == HearingMeasurementStatus.Detected)
                .OrderBy(m => m.FrequencyHz)
                .ToList();
            var candidates = new List<(HearingMeasurement M, double Deviation)>();
            for (int i = 1; i < data.Count - 1; i++)
            {
                var prev = data[i - 1]; var cur = data[i]; var next = data[i + 1];
                double t = (Math.Log(cur.FrequencyHz) - Math.Log(prev.FrequencyHz)) /
                           (Math.Log(next.FrequencyHz) - Math.Log(prev.FrequencyHz));
                double expected = prev.ThresholdDbFs + ((next.ThresholdDbFs - prev.ThresholdDbFs) * t);
                double deviation = Math.Abs(cur.ThresholdDbFs - expected);
                if (deviation >= OutlierThresholdDb) candidates.Add((cur, deviation));
            }
            foreach (var candidate in candidates.OrderByDescending(x => x.Deviation).Take(MaximumAutomaticVerificationsPerEar))
            {
                candidate.M.WasAutomaticallyFlagged = true;
                candidate.M.Confidence = MeasurementConfidence.Medium;
                _verificationQueue.Enqueue((ear, candidate.M.FrequencyHz));
            }
        }
    }

    private void StartNextAutomaticVerification()
    {
        if (_verificationQueue.Count == 0)
        {
            _automaticVerification = false;
            _verificationTarget = null;
            FinishSession();
            return;
        }

        var item = _verificationQueue.Dequeue();
        Ear = item.Ear;
        FrequencyIndex = Array.FindIndex(Frequencies, f => Math.Abs(f - item.Frequency) < 0.01);
        _verificationTarget = Session.Measurements.First(m => m.Ear == item.Ear && Math.Abs(m.FrequencyHz - item.Frequency) < 0.01);
        _automaticVerification = true;
        State.Reset(Math.Clamp(_verificationTarget.ThresholdDbFs + 4.0, -70.0, -8.0));
        AppServices.Log.Log($"Verification: {Ear} {CurrentFrequency:0.##} Hz (initial {_verificationTarget.ThresholdDbFs:0.0} dBFS)", LogLevel.Info);
    }

    private void CompleteAutomaticVerification(double threshold, HearingMeasurementStatus status, List<HearingTrial> trials)
    {
        var target = _verificationTarget!;
        target.VerificationThresholdsDbFs.Add(threshold);
        target.VerificationStatuses.Add(status);
        target.Presentations += State.Presentations;
        target.Trials.AddRange(trials);

        double initial = target.InitialThresholdDbFs ?? target.ThresholdDbFs;
        double delta = Math.Abs(threshold - initial);
        bool needsSecondVerification = target.VerificationThresholdsDbFs.Count < 2 &&
                                       (status != HearingMeasurementStatus.Detected || delta > 6.0);
        if (needsSecondVerification)
        {
            // A large disagreement or a verification that reaches the test ceiling gets exactly
            // one additional confirmation. No catch trials or blanket repetitions are introduced.
            _verificationQueue.Enqueue((target.Ear, target.FrequencyHz));
            target.Confidence = MeasurementConfidence.Low;
        }
        else
        {
            var detectedVerificationValues = target.VerificationThresholdsDbFs
                .Zip(target.VerificationStatuses, (value, verificationStatus) => new { value, verificationStatus })
                .Where(x => x.verificationStatus == HearingMeasurementStatus.Detected)
                .Select(x => x.value)
                .ToList();

            var values = new List<double> { initial };
            values.AddRange(detectedVerificationValues);
            values.Sort();

            // A ceiling-only verification must not be averaged into a valid detected threshold.
            // Keep the detected measurement, mark it low-confidence, and preserve all trial data.
            if (detectedVerificationValues.Count == 0)
            {
                target.ThresholdDbFs = initial;
                target.Confidence = MeasurementConfidence.Low;
            }
            else
            {
                target.ThresholdDbFs = values.Count >= 3
                    ? values[values.Count / 2]
                    : values.Average();
                double spread = values.Max() - values.Min();
                target.Confidence = spread <= 3.0 ? MeasurementConfidence.High : spread <= 6.0 ? MeasurementConfidence.Medium : MeasurementConfidence.Low;
            }

            target.VerificationCompleted = true;
            target.Timestamp = DateTime.Now;
        }

        AppServices.Profiles.Autosave(Session);
        MeasurementCompleted?.Invoke(target);
        _verificationTarget = null;
        _automaticVerification = false;
        StartNextAutomaticVerification();
        StateChanged?.Invoke();
    }

    private void FinishSession()
    {
        Session.CompletedAt = DateTime.Now;
        Session.UpdatedAt = DateTime.Now;
        Session.CorrectionAlgorithmVersionAtMeasurement = CorrectionPreviewService.AlgorithmVersion;
        AppServices.Profiles.Save(Session);
        AppServices.CorrectionPresets.EnsureDefaultForProfile(Session);
        AppServices.Log.Log("Hearing test completed, unusual points verified where needed, and profile saved.", LogLevel.Success);
    }

    private static string FormatMeasurement(HearingMeasurement m) => m.Status switch
    {
        HearingMeasurementStatus.NotDetectedAtCeiling => $"not detected at {m.ThresholdDbFs:0.0} dBFS ceiling",
        HearingMeasurementStatus.Skipped => "skipped",
        _ => $"{m.ThresholdDbFs:0.0} dBFS"
    };

    public void Skip()
    {
        if (_singleFrequencyRetest)
        {
            _singleFrequencyRetest = false;
            _retestOriginalMeasurement = null;
            StateChanged?.Invoke();
            return;
        }
        if (_automaticVerification)
        {
            _verificationTarget = null;
            _automaticVerification = false;
            StartNextAutomaticVerification();
            StateChanged?.Invoke();
            return;
        }
        if (FrequencyIndex < Frequencies.Length - 1) FrequencyIndex++;
        else if (Ear == EarChannel.Left) { Ear = EarChannel.Right; FrequencyIndex = 0; }
        StateChanged?.Invoke();
    }

    private void MoveToFirstMissingMeasurement()
    {
        foreach (var ear in new[] { EarChannel.Left, EarChannel.Right })
        {
            for (int i = 0; i < Frequencies.Length; i++)
            {
                double f = Frequencies[i];
                bool exists = Session.Measurements.Any(m => m.Ear == ear && Math.Abs(m.FrequencyHz - f) < 0.01);
                if (exists) continue;
                Ear = ear; FrequencyIndex = i;
                var previous = Session.Measurements
                    .Where(m => m.Ear == ear && m.FrequencyHz < f && m.Status == HearingMeasurementStatus.Detected)
                    .OrderByDescending(m => m.FrequencyHz).FirstOrDefault();
                if (previous is not null) State.LevelDbFs = Math.Clamp(previous.ThresholdDbFs + 8.0, -70.0, -18.0);
                return;
            }
        }
        BuildVerificationQueue();
        if (_verificationQueue.Count > 0)
        {
            StartNextAutomaticVerification();
            return;
        }
        FinishSession();
        Ear = EarChannel.Left;
        FrequencyIndex = 0;
    }

    private enum ThresholdPhase { Coarse, Refine, Confirm, Complete }

    private sealed class ThresholdState
    {
        private readonly List<HearingTrial> _trials = new();
        public double LevelDbFs { get; set; } = -30.0;
        public int Presentations { get; set; }
        public bool Completed { get; set; }
        public ThresholdPhase Phase { get; set; } = ThresholdPhase.Coarse;
        public double? NotHeardBoundaryDbFs { get; private set; }
        public double? HeardBoundaryDbFs { get; private set; }
        public double CandidateThresholdDbFs { get; set; }
        public int CeilingNotHeardCount { get; set; }
        public bool HasValidBracket => NotHeardBoundaryDbFs.HasValue && HeardBoundaryDbFs.HasValue && NotHeardBoundaryDbFs.Value < HeardBoundaryDbFs.Value;
        public double BracketWidthDb => HasValidBracket ? HeardBoundaryDbFs!.Value - NotHeardBoundaryDbFs!.Value : double.PositiveInfinity;
        public string BracketText => HasValidBracket ? $"{NotHeardBoundaryDbFs:0.0} … {HeardBoundaryDbFs:0.0} dBFS" : HeardBoundaryDbFs.HasValue ? $"heard ≤ {HeardBoundaryDbFs:0.0} dBFS" : NotHeardBoundaryDbFs.HasValue ? $"not heard ≥ {NotHeardBoundaryDbFs:0.0} dBFS" : "not bracketed";

        public void Reset(double level)
        {
            _trials.Clear(); LevelDbFs = level; Presentations = 0; Completed = false; Phase = ThresholdPhase.Coarse;
            NotHeardBoundaryDbFs = null; HeardBoundaryDbFs = null; CandidateThresholdDbFs = 0; CeilingNotHeardCount = 0;
        }
        public void RecordTrial(double level, bool heard) => _trials.Add(new HearingTrial { LevelDbFs = level, Heard = heard, Timestamp = DateTime.Now });
        public List<HearingTrial> SnapshotTrials() => _trials.Select(t => new HearingTrial { LevelDbFs = t.LevelDbFs, Heard = t.Heard, Timestamp = t.Timestamp }).ToList();
        public bool RegisterHeardBoundary(double level)
        {
            if (NotHeardBoundaryDbFs.HasValue && level <= NotHeardBoundaryDbFs.Value) return false;
            HeardBoundaryDbFs = HeardBoundaryDbFs.HasValue ? Math.Min(HeardBoundaryDbFs.Value, level) : level; return true;
        }
        public bool RegisterNotHeardBoundary(double level)
        {
            if (HeardBoundaryDbFs.HasValue && level >= HeardBoundaryDbFs.Value) return false;
            NotHeardBoundaryDbFs = NotHeardBoundaryDbFs.HasValue ? Math.Max(NotHeardBoundaryDbFs.Value, level) : level; return true;
        }
        public void ResetHeardBoundary() => HeardBoundaryDbFs = null;
        public void ResetNotHeardBoundary() => NotHeardBoundaryDbFs = null;
        public double BestEstimate()
        {
            if (HeardBoundaryDbFs.HasValue) return HeardBoundaryDbFs.Value;
            if (NotHeardBoundaryDbFs.HasValue) return Math.Clamp(NotHeardBoundaryDbFs.Value + 4.0, MinimumLevelDbFs, MaximumLevelDbFs);
            var heard = _trials.Where(t => t.Heard).OrderBy(t => t.LevelDbFs).FirstOrDefault();
            return heard is not null ? heard.LevelDbFs : LevelDbFs;
        }
    }
}
