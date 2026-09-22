#include "AudioTuneFxNative.h"

#include "DfxDsp.h"

#include <cmath>
#include <new>

namespace
{
    constexpr const char* UpstreamCommit = "d8e7a23d37ed5939c2a3090a1c1756c7f2500b17";

    DfxDsp* engine(AudioTuneFxHandle handle)
    {
        return static_cast<DfxDsp*>(handle);
    }

    bool finiteInRange(float value, float minimum, float maximum)
    {
        return std::isfinite(value) && value >= minimum && value <= maximum;
    }
}

AudioTuneFxHandle __cdecl AudioTuneFx_Create(void)
{
    try
    {
        return new DfxDsp();
    }
    catch (...)
    {
        return nullptr;
    }
}

void __cdecl AudioTuneFx_Destroy(AudioTuneFxHandle handle)
{
    delete engine(handle);
}

int __cdecl AudioTuneFx_Configure(AudioTuneFxHandle handle, int32_t sampleRate, int32_t channels)
{
    if (handle == nullptr || sampleRate < 8000 || sampleRate > 384000 || channels < 1 || channels > 2)
    {
        return 1;
    }

    // The original DfxDsp API uses short* for historical reasons. Its own
    // implementation explicitly states that this format is 32-bit float.
    return engine(handle)->setSignalFormat(32, channels, sampleRate, 32);
}

int __cdecl AudioTuneFx_SetPower(AudioTuneFxHandle handle, int32_t enabled)
{
    if (handle == nullptr)
    {
        return 1;
    }

    engine(handle)->powerOn(enabled != 0);
    return 0;
}

int __cdecl AudioTuneFx_SetEqEnabled(AudioTuneFxHandle handle, int32_t enabled)
{
    if (handle == nullptr)
    {
        return 1;
    }

    engine(handle)->eqOn(enabled != 0);
    return 0;
}

int __cdecl AudioTuneFx_SetEffect(AudioTuneFxHandle handle, int32_t effect, float value0To10)
{
    if (handle == nullptr || effect < 0 || effect >= DfxDsp::NumEffects || !finiteInRange(value0To10, 0.0f, 10.0f))
    {
        return 1;
    }

    engine(handle)->setEffectValue(static_cast<DfxDsp::Effect>(effect), value0To10);
    return 0;
}

int __cdecl AudioTuneFx_SetMasterGain(AudioTuneFxHandle handle, float gainDb)
{
    if (handle == nullptr || !finiteInRange(gainDb, -60.0f, 24.0f))
    {
        return 1;
    }

    engine(handle)->setMasterGain(gainDb);
    return 0;
}

int __cdecl AudioTuneFx_SetVolumeLeveling(AudioTuneFxHandle handle, float gainDb)
{
    if (handle == nullptr || !finiteInRange(gainDb, 0.0f, 10.0f))
    {
        return 1;
    }

    engine(handle)->setVolumeLeveling(gainDb);
    return 0;
}

int __cdecl AudioTuneFx_ProcessFloat32Interleaved(
    AudioTuneFxHandle handle,
    const float* input,
    float* output,
    int32_t frameCount)
{
    if (handle == nullptr || input == nullptr || output == nullptr || frameCount < 0)
    {
        return 1;
    }

    return engine(handle)->processAudio(
        reinterpret_cast<short int*>(const_cast<float*>(input)),
        reinterpret_cast<short int*>(output),
        frameCount,
        0);
}

const char* __cdecl AudioTuneFx_GetUpstreamCommit(void)
{
    return UpstreamCommit;
}
