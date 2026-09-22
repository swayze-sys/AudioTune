using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AudioTune.Services;

public enum FxSoundEffect
{
    Clarity = 0,
    Ambience = 1,
    Surround = 2,
    DynamicBoost = 3,
    Bass = 4
}

public sealed record FxSoundEffectSettings(
    float Clarity,
    float Ambience,
    float Surround,
    float DynamicBoost,
    float Bass)
{
    public static FxSoundEffectSettings Neutral { get; } = new(0, 0, 0, 0, 0);

    public void Validate()
    {
        ValidateValue(Clarity, nameof(Clarity));
        ValidateValue(Ambience, nameof(Ambience));
        ValidateValue(Surround, nameof(Surround));
        ValidateValue(DynamicBoost, nameof(DynamicBoost));
        ValidateValue(Bass, nameof(Bass));
    }

    internal IEnumerable<(FxSoundEffect Effect, float Value)> Enumerate()
    {
        yield return (FxSoundEffect.Clarity, Clarity);
        yield return (FxSoundEffect.Ambience, Ambience);
        yield return (FxSoundEffect.Surround, Surround);
        yield return (FxSoundEffect.DynamicBoost, DynamicBoost);
        yield return (FxSoundEffect.Bass, Bass);
    }

    private static void ValidateValue(float value, string name)
    {
        if (!float.IsFinite(value) || value is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(name, value, "FxSound effect values must be finite and between 0 and 10.");
        }
    }
}

/// <summary>
/// Managed owner for one isolated instance of the original FxSound DfxDsp
/// engine. This class only processes buffers supplied by AudioTune; it does
/// not install a virtual device or modify the Equalizer APO signal path.
/// </summary>
public sealed class FxSoundNativeEngine : IDisposable
{
    private readonly object sync = new();
    private readonly FxSoundSafeHandle handle;
    private int channels;
    private bool disposed;

    public FxSoundNativeEngine()
    {
        handle = NativeMethods.Create();
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new InvalidOperationException("The native FxSound DSP engine could not be created.");
        }
    }

    public static string UpstreamCommit
    {
        get
        {
            var pointer = NativeMethods.GetUpstreamCommit();
            return Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
        }
    }

    public void Configure(int sampleRate, int channelCount)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sampleRate is < 8000 or > 384000)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }
        if (channelCount is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount));
        }

        lock (sync)
        {
            ThrowIfFailed(NativeMethods.Configure(handle, sampleRate, channelCount), "configure signal format");
            channels = channelCount;
        }
    }

    public void SetPower(bool enabled)
    {
        Invoke(() => NativeMethods.SetPower(handle, enabled ? 1 : 0), "change power state");
    }

    public void SetEqualizerEnabled(bool enabled)
    {
        Invoke(() => NativeMethods.SetEqEnabled(handle, enabled ? 1 : 0), "change equalizer state");
    }

    public void SetEffects(FxSoundEffectSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (var (effect, value) in settings.Enumerate())
            {
                ThrowIfFailed(NativeMethods.SetEffect(handle, (int)effect, value), $"set {effect}");
            }
        }
    }

    public void SetMasterGain(float gainDb)
    {
        if (!float.IsFinite(gainDb) || gainDb is < -60 or > 24)
        {
            throw new ArgumentOutOfRangeException(nameof(gainDb));
        }
        Invoke(() => NativeMethods.SetMasterGain(handle, gainDb), "set master gain");
    }

    public void SetVolumeLeveling(float value0To10)
    {
        if (!float.IsFinite(value0To10) || value0To10 is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(value0To10));
        }
        Invoke(() => NativeMethods.SetVolumeLeveling(handle, value0To10), "set volume leveling");
    }

    public void Process(ReadOnlySpan<float> input, Span<float> output)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (channels == 0)
        {
            throw new InvalidOperationException("Configure must be called before processing audio.");
        }
        if (input.Length != output.Length || input.Length % channels != 0)
        {
            throw new ArgumentException("Input and output must have equal, complete interleaved frames.");
        }

        var inputArray = input.ToArray();
        var outputArray = new float[output.Length];
        lock (sync)
        {
            ThrowIfFailed(
                NativeMethods.ProcessFloat32Interleaved(handle, inputArray, outputArray, inputArray.Length / channels),
                "process audio");
        }
        outputArray.CopyTo(output);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        lock (sync)
        {
            if (!disposed)
            {
                handle.Dispose();
                disposed = true;
            }
        }
        GC.SuppressFinalize(this);
    }

    private void Invoke(Func<int> action, string operation)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ThrowIfFailed(action(), operation);
        }
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status != 0)
        {
            throw new InvalidOperationException($"The native FxSound engine failed to {operation} (status {status}).");
        }
    }

    private sealed class FxSoundSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private FxSoundSafeHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.Destroy(handle);
            return true;
        }
    }

    private static class NativeMethods
    {
        private const string Library = "AudioTune.FxSound.Native.dll";

        [DllImport(Library, EntryPoint = "AudioTuneFx_Create", CallingConvention = CallingConvention.Cdecl)]
        internal static extern FxSoundSafeHandle Create();

        [DllImport(Library, EntryPoint = "AudioTuneFx_Destroy", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Destroy(IntPtr handle);

        [DllImport(Library, EntryPoint = "AudioTuneFx_Configure", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Configure(FxSoundSafeHandle handle, int sampleRate, int channels);

        [DllImport(Library, EntryPoint = "AudioTuneFx_SetPower", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetPower(FxSoundSafeHandle handle, int enabled);

        [DllImport(Library, EntryPoint = "AudioTuneFx_SetEqEnabled", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetEqEnabled(FxSoundSafeHandle handle, int enabled);

        [DllImport(Library, EntryPoint = "AudioTuneFx_SetEffect", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetEffect(FxSoundSafeHandle handle, int effect, float value0To10);

        [DllImport(Library, EntryPoint = "AudioTuneFx_SetMasterGain", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetMasterGain(FxSoundSafeHandle handle, float gainDb);

        [DllImport(Library, EntryPoint = "AudioTuneFx_SetVolumeLeveling", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetVolumeLeveling(FxSoundSafeHandle handle, float value0To10);

        [DllImport(Library, EntryPoint = "AudioTuneFx_ProcessFloat32Interleaved", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int ProcessFloat32Interleaved(
            FxSoundSafeHandle handle,
            [In] float[] input,
            [Out] float[] output,
            int frameCount);

        [DllImport(Library, EntryPoint = "AudioTuneFx_GetUpstreamCommit", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr GetUpstreamCommit();
    }
}
