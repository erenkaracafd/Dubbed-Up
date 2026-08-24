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

    public void Initialize(Node? contextNode = null)
    {
        if (_isInitialized)
        {
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
                AudioServer.SetBusMute(_recordBusIndex, true); // Mute record bus to prevent feedback loops

                _recordEffect = new AudioEffectRecord();
                AudioServer.AddBusEffect(_recordBusIndex, _recordEffect);
            }
            else
            {
                // Find existing AudioEffectRecord if present
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

            // If context node is provided, attach the microphone capture stream player
            if (contextNode is not null && _microphonePlayer is null)
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

            _isInitialized = true;
            GD.Print("[Microphone] Real microphone service successfully initialized on bus 'Record'.");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Microphone] Failed to initialize microphone service: {ex.Message}");
        }
    }

    public float GetLivePeakLevel(float gainMultiplier = 1.0f)
    {
        if (_recordBusIndex == -1)
        {
            Initialize();
        }

        try
        {
            var peakDb = AudioServer.GetBusPeakVolumeLeftDb(_recordBusIndex, 0);
            var peakRightDb = AudioServer.GetBusPeakVolumeRightDb(_recordBusIndex, 0);
            var maxDb = Math.Max(peakDb, peakRightDb);

            if (maxDb <= -60.0f)
            {
                return 0.0f;
            }

            var linear = Mathf.DbToLinear(maxDb);
            var level = (float)(linear * 100.0 * gainMultiplier);
            return Math.Clamp(level, 0.0f, 100.0f);
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
            Initialize();
        }

        if (_recordEffect is not null)
        {
            _recordEffect.SetRecordingActive(true);
        }
    }

    public AudioStreamWav? StopRecording()
    {
        if (_recordEffect is null)
        {
            return null;
        }

        _recordEffect.SetRecordingActive(false);
        var sample = _recordEffect.GetRecording();
        return sample;
    }
}

