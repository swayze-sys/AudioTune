using NAudio.Wave;

namespace AudioTune.Services;

public sealed class PulseToneSampleProvider : ISampleProvider
{
    private readonly double _frequency;
    private readonly double _amplitude;
    private readonly int _channel;
    private readonly int _sampleRate;
    private readonly int _pulseSamples;
    private readonly int _gapSamples;
    private readonly int _fadeSamples;
    private readonly int _pulseCount;
    private readonly int _sequenceSamples;
    private readonly int _sequenceGapSamples;
    private readonly bool _repeatForever;
    private long _frame;

    public PulseToneSampleProvider(
        double frequencyHz,
        double levelDbFs,
        int channel,
        int sampleRate = 48000,
        int pulseMilliseconds = 500,
        int gapMilliseconds = 150,
        int pulseCount = 3,
        int fadeMilliseconds = 25,
        bool repeatForever = false,
        int sequenceGapMilliseconds = 450)
    {
        _frequency = frequencyHz;
        _amplitude = Math.Pow(10.0, levelDbFs / 20.0);
        _channel = channel;
        _sampleRate = sampleRate;
        _pulseSamples = sampleRate * pulseMilliseconds / 1000;
        _gapSamples = sampleRate * gapMilliseconds / 1000;
        _fadeSamples = Math.Max(1, sampleRate * fadeMilliseconds / 1000);
        _pulseCount = Math.Max(1, pulseCount);
        _sequenceSamples = (_pulseSamples * _pulseCount) + (_gapSamples * (_pulseCount - 1));
        _sequenceGapSamples = Math.Max(0, sampleRate * sequenceGapMilliseconds / 1000);
        _repeatForever = repeatForever;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<float> buffer)
    {
        int framesRequested = buffer.Length / 2;
        int framesWritten = 0;
        long finiteLength = _sequenceSamples;
        int repeatingCycle = _sequenceSamples + _sequenceGapSamples;

        for (int i = 0; i < framesRequested; i++)
        {
            if (!_repeatForever && _frame >= finiteLength)
                break;

            long sequenceFrame = _repeatForever
                ? _frame % repeatingCycle
                : _frame;

            double sample = 0.0;
            if (sequenceFrame < _sequenceSamples)
            {
                int pulseCycle = _pulseSamples + _gapSamples;
                int pulseIndex = (int)(sequenceFrame / pulseCycle);
                int local = (int)(sequenceFrame % pulseCycle);

                if (pulseIndex < _pulseCount && local < _pulseSamples)
                {
                    double envelope = 1.0;
                    if (local < _fadeSamples)
                        envelope = local / (double)_fadeSamples;
                    else if (local >= _pulseSamples - _fadeSamples)
                        envelope = Math.Max(0.0, (_pulseSamples - local - 1) / (double)_fadeSamples);

                    // Use the absolute frame for continuous phase. The fade makes sequence boundaries click-free.
                    sample = Math.Sin(2.0 * Math.PI * _frequency * _frame / _sampleRate) * _amplitude * envelope;
                }
            }

            int index = i * 2;
            buffer[index] = _channel == 0 ? (float)sample : 0f;
            buffer[index + 1] = _channel == 1 ? (float)sample : 0f;
            _frame++;
            framesWritten++;
        }

        return framesWritten * 2;
    }
}
