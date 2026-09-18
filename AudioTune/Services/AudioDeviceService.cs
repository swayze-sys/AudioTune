using AudioTune.Models;
using NAudio.CoreAudioApi;

namespace AudioTune.Services;

public sealed class AudioDeviceService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<AudioDeviceInfo> EnumerateOutputs()
    {
        string? defaultId = null;
        try { defaultId = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID; } catch { }

        return _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName, d.ID == defaultId))
            .OrderByDescending(d => d.IsDefault)
            .ThenBy(d => d.Name)
            .ToList();
    }

    public MMDevice? GetDevice(string? id)
    {
        try
        {
            return string.IsNullOrWhiteSpace(id)
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : _enumerator.GetDevice(id);
        }
        catch { return null; }
    }

    public double? GetMasterVolumePercent(string? id)
    {
        MMDevice? device = null;
        try
        {
            device = GetDevice(id);
            return device?.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0;
        }
        catch
        {
            return null;
        }
        finally
        {
            device?.Dispose();
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
