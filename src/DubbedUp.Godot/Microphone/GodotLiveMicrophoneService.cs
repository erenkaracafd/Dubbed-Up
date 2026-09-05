using Godot;

namespace DubbedUp.Godot.Microphone;

public sealed class GodotLiveMicrophoneService
{
    private static GodotLiveMicrophoneService? _instance;
    public static GodotLiveMicrophoneService Instance => _instance ??= new();

    private const string RecordBusName = "Record";
    private const string SinkBusName = "RecordSink";

    private int _recordBusIndex = -1;
    private AudioEffectRecord? _recordEffect;
    private AudioStreamPlayer? _microphonePlayer;
    private string? _selectedInputDevice;
    public double LatencyCompensationSeconds { get; set; } = 0.15; // 150ms default shift back

    public IReadOnlyList<string> GetAvailableInputDevices()
    {
        try
        {
            return AudioServer.GetInputDeviceList();
        }
        catch
        {
            return [];
        }
    }

    public string CurrentInputDevice => _selectedInputDevice ?? AudioServer.InputDevice;

    public void SetInputDevice(string deviceName)
    {
        try
        {
            // Empty string or null means default audio device in Godot WASAPI
            if (string.IsNullOrWhiteSpace(deviceName) || deviceName == "Default" || deviceName == "Default Microphone")
            {
                _selectedInputDevice = null;
                AudioServer.InputDevice = "";
            }
            else
            {
                _selectedInputDevice = deviceName;
                AudioServer.InputDevice = deviceName;
            }

            if (_microphonePlayer is not null && GodotObject.IsInstanceValid(_microphonePlayer))
            {
                _microphonePlayer.Stop();
                _microphonePlayer.Play();
            }

            GD.Print($"[Microphone] Switched input device to: '{AudioServer.InputDevice}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to set input device '{deviceName}': {ex.Message}");
        }
    }

