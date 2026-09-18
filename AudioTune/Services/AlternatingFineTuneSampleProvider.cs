using NAudio.Wave;

namespace AudioTune.Services;

public sealed class AlternatingFineTuneSampleProvider : ISampleProvider
{
    private readonly double _referenceFrequency;
    private readonly double _referenceAmplitude;
    private readonly double _testFrequency;
    private readonly double _testAmplitude;
    private readonly int _channel;
    private readonly int _sampleRate;
    private readonly int _toneSamples;
    private readonly int _shortGapSamples;
    private readonly int _cycleGapSamples;
    private readonly int _fadeSamples;
    private long _frame;

    public AlternatingFineTuneSampleProvider(
        double referenceFrequencyHz,
        double referenceLevelDbFs,
        double testFrequencyHz,
        double testLevelDbFs,
        int channel,
        int sampleRate = 48000,
        int toneMilliseconds = 650,
        int shortGapMilliseconds = 250,
        int cycleGapMilliseconds = 650,
        int fadeMilliseconds = 25)
    {
        _referenceFrequency = referenceFrequencyHz;
        _referenceAmplitude = Math.Pow(10.0, referenceLevelDbFs / 20.0);
        _testFrequency = testFrequencyHz;
        _testAmplitude = Math.Pow(10.0, testLevelDbFs / 20.0);
        _channel = channel;
        _sampleRate = sampleRate;
        _toneSamples = sampleRate * toneMilliseconds / 1000;
        _shortGapSamples = sampleRate * shortGapMilliseconds / 1000;
        _cycleGapSamples = sampleRate * cycleGapMilliseconds / 1000;
        _fadeSamples = Math.Max(1, sampleRate * fadeMilliseconds / 1000);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<float> buffer)
    {
        int frames = buffer.Length / 2;
        int cycle = _toneSamples + _shortGapSamples + _toneSamples + _cycleGapSamples;
        for (int i = 0; i < frames; i++)
        {
            int local = (int)(_frame % cycle);
            double sample = 0.0;
            if (local < _toneSamples)
                sample = RenderTone(_referenceFrequency, _referenceAmplitude, local);
            else
            {
                int testStart = _toneSamples + _shortGapSamples;
                if (local >= testStart && local < testStart + _toneSamples)
                    sample = RenderTone(_testFrequency, _testAmplitude, local - testStart);
            }

            int index = i * 2;
            buffer[index] = _channel == 0 ? (float)sample : 0f;
            buffer[index + 1] = _channel == 1 ? (float)sample : 0f;
            _frame++;
        }
        return frames * 2;
    }

    private double RenderTone(double frequency, double amplitude, int local)
    {
        double envelope = 1.0;
        if (local < _fadeSamples) envelope = local / (double)_fadeSamples;
        else if (local >= _toneSamples - _fadeSamples) envelope = Math.Max(0.0, (_toneSamples - local - 1) / (double)_fadeSamples);
        return Math.Sin(2.0 * Math.PI * frequency * _frame / _sampleRate) * amplitude * envelope;
    }
}
