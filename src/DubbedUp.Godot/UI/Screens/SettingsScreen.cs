using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SettingsScreen : BaseScreen
{
    private HSlider? _masterVolumeSlider;
    private Label? _masterVolumeValueLabel;
    private HSlider? _micGainSlider;
    private Label? _micGainValueLabel;
    private OptionButton? _micDeviceOption;
    private Label? _micDeviceLabel;
    private Button? _micTestButton;
    private ProgressBar? _testMeterBar;
    private Button? _speakerTestButton;
    private Label? _statusLabel;
    private Button? _saveButton;
    private Button? _backButton;

    private bool _isTestingMic = false;
    private float _micGainMultiplier = 1.0f;
    private AudioStreamPlayer? _beepPlayer;

    public override void _Ready()
    {
        _masterVolumeSlider = GetNodeOrNull<HSlider>("CenterContainer/VBoxContainer/FormContainer/MasterVolHBox/MasterVolumeSlider");
        _masterVolumeValueLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/FormContainer/MasterVolHBox/MasterVolumeValueLabel");

        _micGainSlider = GetNodeOrNull<HSlider>("CenterContainer/VBoxContainer/FormContainer/MicGainHBox/MicGainSlider");
        _micGainValueLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/FormContainer/MicGainHBox/MicGainValueLabel");

        _micDeviceLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/FormContainer/MicDeviceLabel");
        _micDeviceOption = GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/FormContainer/MicDeviceOption");

        _micTestButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/FormContainer/MicTestButton");
        _testMeterBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/FormContainer/TestMeterBar");
        _speakerTestButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/FormContainer/SpeakerTestButton");

        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _saveButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ButtonsHBox/SaveButton");
        _backButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ButtonsHBox/BackButton");

        // Beep test player
        _beepPlayer = new AudioStreamPlayer();
        AddChild(_beepPlayer);

        if (_masterVolumeSlider is not null)
        {
            _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        }

        if (_micGainSlider is not null)
        {
            _micGainSlider.ValueChanged += OnMicGainChanged;
        }

        if (_micTestButton is not null)
        {
            _micTestButton.Pressed += OnMicTestPressed;
        }

        if (_speakerTestButton is not null)
        {
            _speakerTestButton.Pressed += OnSpeakerTestPressed;
        }

        if (_saveButton is not null)
        {
            _saveButton.Pressed += OnSavePressed;
        }

        if (_backButton is not null)
        {
            _backButton.Pressed += OnBackPressed;
        }

        Microphone.GodotLiveMicrophoneService.Instance.Initialize(this);
        PopulateMicrophoneDevices();
        LoadSettings();
    }

    public override void _Process(double delta)
    {
        if (_isTestingMic && _testMeterBar is not null)
        {
            var level = Microphone.GodotLiveMicrophoneService.Instance.GetLivePeakLevel(_micGainMultiplier);
            _testMeterBar.Value = Math.Clamp(level, 0.0, 100.0);
        }
    }

    private void PopulateMicrophoneDevices()
    {
        if (_micDeviceOption is null) return;

        _micDeviceOption.Clear();

        var devices = Microphone.GodotLiveMicrophoneService.Instance.GetAvailableInputDevices();
        var currentDevice = Microphone.GodotLiveMicrophoneService.Instance.CurrentInputDevice;

        // Always add default first
        _micDeviceOption.AddItem("Default Microphone", 0);

        var selectIdx = 0;
        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            _micDeviceOption.AddItem(device, i + 1);
            if (device == currentDevice)
            {
                selectIdx = i + 1;
            }
        }

        _micDeviceOption.Select(selectIdx);
        _micDeviceOption.ItemSelected += OnMicDeviceSelected;

        if (_micDeviceLabel is not null)
        {
            _micDeviceLabel.Text = devices.Count == 0
                ? "Microphone Device: (none detected — check system permissions)"
                : $"Microphone Device: ({devices.Count} device(s) found)";
        }
    }

    private void OnMicDeviceSelected(long index)
    {
        if (_micDeviceOption is null) return;

        var deviceName = _micDeviceOption.GetItemText((int)index);

        if (index == 0 || deviceName == "Default Microphone")
        {
            Microphone.GodotLiveMicrophoneService.Instance.SetInputDevice("Default");
        }
        else
        {
            Microphone.GodotLiveMicrophoneService.Instance.SetInputDevice(deviceName);
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"🎙 Microphone set to: {deviceName}";
        }
    }

    private void OnMasterVolumeChanged(double value)
    {
        if (_masterVolumeValueLabel is not null)
        {
            _masterVolumeValueLabel.Text = $"{Math.Round(value)}%";
        }

        var linear = (float)(value / 100.0);
        var db = linear > 0 ? Mathf.LinearToDb(linear) : -80.0f;
        AudioServer.SetBusVolumeDb(0, db);
    }

    private void OnMicGainChanged(double value)
    {
        _micGainMultiplier = (float)(value / 100.0);
        if (_micGainValueLabel is not null)
        {
            _micGainValueLabel.Text = $"{Math.Round(value)}%";
        }
    }

    private void OnMicTestPressed()
    {
        _isTestingMic = !_isTestingMic;

        if (_testMeterBar is not null)
        {
            _testMeterBar.Visible = _isTestingMic;
            if (!_isTestingMic)
            {
                _testMeterBar.Value = 0;
            }
        }

        if (_micTestButton is not null)
        {
            _micTestButton.Text = _isTestingMic ? "⏹ Stop Mic Test" : "🎙 Test Microphone Level";
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = _isTestingMic
                ? "Mic test active — speak into your microphone to see the level bar move!"
                : "Mic test stopped.";
        }
    }

    private void OnSpeakerTestPressed()
    {
        try
        {
            // Generate a short 440Hz beep tone programmatically
            const int sampleRate = 44100;
            const float frequency = 440.0f; // A4 note
            const float durationSeconds = 0.4f;
            const int sampleCount = (int)(sampleRate * durationSeconds);
            var pcm = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = i < sampleRate * 0.02f
                    ? i / (sampleRate * 0.02f)
                    : i > sampleRate * (durationSeconds - 0.05f)
                        ? (sampleCount - i) / (sampleRate * 0.05f)
                        : 1.0f;
                pcm[i] = (short)(Math.Sin(2 * Math.PI * frequency * t) * 28000 * envelope);
            }

            var byteData = new byte[sampleCount * 2];
            Buffer.BlockCopy(pcm, 0, byteData, 0, byteData.Length);

            var wav = new AudioStreamWav
            {
                Data = byteData,
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = sampleRate,
                Stereo = false
            };

            if (_beepPlayer is not null)
            {
                _beepPlayer.Stream = wav;
                _beepPlayer.Play();
            }

            if (_statusLabel is not null)
            {
                _statusLabel.Text = "🔊 Speaker test beep played! Did you hear it?";
            }
        }
        catch (Exception ex)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = $"Speaker test failed: {ex.Message}";
            }
        }
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        var err = config.Load("user://audio_settings.cfg");
        if (err == Error.Ok)
        {
            var masterVol = (double)config.GetValue("Audio", "MasterVolume", 100.0);
            var micGain = (double)config.GetValue("Audio", "MicGain", 100.0);
            var micDevice = (string)config.GetValue("Audio", "MicDevice", "Default");

            if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = masterVol;
            if (_micGainSlider is not null) _micGainSlider.Value = micGain;

            if (micDevice != "Default" && _micDeviceOption is not null)
            {
                for (int i = 0; i < _micDeviceOption.ItemCount; i++)
                {
                    if (_micDeviceOption.GetItemText(i) == micDevice)
                    {
                        _micDeviceOption.Select(i);
                        Microphone.GodotLiveMicrophoneService.Instance.SetInputDevice(micDevice);
                        break;
                    }
                }
            }
        }
        else
        {
            if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = 100.0;
            if (_micGainSlider is not null) _micGainSlider.Value = 100.0;
        }
    }

    private void OnSavePressed()
    {
        var config = new ConfigFile();
        var masterVol = _masterVolumeSlider?.Value ?? 100.0;
        var micGain = _micGainSlider?.Value ?? 100.0;
        var micDevice = _micDeviceOption is not null
            ? _micDeviceOption.GetItemText(_micDeviceOption.Selected)
            : "Default";

        config.SetValue("Audio", "MasterVolume", masterVol);
        config.SetValue("Audio", "MicGain", micGain);
        config.SetValue("Audio", "MicDevice", micDevice);
        config.Save("user://audio_settings.cfg");

        if (_statusLabel is not null)
        {
            _statusLabel.Text = "✅ Settings saved!";
        }
    }

    private void OnBackPressed()
    {
        _isTestingMic = false;
        Navigator?.NavigateTo(AppScreen.Settings == Navigator.CurrentScreen
            ? AppScreen.MainMenu
            : AppScreen.MainMenu);
    }
}
