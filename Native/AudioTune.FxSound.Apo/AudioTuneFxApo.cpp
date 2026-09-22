#include "Vst2Abi.h"

#include "DfxDsp.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <new>
#include <string_view>
#include <vector>

namespace
{
    constexpr std::array<const char*, ParameterCount> ParameterNames = {
        "Power", "Clarity", "Ambience", "Surround", "Dynamic", "Bass"
    };

    constexpr std::int32_t PluginId = FourCc('A', 'T', 'f', 'x');
    constexpr std::int32_t PluginVersion = 0x00040107;

    void CopyText(void* destination, const char* text, std::size_t capacity = 64)
    {
        if (destination == nullptr || capacity == 0)
            return;
        auto* output = static_cast<char*>(destination);
        strncpy_s(output, capacity, text, _TRUNCATE);
    }

    class Plugin final
    {
    public:
        explicit Plugin(AudioMasterCallback audioMaster)
            : audioMaster_(audioMaster)
        {
            effect_.magic = EffectMagic;
            effect_.dispatcher = Dispatch;
            effect_.process = Process;
            effect_.setParameter = SetParameter;
            effect_.getParameter = GetParameter;
            effect_.numPrograms = 1;
            effect_.numParams = ParameterCount;
            effect_.numInputs = 2;
            effect_.numOutputs = 2;
            effect_.flags = EffectCanReplacing;
            effect_.object = this;
            effect_.uniqueID = PluginId;
            effect_.version = PluginVersion;
            effect_.processReplacing = Process;
            parameters_[ParameterPower] = 0.0f;
        }

        AEffect* effect() noexcept { return &effect_; }

    private:
        static Plugin* From(AEffect* effect) noexcept
        {
            return effect == nullptr ? nullptr : static_cast<Plugin*>(effect->object);
        }

        static std::intptr_t __cdecl Dispatch(
            AEffect* effect,
            std::int32_t opcode,
            std::int32_t index,
            std::intptr_t value,
            void* pointer,
            float option)
        {
            auto* plugin = From(effect);
            if (plugin == nullptr)
                return 0;

            switch (opcode)
            {
            case EffOpen:
                return 1;
            case EffClose:
                delete plugin;
                return 1;
            case EffSetSampleRate:
                plugin->sampleRate_ = std::clamp(static_cast<std::int32_t>(std::lround(option)), 8000, 384000);
                plugin->ConfigureEngine();
                return 1;
            case EffSetBlockSize:
                plugin->blockSize_ = std::max<std::int32_t>(0, static_cast<std::int32_t>(value));
                plugin->ReserveBuffers();
                return 1;
            case EffMainsChanged:
                plugin->processing_ = value != 0;
                return 1;
            case EffStartProcess:
                plugin->processing_ = true;
                return 1;
            case EffStopProcess:
                plugin->processing_ = false;
                return 1;
            case EffGetParamName:
                if (index >= 0 && index < ParameterCount)
                    CopyText(pointer, ParameterNames[static_cast<std::size_t>(index)], 32);
                return 1;
            case EffGetParamLabel:
                CopyText(pointer, index == ParameterPower ? "" : "0-10", 16);
                return 1;
            case EffGetParamDisplay:
                if (index >= 0 && index < ParameterCount)
                {
                    if (index == ParameterPower)
                        CopyText(pointer, plugin->parameters_[index] >= 0.5f ? "On" : "Off", 16);
                    else
                    {
                        char text[16]{};
                        std::snprintf(text, sizeof(text), "%.1f", plugin->parameters_[index] * 10.0f);
                        CopyText(pointer, text, 16);
                    }
                }
                return 1;
            case EffGetEffectName:
                CopyText(pointer, "AudioTune FxSound", 64);
                return 1;
            case EffGetVendorString:
                CopyText(pointer, "AudioTune", 64);
                return 1;
            case EffGetProductString:
                CopyText(pointer, "AudioTune FxSound APO Host", 64);
                return 1;
            case EffGetVendorVersion:
                return PluginVersion;
            case EffGetVstVersion:
                return 2400;
            case EffGetPlugCategory:
                return 1; // effect
            case EffCanDo:
                if (pointer != nullptr && std::string_view(static_cast<const char*>(pointer)) == "startStopProcess")
                    return 1;
                return 0;
            default:
                return 0;
            }
        }

        static void __cdecl SetParameter(AEffect* effect, std::int32_t index, float value)
        {
            auto* plugin = From(effect);
            if (plugin == nullptr || index < 0 || index >= ParameterCount || !std::isfinite(value))
                return;
            plugin->parameters_[index] = std::clamp(value, 0.0f, 1.0f);
            plugin->ApplyParameters();
        }

