#pragma once

#include <cstdint>

// Minimal VST 2 binary ABI used by Equalizer APO 1.4.2.  This intentionally
// contains only the fields and opcodes needed by AudioTune's headless effect.
struct AEffect;

using AudioMasterCallback = std::intptr_t(__cdecl*)(
    AEffect*, std::int32_t, std::int32_t, std::intptr_t, void*, float);
using DispatcherCallback = std::intptr_t(__cdecl*)(
    AEffect*, std::int32_t, std::int32_t, std::intptr_t, void*, float);
using ProcessCallback = void(__cdecl*)(AEffect*, float**, float**, std::int32_t);
using SetParameterCallback = void(__cdecl*)(AEffect*, std::int32_t, float);
using GetParameterCallback = float(__cdecl*)(AEffect*, std::int32_t);

struct AEffect
{
    std::int32_t magic;
    DispatcherCallback dispatcher;
    ProcessCallback process;
    SetParameterCallback setParameter;
    GetParameterCallback getParameter;
    std::int32_t numPrograms;
    std::int32_t numParams;
    std::int32_t numInputs;
    std::int32_t numOutputs;
    std::int32_t flags;
    void* reserved1;
    void* reserved2;
    std::int32_t initialDelay;
    std::int32_t reserved3;
    std::int32_t reserved4;
    float reserved5;
    void* object;
    void* user;
    std::int32_t uniqueID;
    std::int32_t version;
    ProcessCallback processReplacing;
};

constexpr std::int32_t FourCc(char a, char b, char c, char d)
{
    return (static_cast<std::int32_t>(a) << 24) |
           (static_cast<std::int32_t>(b) << 16) |
           (static_cast<std::int32_t>(c) << 8) |
           static_cast<std::int32_t>(d);
}

constexpr std::int32_t EffectMagic = FourCc('V', 's', 't', 'P');
constexpr std::int32_t EffectCanReplacing = 1 << 4;

enum DispatcherOpcode : std::int32_t
{
    EffOpen = 0,
    EffClose = 1,
    EffGetParamLabel = 6,
    EffGetParamDisplay = 7,
    EffGetParamName = 8,
    EffSetSampleRate = 10,
    EffSetBlockSize = 11,
    EffMainsChanged = 12,
    EffGetPlugCategory = 35,
    EffGetEffectName = 45,
    EffGetVendorString = 47,
    EffGetProductString = 48,
    EffGetVendorVersion = 49,
    EffCanDo = 51,
    EffGetVstVersion = 58,
    EffStartProcess = 71,
    EffStopProcess = 72
};

enum ParameterIndex : std::int32_t
{
    ParameterPower = 0,
    ParameterClarity,
    ParameterAmbience,
    ParameterSurround,
    ParameterDynamic,
    ParameterBass,
    ParameterCount
};