    public void RestartMicrophoneCapture()
    {
        try
        {
            if (_microphonePlayer is not null && GodotObject.IsInstanceValid(_microphonePlayer))
            {
                _microphonePlayer.Stop();
                _microphonePlayer.Play();
                GD.Print("[Microphone] Restarted microphone capture stream.");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to restart capture: {ex.Message}");
        }
    }

    public void ApplyMicGain(float gainMultiplier)
    {
        try
        {
            if (_recordBusIndex == -1)
            {
                _recordBusIndex = AudioServer.GetBusIndex(RecordBusName);
            }

            if (_recordBusIndex != -1)
            {
                var clamped = Math.Clamp(gainMultiplier, 0.0f, 3.0f);
                var db = clamped <= 0.001f ? -80.0f : Mathf.LinearToDb(clamped);
                AudioServer.SetBusVolumeDb(_recordBusIndex, db);
                GD.Print($"[Microphone] Set Record bus gain to {db:F1} dB ({clamped * 100:F0}%)");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to apply mic gain: {ex.Message}");
        }
    }

    public void LoadAudioSettings()
    {
        try
        {
            // Explicitly query input devices so WASAPI driver refreshes endpoints
            var devices = GetAvailableInputDevices();

            var config = new ConfigFile();
            if (config.Load("user://audio_settings.cfg") == Error.Ok)
            {
                var micDevice = (string)config.GetValue("Audio", "MicDevice", "Default");
                var micLatency = (double)config.GetValue("Audio", "MicLatencyMs", 150.0);
                var micGain = (double)config.GetValue("Audio", "MicGain", 100.0);
                var masterVol = (double)config.GetValue("Audio", "MasterVolume", 100.0);

                LatencyCompensationSeconds = micLatency / 1000.0;

                // Apply master volume
                var masterBusIndex = AudioServer.GetBusIndex("Master");
                if (masterBusIndex != -1)
                {
                    if (masterVol <= 0.0)
                    {
                        AudioServer.SetBusMute(masterBusIndex, true);
                    }
                    else
                    {
                        AudioServer.SetBusMute(masterBusIndex, false);
                        AudioServer.SetBusVolumeDb(masterBusIndex, Mathf.LinearToDb((float)(masterVol / 100.0)));
                    }
                }

                // Apply mic device
                if (!string.IsNullOrWhiteSpace(micDevice) && micDevice != "Default" && micDevice != "Default Microphone")
                {
                    var matched = devices.FirstOrDefault(d => string.Equals(d, micDevice, StringComparison.OrdinalIgnoreCase));
                    SetInputDevice(matched ?? micDevice);
                }
                else
                {
                    SetInputDevice("");
                }

                ApplyMicGain((float)(micGain / 100.0));
            }
            else
            {
                SetInputDevice("");
                ApplyMicGain(1.0f);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to load audio settings: {ex.Message}");
        }
    }

    public void Initialize(Node? contextNode = null)
    {
        try
        {
            SetupRecordBus();
            EnsureMicrophonePlayer(contextNode);
            LoadAudioSettings();
            RestartMicrophoneCapture();
            GD.Print($"[Microphone] Initialized on bus '{RecordBusName}' -> '{SinkBusName}'. Device: '{AudioServer.InputDevice}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to initialize: {ex.Message}");
        }
    }

    private void SetupRecordBus()
    {
        // 1. Silent Sink Bus: Volume -80dB, UNMUTED so audio graph keeps pulling samples
        var sinkBusIndex = AudioServer.GetBusIndex(SinkBusName);
        if (sinkBusIndex == -1)
        {
            AudioServer.AddBus();
            sinkBusIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(sinkBusIndex, SinkBusName);
        }

        AudioServer.SetBusMute(sinkBusIndex, false);
        AudioServer.SetBusVolumeDb(sinkBusIndex, -80.0f);

        // 2. Record Bus: Volume 0dB, sends to RecordSink, UNMUTED
        _recordBusIndex = AudioServer.GetBusIndex(RecordBusName);
        if (_recordBusIndex == -1)
        {
            AudioServer.AddBus();
            _recordBusIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(_recordBusIndex, RecordBusName);
        }

        AudioServer.SetBusMute(_recordBusIndex, false);
        AudioServer.SetBusVolumeDb(_recordBusIndex, 0.0f);
        AudioServer.SetBusSend(_recordBusIndex, SinkBusName);

        // 3. AudioEffectRecord Effect
        _recordEffect = null;
        for (int i = 0; i < AudioServer.GetBusEffectCount(_recordBusIndex); i++)
        {
            if (AudioServer.GetBusEffect(_recordBusIndex, i) is AudioEffectRecord effect)
            {
                _recordEffect = effect;
                break;
            }
        }

        if (_recordEffect is null)
        {
            _recordEffect = new AudioEffectRecord();
            AudioServer.AddBusEffect(_recordBusIndex, _recordEffect);
        }
    }

    public void EnsureMicrophonePlayer(Node? fallbackNode = null)
    {
        if (_microphonePlayer is not null && GodotObject.IsInstanceValid(_microphonePlayer) && _microphonePlayer.IsInsideTree())
        {
            if (!_microphonePlayer.Playing)
            {
                _microphonePlayer.Play();
            }
            return;
        }

        try
        {
            Node? parent = null;
            if (Engine.GetMainLoop() is SceneTree tree && tree.Root is not null)
            {
                parent = tree.Root;
            }
            else if (fallbackNode is not null && fallbackNode.IsInsideTree())
            {
                parent = fallbackNode;
            }

            if (parent is null) return;

            _microphonePlayer = new AudioStreamPlayer
            {
                Name = "PersistentMicrophoneCapturePlayer",
                Bus = RecordBusName,
                Stream = new AudioStreamMicrophone(),
                Autoplay = true
            };

            parent.AddChild(_microphonePlayer);
            _microphonePlayer.Play();
            GD.Print($"[Microphone] Attached persistent microphone player to node: '{parent.Name}' on bus '{RecordBusName}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to attach microphone player: {ex.Message}");
        }
    }

    public float GetLivePeakLevel(float gainMultiplier = 3.0f)
    {
        if (_recordBusIndex == -1)
        {
            _recordBusIndex = AudioServer.GetBusIndex(RecordBusName);
            if (_recordBusIndex == -1) return 0.0f;
        }

        try
        {
            EnsureMicrophonePlayer();

            var peakLeft = AudioServer.GetBusPeakVolumeLeftDb(_recordBusIndex, 0);
            var peakRight = AudioServer.GetBusPeakVolumeRightDb(_recordBusIndex, 0);
            var maxDb = Math.Max(peakLeft, peakRight);

            if (maxDb <= -60.0f || float.IsNegativeInfinity(maxDb) || float.IsNaN(maxDb))
            {
                return 0.0f;
            }

            var linear = Mathf.DbToLinear(maxDb);
            return Math.Clamp(linear * 100.0f * gainMultiplier, 0.0f, 100.0f);
        }
        catch
        {
            return 0.0f;
        }
    }

    public void StartRecording()
    {
        SetupRecordBus();
        EnsureMicrophonePlayer();

        if (_recordEffect is not null)
        {
            _recordEffect.SetRecordingActive(true);
            GD.Print("[Microphone] AudioEffectRecord activated.");
        }
    }

    public AudioStreamWav? StopRecording()
    {
        if (_recordEffect is null)
        {
            return null;
        }

        _recordEffect.SetRecordingActive(false);
        var recording = _recordEffect.GetRecording();
        GD.Print($"[Microphone] AudioEffectRecord stopped. Captured sample: {recording != null}");
        return recording;
    }
}
