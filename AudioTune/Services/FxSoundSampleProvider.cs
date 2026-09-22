using NAudio.Wave;

namespace AudioTune.Services;

internal sealed class FxSoundSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider source;
    private readonly FxSoundEnhancementService settings;
    private readonly FxSoundNativeEngine engine;
    private float[] input = [];
    private float[] output = [];
    private FxSoundEffectSettings? appliedEffects;
    private bool? appliedPower;
    private bool disposed;

    public FxSoundSampleProvider(ISampleProvider source, FxSoundEnhancementService settings)
    {
        this.source = source;
        this.settings = settings;
        engine = new FxSoundNativeEngine();
        engine.Configure(source.WaveFormat.SampleRate, source.WaveFormat.Channels);
        engine.SetEqualizerEnabled(false);
        engine.SetMasterGain(0);
        engine.SetVolumeLeveling(0);
    }

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(Span<float> buffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var read = source.Read(buffer);
        if (read == 0) return 0;

        var enabled = settings.Enabled;
        var effects = settings.CurrentEffects;
        if (appliedPower != enabled)
        {
            engine.SetPower(enabled);
            appliedPower = enabled;
        }
        if (appliedEffects != effects)
        {
            engine.SetEffects(effects);
            appliedEffects = effects;
        }

        if (!enabled) return read;

        EnsureCapacity(read);
        buffer[..read].CopyTo(input);
        engine.Process(input.AsSpan(0, read), output.AsSpan(0, read));
        output.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public void Dispose()
    {
        if (disposed) return;
        engine.Dispose();
        disposed = true;
    }

    private void EnsureCapacity(int count)
    {
        if (input.Length >= count) return;
        input = new float[count];
        output = new float[count];
    }
}
