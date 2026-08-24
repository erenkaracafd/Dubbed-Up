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
    private bool _isInitialized = false;
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

            // Restart the microphone player to pick up the new device
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
        if (_isInitialized)
        {
            // Re-attach the microphone player to a new context node if needed
            if (contextNode is not null && _microphonePlayer is null)
            {
                AttachMicrophonePlayer(contextNode);
            }
            return;
        }

        try
        {
            _recordBusIndex = AudioServer.GetBusIndex(RecordBusName);
            if (_recordBusIndex == -1)
            {
                AudioServer.AddBus();
                _recordBusIndex = AudioServer.BusCount - 1;
                AudioServer.SetBusName(_recordBusIndex, RecordBusName);
                AudioServer.SetBusMute(_recordBusIndex, true); // Mute to prevent feedback loops

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

            if (contextNode is not null)
            {
                AttachMicrophonePlayer(contextNode);
            }

            _isInitialized = true;
            GD.Print($"[Microphone] Initialized on bus '{RecordBusName}'. Device: '{AudioServer.InputDevice}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to initialize: {ex.Message}");
        }
    }

    private void AttachMicrophonePlayer(Node contextNode)
    {
        try
        {
            _microphonePlayer = new AudioStreamPlayer
            {
                Name = "MicrophoneCapturePlayer",
                Bus = RecordBusName,
                Stream = new AudioStreamMicrophone(),
                Autoplay = true
            };
            contextNode.AddChild(_microphonePlayer);
            _microphonePlayer.Play();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to attach microphone player: {ex.Message}");
        }
    }

    public float GetLivePeakLevel(float gainMultiplier = 1.0f)
    {
        if (_recordBusIndex == -1)
        {
            return 0.0f;
        }

        try
        {
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
        if (_recordEffect is null)
        {
            return;
        }
        _recordEffect.SetRecordingActive(true);
    }

    public AudioStreamWav? StopRecording()
    {
        if (_recordEffect is null)
        {
            return null;
        }

        _recordEffect.SetRecordingActive(false);
        return _recordEffect.GetRecording();
    }
}
