#include "../AudioTune.FxSound.Apo/Vst2Abi.h"

#include "DfxDsp.h"

#include <Windows.h>

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <vector>

namespace
{
    using PluginMain = AEffect*(__cdecl*)(AudioMasterCallback);

    std::intptr_t __cdecl AudioMaster(AEffect*, std::int32_t opcode, std::int32_t, std::intptr_t, void*, float)
    {
        return opcode == 1 ? 2400 : 0;
    }

    bool NearlyEqual(float a, float b)
    {
        return std::abs(a - b) <= 1.0e-6f;
    }

    int Fail(const char* message)
    {
        std::fprintf(stderr, "FAIL: %s\n", message);
        return 1;
    }
}

int main()
{
    HMODULE library = LoadLibraryW(L"AudioTune.FxSound.Apo.dll");
    if (library == nullptr)
        return Fail("AudioTune.FxSound.Apo.dll could not be loaded.");

    auto pluginMain = reinterpret_cast<PluginMain>(GetProcAddress(library, "VSTPluginMain"));
    if (pluginMain == nullptr)
        return Fail("VSTPluginMain export is missing.");

    AEffect* effect = pluginMain(AudioMaster);
    if (effect == nullptr || effect->magic != EffectMagic)
        return Fail("The plug-in did not return a valid AEffect.");
    if (effect->numInputs != 2 || effect->numOutputs != 2 || effect->numParams != ParameterCount)
        return Fail("The plug-in channel or parameter layout is invalid.");

    const std::array<const char*, ParameterCount> expectedNames = {
        "Power", "Clarity", "Ambience", "Surround", "Dynamic", "Bass"
    };
    for (int i = 0; i < ParameterCount; ++i)
    {
        char name[32]{};
        effect->dispatcher(effect, EffGetParamName, i, 0, name, 0.0f);
        if (std::strcmp(name, expectedNames[static_cast<std::size_t>(i)]) != 0)
            return Fail("A parameter name does not match the Equalizer APO config contract.");
    }

    constexpr int sampleRate = 48000;
    constexpr int frameCount = 1024;
    std::vector<float> left(frameCount);
    std::vector<float> right(frameCount);
    for (int i = 0; i < frameCount; ++i)
    {
        left[i] = static_cast<float>(0.17 * std::sin(2.0 * 3.141592653589793 * 997.0 * i / sampleRate));
        right[i] = static_cast<float>(0.13 * std::sin(2.0 * 3.141592653589793 * 233.0 * i / sampleRate));
    }
    std::vector<float> outputLeft(frameCount);
    std::vector<float> outputRight(frameCount);
    float* inputs[] = { left.data(), right.data() };
    float* outputs[] = { outputLeft.data(), outputRight.data() };

    effect->dispatcher(effect, EffOpen, 0, 0, nullptr, 0.0f);
    effect->dispatcher(effect, EffSetSampleRate, 0, 0, nullptr, static_cast<float>(sampleRate));
    effect->dispatcher(effect, EffSetBlockSize, 0, frameCount, nullptr, 0.0f);
    effect->dispatcher(effect, EffMainsChanged, 0, 1, nullptr, 0.0f);

    effect->setParameter(effect, ParameterPower, 0.0f);
    effect->processReplacing(effect, inputs, outputs, frameCount);
    for (int i = 0; i < frameCount; ++i)
    {
        if (outputLeft[i] != left[i] || outputRight[i] != right[i])
            return Fail("Power=0 is not sample-transparent.");
    }

    const std::array<float, 5> values = { 0.6f, 0.4f, 0.5f, 0.7f, 0.6f };
    effect->setParameter(effect, ParameterClarity, values[0]);
    effect->setParameter(effect, ParameterAmbience, values[1]);
    effect->setParameter(effect, ParameterSurround, values[2]);
    effect->setParameter(effect, ParameterDynamic, values[3]);
    effect->setParameter(effect, ParameterBass, values[4]);
    effect->setParameter(effect, ParameterPower, 1.0f);
    effect->processReplacing(effect, inputs, outputs, frameCount);

    DfxDsp direct;
    if (direct.setSignalFormat(32, 2, sampleRate, 32) != 0)
        return Fail("Direct DfxDsp format setup failed.");
    direct.eqOn(false);
    direct.setMasterGain(0.0f);
    direct.setVolumeLeveling(0.0f);
    direct.setEffectValue(DfxDsp::Fidelity, 6.0f);
    direct.setEffectValue(DfxDsp::Ambience, 4.0f);
    direct.setEffectValue(DfxDsp::Surround, 5.0f);
    direct.setEffectValue(DfxDsp::DynamicBoost, 7.0f);
    direct.setEffectValue(DfxDsp::Bass, 6.0f);
    direct.powerOn(true);

    std::vector<float> directInput(frameCount * 2);
    std::vector<float> directOutput(frameCount * 2);
    for (int i = 0; i < frameCount; ++i)
    {
        directInput[static_cast<std::size_t>(i) * 2] = left[i];
        directInput[static_cast<std::size_t>(i) * 2 + 1] = right[i];
    }
    if (direct.processAudio(
        reinterpret_cast<short*>(directInput.data()),
        reinterpret_cast<short*>(directOutput.data()),
        frameCount,
        0) != 0)
        return Fail("Direct DfxDsp processing failed.");

    float maximumDifference = 0.0f;
    for (int i = 0; i < frameCount; ++i)
    {
        maximumDifference = (std::max)(maximumDifference, std::abs(outputLeft[i] - directOutput[static_cast<std::size_t>(i) * 2]));
        maximumDifference = (std::max)(maximumDifference, std::abs(outputRight[i] - directOutput[static_cast<std::size_t>(i) * 2 + 1]));
    }
    if (!NearlyEqual(maximumDifference, 0.0f))
        return Fail("The APO host output differs from direct FxSound DfxDsp output.");

    effect->dispatcher(effect, EffStopProcess, 0, 0, nullptr, 0.0f);
    effect->dispatcher(effect, EffClose, 0, 0, nullptr, 0.0f);
    FreeLibrary(library);

    std::printf("PASS: VST2 ABI, bypass and FxSound output are exact (max difference %.6e).\n", maximumDifference);
    return 0;
}
