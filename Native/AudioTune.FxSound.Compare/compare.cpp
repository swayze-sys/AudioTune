#include "AudioTuneFxNative.h"
#include "DfxDsp.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <iomanip>
#include <iostream>
#include <memory>
#include <string>
#include <vector>

namespace
{
    constexpr int SampleRate = 48000;
    constexpr int Channels = 2;
    constexpr int Frames = SampleRate * 2;
    constexpr int BlockFrames = 512;

    struct Scenario
    {
        const char* name;
        std::array<float, DfxDsp::NumEffects> effects;
    };

    std::vector<float> makeSignal()
    {
        std::vector<float> signal(Frames * Channels);
        uint32_t randomState = 0x12345678u;

        for (int frame = 0; frame < Frames; ++frame)
        {
            const double time = static_cast<double>(frame) / SampleRate;
            randomState = randomState * 1664525u + 1013904223u;
            const float noise = (static_cast<float>((randomState >> 8) & 0xffffu) / 32767.5f - 1.0f) * 0.002f;
            const float impulse = frame == 0 ? 0.15f : 0.0f;
            signal[frame * 2] = impulse
                + 0.10f * std::sin(2.0 * 3.141592653589793 * 80.0 * time)
                + 0.06f * std::sin(2.0 * 3.141592653589793 * 997.0 * time)
                + 0.03f * std::sin(2.0 * 3.141592653589793 * 12000.0 * time)
                + noise;
            signal[frame * 2 + 1] = impulse
                + 0.09f * std::sin(2.0 * 3.141592653589793 * 125.0 * time)
                + 0.05f * std::sin(2.0 * 3.141592653589793 * 1800.0 * time)
                + 0.025f * std::sin(2.0 * 3.141592653589793 * 14500.0 * time)
                - noise;
        }

        return signal;
    }

    bool configureReference(DfxDsp& dsp, const Scenario& scenario)
    {
        if (dsp.setSignalFormat(32, Channels, SampleRate, 32) != 0)
        {
            return false;
        }

        dsp.powerOn(true);
        dsp.eqOn(false);
        dsp.setMasterGain(0.0f);
        dsp.setVolumeLeveling(0.0f);
        dsp.setBalance(0.0f);
        for (int effect = 0; effect < DfxDsp::NumEffects; ++effect)
        {
            dsp.setEffectValue(static_cast<DfxDsp::Effect>(effect), scenario.effects[effect]);
        }
        return true;
    }

    bool configureAdapter(AudioTuneFxHandle handle, const Scenario& scenario)
    {
        if (AudioTuneFx_Configure(handle, SampleRate, Channels) != 0
            || AudioTuneFx_SetPower(handle, 1) != 0
            || AudioTuneFx_SetEqEnabled(handle, 0) != 0
            || AudioTuneFx_SetMasterGain(handle, 0.0f) != 0
            || AudioTuneFx_SetVolumeLeveling(handle, 0.0f) != 0)
        {
            return false;
        }

        for (int effect = 0; effect < DfxDsp::NumEffects; ++effect)
        {
            if (AudioTuneFx_SetEffect(handle, effect, scenario.effects[effect]) != 0)
            {
                return false;
            }
        }
        return true;
    }

    bool runScenario(const Scenario& scenario)
    {
        const std::vector<float> input = makeSignal();
        std::vector<float> referenceOutput(input.size());
        std::vector<float> adapterOutput(input.size());

        DfxDsp reference;
        using Handle = std::unique_ptr<void, decltype(&AudioTuneFx_Destroy)>;
        Handle adapter(AudioTuneFx_Create(), &AudioTuneFx_Destroy);
        if (!adapter || !configureReference(reference, scenario) || !configureAdapter(adapter.get(), scenario))
        {
            std::cerr << "Configuration failed for scenario: " << scenario.name << '\n';
            return false;
        }

        for (int offset = 0; offset < Frames; offset += BlockFrames)
        {
            const int blockFrames = std::min(BlockFrames, Frames - offset);
            const int sampleOffset = offset * Channels;
            if (reference.processAudio(
                    reinterpret_cast<short int*>(const_cast<float*>(input.data() + sampleOffset)),
                    reinterpret_cast<short int*>(referenceOutput.data() + sampleOffset),
                    blockFrames,
                    0) != 0
                || AudioTuneFx_ProcessFloat32Interleaved(
                    adapter.get(),
                    input.data() + sampleOffset,
                    adapterOutput.data() + sampleOffset,
                    blockFrames) != 0)
            {
                std::cerr << "Processing failed for scenario: " << scenario.name << '\n';
                return false;
            }
        }

        double squaredError = 0.0;
        double maxError = 0.0;
        size_t mismatchCount = 0;
        for (size_t index = 0; index < referenceOutput.size(); ++index)
        {
            const double error = std::abs(static_cast<double>(referenceOutput[index]) - adapterOutput[index]);
            squaredError += error * error;
            maxError = std::max(maxError, error);
            if (error != 0.0)
            {
                ++mismatchCount;
            }
        }

        const double rmsError = std::sqrt(squaredError / referenceOutput.size());
        const double nullDbfs = rmsError > 0.0 ? 20.0 * std::log10(rmsError) : -INFINITY;
        std::cout << std::left << std::setw(22) << scenario.name
                  << " mismatches=" << std::setw(8) << mismatchCount
                  << " max=" << std::scientific << maxError
                  << " rms=" << rmsError
                  << " null_dBFS=" << nullDbfs << '\n';

        // Both paths execute the same pinned source. Any sample difference is
        // therefore an adapter regression, not an acceptable approximation.
        return mismatchCount == 0;
    }
}

int main()
{
    const std::array<Scenario, 7> scenarios = {{
        {"all_zero", {0.0f, 0.0f, 0.0f, 0.0f, 0.0f}},
        {"clarity_5", {5.0f, 0.0f, 0.0f, 0.0f, 0.0f}},
        {"ambience_5", {0.0f, 5.0f, 0.0f, 0.0f, 0.0f}},
        {"surround_5", {0.0f, 0.0f, 5.0f, 0.0f, 0.0f}},
        {"dynamic_5", {0.0f, 0.0f, 0.0f, 5.0f, 0.0f}},
        {"bass_5", {0.0f, 0.0f, 0.0f, 0.0f, 5.0f}},
        {"combined", {6.0f, 4.0f, 5.0f, 7.0f, 6.0f}}
    }};

    std::cout << "FxSound upstream commit: " << AudioTuneFx_GetUpstreamCommit() << '\n';
    bool passed = true;
    for (const Scenario& scenario : scenarios)
    {
        passed = runScenario(scenario) && passed;
    }

    std::cout << (passed ? "PASS: adapter output is sample-identical to direct DfxDsp output.\n"
                          : "FAIL: adapter output differs from direct DfxDsp output.\n");
    return passed ? 0 : 1;
}
