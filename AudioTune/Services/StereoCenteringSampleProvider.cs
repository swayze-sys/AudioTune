using NAudio.Dsp;
using NAudio.Wave;

namespace AudioTune.Services;

/// <summary>
/// Continuous identical L/R band-limited noise passed through AudioTune's current correction.
/// A post-EQ balance control only attenuates one side, so the centering test cannot add clipping gain.
/// </summary>
public sealed class StereoCenteringSampleProvider : ISampleProvider
{
    private readonly BiQuadFilter _highPass;
    private readonly BiQuadFilter _lowPass;
    private readonly BiQuadFilter[] _leftEq;
    private readonly BiQuadFilter[] _rightEq;
    private readonly float _preampLinear;
    private uint _rng = 0xA17D3E21u;
    private volatile float _balanceDb;
    private volatile float _leftTrimLinear = 1f;
    private volatile float _rightTrimLinear = 1f;

    public StereoCenteringSampleProvider(int sampleRate, DspFilterSet filterSet, double initialBalanceDb)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        _highPass = BiQuadFilter.HighPassFilter(sampleRate, 300f, 0.7071f);
        _lowPass = BiQuadFilter.LowPassFilter(sampleRate, 4000f, 0.7071f);
        _leftEq = CreateFilters(sampleRate, filterSet.Left);
        _rightEq = CreateFilters(sampleRate, filterSet.Right);
        _preampLinear = (float)Math.Pow(10.0, Math.Min(0.0, filterSet.AppliedPreampDb) / 20.0);
        BalanceDb = initialBalanceDb;
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Positive moves image right by attenuating left; negative moves it left by attenuating right.</summary>
    public double BalanceDb
    {
        get => _balanceDb;
        set
        {
            float b = (float)Math.Clamp(value, -3.0, 3.0);
            _balanceDb = b;
            var (leftTrimDb, rightTrimDb) = DspFilterService.GetStereoCenterTrims(b);
            _leftTrimLinear = (float)Math.Pow(10.0, leftTrimDb / 20.0);
            _rightTrimLinear = (float)Math.Pow(10.0, rightTrimDb / 20.0);
        }
    }

    public int Read(Span<float> buffer)
    {
        int frames = buffer.Length / 2;
        for (int frame = 0; frame < frames; frame++)
        {
            float mono = NextNoise() * 0.18f;
            mono = _highPass.Transform(mono);
            mono = _lowPass.Transform(mono);

            float left = mono;
            float right = mono;
            foreach (var filter in _leftEq) left = filter.Transform(left);
            foreach (var filter in _rightEq) right = filter.Transform(right);

            buffer[frame * 2] = Math.Clamp(left * _preampLinear * _leftTrimLinear, -1f, 1f);
            buffer[(frame * 2) + 1] = Math.Clamp(right * _preampLinear * _rightTrimLinear, -1f, 1f);
        }
        return frames * 2;
    }

    private float NextNoise()
    {
        // xorshift32: deterministic, allocation-free pseudo-random source.
        uint x = _rng;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _rng = x;
        return ((x & 0x00FFFFFF) / 8388607.5f) - 1f;
    }

    private static BiQuadFilter[] CreateFilters(int sampleRate, IReadOnlyList<ParametricEqFilter> filters)
        => filters
            .Where(x => x.FrequencyHz < sampleRate * 0.47 && Math.Abs(x.GainDb) >= 0.01)
            .Select(x => BiQuadFilter.PeakingEQ(sampleRate, (float)x.FrequencyHz, (float)x.Q, (float)Math.Clamp(x.GainDb, -6.0, 6.0)))
            .ToArray();
}
