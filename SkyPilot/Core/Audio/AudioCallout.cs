using System.Speech.Synthesis;

namespace SkyPilot.Core.Audio;

/// <summary>
/// Handles text-to-speech callouts for flight events.
/// </summary>
public class AudioCallout : IDisposable
{
    private readonly SpeechSynthesizer _synth;
    private int _lastAltitudeCallout;
    private string _lastMode = "";
    private bool _lastArmed;
    private float _lastBattery;
    private bool _enabled = true;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public AudioCallout()
    {
        _synth = new SpeechSynthesizer();
        _synth.Rate = 1; // Slightly faster
        _synth.Volume = 100;
        try { _synth.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Adult); } catch { }
    }

    public void Update(SkyPilot.Core.Mavlink.VehicleState state)
    {
        if (!_enabled || !state.IsConnected) return;

        // Altitude callouts every 10m
        int alt = (int)state.AltitudeRel;
        int altRound = (alt / 10) * 10;
        if (altRound != _lastAltitudeCallout && altRound > 0 && altRound <= 200)
        {
            _lastAltitudeCallout = altRound;
            Speak($"{altRound} meters");
        }

        // Mode changes
        string mode = state.FlightModeName;
        if (mode != _lastMode && !string.IsNullOrEmpty(_lastMode))
        {
            _lastMode = mode;
            Speak($"Mode {mode}");
        }
        _lastMode = mode;

        // Arm/Disarm
        if (state.IsArmed != _lastArmed)
        {
            _lastArmed = state.IsArmed;
            Speak(state.IsArmed ? "Armed" : "Disarmed");
        }

        // Low battery warnings
        if (state.BatteryRemaining > 0 && state.BatteryRemaining != _lastBattery)
        {
            if (state.BatteryRemaining <= 20 && _lastBattery > 20)
                Speak($"Warning battery {state.BatteryRemaining} percent");
            else if (state.BatteryRemaining <= 10 && _lastBattery > 10)
                Speak($"Critical battery {state.BatteryRemaining} percent");
            _lastBattery = state.BatteryRemaining;
        }
    }

    public void Speak(string text)
    {
        if (!_enabled) return;
        try
        {
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
        }
        catch { }
    }

    public void Dispose()
    {
        _synth?.Dispose();
    }
}
