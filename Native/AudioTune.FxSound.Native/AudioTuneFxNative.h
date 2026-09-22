#pragma once

#include <stdint.h>

#if defined(AUDIOTUNE_FXSOUND_NATIVE_EXPORTS)
#define AUDIOTUNE_FX_API __declspec(dllexport)
#else
#define AUDIOTUNE_FX_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef void* AudioTuneFxHandle;

enum AudioTuneFxEffect
{
    AudioTuneFxEffectClarity = 0,
    AudioTuneFxEffectAmbience = 1,
    AudioTuneFxEffectSurround = 2,
    AudioTuneFxEffectDynamicBoost = 3,
    AudioTuneFxEffectBass = 4
};

// Status convention: 0 = success, non-zero = failure.
AUDIOTUNE_FX_API AudioTuneFxHandle __cdecl AudioTuneFx_Create(void);
AUDIOTUNE_FX_API void __cdecl AudioTuneFx_Destroy(AudioTuneFxHandle handle);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_Configure(
    AudioTuneFxHandle handle,
    int32_t sampleRate,
    int32_t channels);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_SetPower(AudioTuneFxHandle handle, int32_t enabled);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_SetEqEnabled(AudioTuneFxHandle handle, int32_t enabled);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_SetEffect(
    AudioTuneFxHandle handle,
    int32_t effect,
    float value0To10);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_SetMasterGain(AudioTuneFxHandle handle, float gainDb);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_SetVolumeLeveling(AudioTuneFxHandle handle, float gainDb);
AUDIOTUNE_FX_API int __cdecl AudioTuneFx_ProcessFloat32Interleaved(
    AudioTuneFxHandle handle,
    const float* input,
    float* output,
    int32_t frameCount);
AUDIOTUNE_FX_API const char* __cdecl AudioTuneFx_GetUpstreamCommit(void);

#ifdef __cplusplus
}
#endif