        static float __cdecl GetParameter(AEffect* effect, std::int32_t index)
        {
            auto* plugin = From(effect);
            if (plugin == nullptr || index < 0 || index >= ParameterCount)
                return 0.0f;
            return plugin->parameters_[index];
        }

        static void __cdecl Process(AEffect* effect, float** inputs, float** outputs, std::int32_t frameCount)
        {
            auto* plugin = From(effect);
            if (plugin != nullptr)
                plugin->ProcessBlock(inputs, outputs, frameCount);
        }

        void ConfigureEngine()
        {
            configured_ = engine_.setSignalFormat(32, 2, sampleRate_, 32) == 0;
            engine_.eqOn(false);
            engine_.setMasterGain(0.0f);
            engine_.setVolumeLeveling(0.0f);
            ApplyParameters();
        }

        void ApplyParameters()
        {
            engine_.powerOn(parameters_[ParameterPower] >= 0.5f);
            engine_.setEffectValue(DfxDsp::Fidelity, parameters_[ParameterClarity] * 10.0f);
            engine_.setEffectValue(DfxDsp::Ambience, parameters_[ParameterAmbience] * 10.0f);
            engine_.setEffectValue(DfxDsp::Surround, parameters_[ParameterSurround] * 10.0f);
            engine_.setEffectValue(DfxDsp::DynamicBoost, parameters_[ParameterDynamic] * 10.0f);
            engine_.setEffectValue(DfxDsp::Bass, parameters_[ParameterBass] * 10.0f);
        }

        void ReserveBuffers()
        {
            const auto sampleCount = static_cast<std::size_t>(std::max(blockSize_, 0)) * 2;
            inputInterleaved_.resize(sampleCount);
            outputInterleaved_.resize(sampleCount);
        }

        static void CopyBlock(float** inputs, float** outputs, std::int32_t frameCount)
        {
            if (inputs == nullptr || outputs == nullptr || frameCount <= 0)
                return;
            for (int channel = 0; channel < 2; ++channel)
            {
                if (outputs[channel] == nullptr)
                    continue;
                if (inputs[channel] != nullptr)
                    std::memcpy(outputs[channel], inputs[channel], static_cast<std::size_t>(frameCount) * sizeof(float));
                else
                    std::memset(outputs[channel], 0, static_cast<std::size_t>(frameCount) * sizeof(float));
            }
        }

        void ProcessBlock(float** inputs, float** outputs, std::int32_t frameCount)
        {
            if (inputs == nullptr || outputs == nullptr || frameCount <= 0)
                return;

            const auto sampleCount = static_cast<std::size_t>(frameCount) * 2;
            if (!processing_ || !configured_ || parameters_[ParameterPower] < 0.5f ||
                sampleCount > inputInterleaved_.size() || sampleCount > outputInterleaved_.size())
            {
                CopyBlock(inputs, outputs, frameCount);
                return;
            }

            for (std::int32_t frame = 0; frame < frameCount; ++frame)
            {
                const auto offset = static_cast<std::size_t>(frame) * 2;
                inputInterleaved_[offset] = inputs[0] == nullptr ? 0.0f : inputs[0][frame];
                inputInterleaved_[offset + 1] = inputs[1] == nullptr ? 0.0f : inputs[1][frame];
            }

            const int result = engine_.processAudio(
                reinterpret_cast<short*>(inputInterleaved_.data()),
                reinterpret_cast<short*>(outputInterleaved_.data()),
                frameCount,
                0);
            if (result != 0)
            {
                CopyBlock(inputs, outputs, frameCount);
                return;
            }

            for (std::int32_t frame = 0; frame < frameCount; ++frame)
            {
                const auto offset = static_cast<std::size_t>(frame) * 2;
                if (outputs[0] != nullptr)
                    outputs[0][frame] = outputInterleaved_[offset];
                if (outputs[1] != nullptr)
                    outputs[1][frame] = outputInterleaved_[offset + 1];
            }
        }

        AEffect effect_{};
        AudioMasterCallback audioMaster_{};
        DfxDsp engine_{};
        std::array<float, ParameterCount> parameters_{};
        std::vector<float> inputInterleaved_;
        std::vector<float> outputInterleaved_;
        std::int32_t sampleRate_ = 48000;
        std::int32_t blockSize_ = 0;
        bool configured_ = false;
        bool processing_ = false;
    };
}

extern "C" __declspec(dllexport) AEffect* __cdecl VSTPluginMain(AudioMasterCallback audioMaster)
{
    try
    {
        auto* plugin = new Plugin(audioMaster);
        return plugin->effect();
    }
    catch (...)
    {
        return nullptr;
    }
}
