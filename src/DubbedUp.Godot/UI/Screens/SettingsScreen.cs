using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class SettingsScreen : BaseScreen
{
    private HSlider? _masterVolumeSlider;
    private Label? _masterVolumeValueLabel;
    private HSlider? _micGainSlider;
    private Label? _micGainValueLabel;
    private Button? _micTestButton;
    private ProgressBar? _testMeterBar;
    private Label? _statusLabel;
    private Button? _saveButton;
    private Button? _backButton;

    private bool _isTestingMic = false;
    private double _meterTime = 0.0;
    private float _micGainMultiplier = 1.0f;

    public override void _Ready()
    {
        _masterVolumeSlider = GetNodeOrNull<HSlider>("CenterContainer/VBoxContainer/FormContainer/MasterVolHBox/MasterVolumeSlider");
        _masterVolumeValueLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/FormContainer/MasterVolHBox/MasterVolumeValueLabel");

        _micGainSlider = GetNodeOrNull<HSlider>("CenterContainer/VBoxContainer/FormContainer/MicGainHBox/MicGainSlider");
        _micGainValueLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/FormContainer/MicGainHBox/MicGainValueLabel");

        _micTestButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/FormContainer/MicTestButton");
        _testMeterBar = GetNodeOrNull<ProgressBar>("CenterContainer/VBoxContainer/FormContainer/TestMeterBar");

        _statusLabel = GetNodeOrNull<Label>("CenterContainer/VBoxContainer/StatusLabel");
        _saveButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ButtonsHBox/SaveButton");
        _backButton = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ButtonsHBox/BackButton");

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

        if (_saveButton is not null)
        {
            _saveButton.Pressed += OnSavePressed;
        }

        if (_backButton is not null)
        {
            _backButton.Pressed += OnBackPressed;
        }

        Microphone.GodotLiveMicrophoneService.Instance.Initialize(this);
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

    private void OnMasterVolumeChanged(double value)
    {
        if (_masterVolumeValueLabel is not null)
        {
            _masterVolumeValueLabel.Text = $"{Math.Round(value)}%";
        }

        // Apply to Master bus in dB
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
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        var err = config.Load("user://audio_settings.cfg");
        if (err == Error.Ok)
        {
            var masterVol = (double)config.GetValue("Audio", "MasterVolume", 100.0);
            var micGain = (double)config.GetValue("Audio", "MicGain", 100.0);

            if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = masterVol;
            if (_micGainSlider is not null) _micGainSlider.Value = micGain;
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

        config.SetValue("Audio", "MasterVolume", masterVol);
        config.SetValue("Audio", "MicGain", micGain);
        config.Save("user://audio_settings.cfg");

        if (_statusLabel is not null)
        {
            _statusLabel.Text = "Settings saved successfully!";
        }
    }

    private void OnBackPressed()
    {
        _isTestingMic = false;
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}

