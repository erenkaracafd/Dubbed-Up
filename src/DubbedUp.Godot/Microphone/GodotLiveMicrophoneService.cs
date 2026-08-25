using Godot;

namespace DubbedUp.Godot.Microphone;

public sealed class GodotLiveMicrophoneService
{
    private static GodotLiveMicrophoneService? _instance;
    public static GodotLiveMicrophoneService Instance => _instance ??= new();

    private const string RecordBusName = "Record";
    private int _recordBusIndex = -1;
    private AudioEffectRecord? _recordEffect;
    private AudioStreamPlayer? _microphonePlayer;
    private string? _selectedInputDevice;

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
            _selectedInputDevice = deviceName;
            AudioServer.InputDevice = deviceName;

            if (_microphonePlayer is not null && GodotObject.IsInstanceValid(_microphonePlayer))
            {
                _microphonePlayer.Stop();
                _microphonePlayer.Play();
            }

            GD.Print($"[Microphone] Switched input device to: '{deviceName}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to set input device '{deviceName}': {ex.Message}");
        }
    }

    public void Initialize(Node? contextNode = null)
    {
        try
        {
            SetupRecordBus();
            EnsureMicrophonePlayer(contextNode);
            GD.Print($"[Microphone] Initialized on bus '{RecordBusName}'. Device: '{AudioServer.InputDevice}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to initialize: {ex.Message}");
        }
    }

    private void SetupRecordBus()
    {
        _recordBusIndex = AudioServer.GetBusIndex(RecordBusName);
        if (_recordBusIndex == -1)
        {
            AudioServer.AddBus();
            _recordBusIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(_recordBusIndex, RecordBusName);
            AudioServer.SetBusMute(_recordBusIndex, true); // Mute to master output to prevent echo feedback

            _recordEffect = new AudioEffectRecord();
            AudioServer.AddBusEffect(_recordBusIndex, _recordEffect);
        }
        else
        {
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
            GD.Print($"[Microphone] Attached persistent microphone player to node: '{parent.Name}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to attach microphone player: {ex.Message}");
        }
    }

    public float GetLivePeakLevel(float gainMultiplier = 2.0f)
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

            if (maxDb <= -60.0f)
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
