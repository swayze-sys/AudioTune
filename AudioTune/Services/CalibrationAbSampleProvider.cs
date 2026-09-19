using NAudio.Dsp;
using NAudio.Wave;

namespace AudioTune.Services;

/// <summary>
/// A/B provider using the same parametric filter set as the Equalizer APO exporter.
/// Both dry and corrected paths share the same composite headroom, so switching does not
/// introduce a simple preamp loudness jump.
/// </summary>
public sealed class CalibrationAbSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[] _leftFilters;
    private readonly BiQuadFilter[] _rightFilters;
    private readonly float _headroomLinear;
    private readonly float _leftTrimLinear;
    private readonly float _rightTrimLinear;
    private readonly float _mixStepPerFrame;
    private volatile float _targetMix;
    private float _currentMix;

    public CalibrationAbSampleProvider(
        ISampleProvider source,
        IReadOnlyList<ParametricEqFilter> leftFilters,
        IReadOnlyList<ParametricEqFilter> rightFilters,
        double headroomDb,
        double leftTrimDb = 0.0,
        double rightTrimDb = 0.0,
        int crossfadeMilliseconds = 25)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        if (source.WaveFormat.Channels != 2)
            throw new NotSupportedException("The A/B listening test currently requires a stereo music file.");

        HeadroomDb = -Math.Max(0.0, headroomDb);
        _headroomLinear = (float)Math.Pow(10.0, HeadroomDb / 20.0);
        _leftTrimLinear = (float)Math.Pow(10.0, Math.Min(0.0, leftTrimDb) / 20.0);
        _rightTrimLinear = (float)Math.Pow(10.0, Math.Min(0.0, rightTrimDb) / 20.0);
        _leftFilters = CreateFilters(source.WaveFormat.SampleRate, leftFilters);
        _rightFilters = CreateFilters(source.WaveFormat.SampleRate, rightFilters);

        var fadeFrames = Math.Max(1, source.WaveFormat.SampleRate * crossfadeMilliseconds / 1000);
        _mixStepPerFrame = 1f / fadeFrames;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;
    public double HeadroomDb { get; }
    public bool CalibratedSelected => _targetMix >= 0.5f;

    public void SetCalibrated(bool enabled) => _targetMix = enabled ? 1f : 0f;

    public void ResetFilterState()
    {
        foreach (var filter in _leftFilters) filter.ResetState();
        foreach (var filter in _rightFilters) filter.ResetState();
    }

    public int Read(Span<float> buffer)
    {
        var read = _source.Read(buffer);
        if (read <= 0) return 0;

        for (var i = 0; i + 1 < read; i += 2)
        {
            var sourceLeft = buffer[i];
            var sourceRight = buffer[i + 1];

            var wetLeft = sourceLeft;
            var wetRight = sourceRight;
            foreach (var filter in _leftFilters) wetLeft = filter.Transform(wetLeft);
            foreach (var filter in _rightFilters) wetRight = filter.Transform(wetRight);

            if (_currentMix < _targetMix)
                _currentMix = Math.Min(_targetMix, _currentMix + _mixStepPerFrame);
            else if (_currentMix > _targetMix)
                _currentMix = Math.Max(_targetMix, _currentMix - _mixStepPerFrame);

            var dryLeft = sourceLeft * _headroomLinear;
            var dryRight = sourceRight * _headroomLinear;
            wetLeft *= _headroomLinear * _leftTrimLinear;
            wetRight *= _headroomLinear * _rightTrimLinear;

            buffer[i] = Math.Clamp(dryLeft + ((wetLeft - dryLeft) * _currentMix), -1f, 1f);
            buffer[i + 1] = Math.Clamp(dryRight + ((wetRight - dryRight) * _currentMix), -1f, 1f);
        }

        return read;
    }

    private static BiQuadFilter[] CreateFilters(int sampleRate, IReadOnlyList<ParametricEqFilter> filters)
    {
        return filters
            .Where(x => x.FrequencyHz < sampleRate * 0.47 && Math.Abs(x.GainDb) >= 0.01)
            .Select(x => BiQuadFilter.PeakingEQ(
                sampleRate,
                (float)x.FrequencyHz,
                (float)x.Q,
                (float)Math.Clamp(x.GainDb, -CorrectionPreviewService.MaximumCombinedGainDb, CorrectionPreviewService.MaximumCombinedGainDb)))
            .ToArray();
    }
}
