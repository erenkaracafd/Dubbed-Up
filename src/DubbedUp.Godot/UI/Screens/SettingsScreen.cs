using Godot;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.UI.Controls;

namespace DubbedUp.Godot.UI.Screens;

/// <summary>
/// Settings: sound, recording timing, microphone and display, grouped into cards.
/// Every change is saved immediately to user://audio_settings.cfg. The layout is built in code with <see cref="UiKit"/>.
/// </summary>
public partial class SettingsScreen : BaseScreen
{
    private const string ConfigPath = "user://audio_settings.cfg";

    private ToggleSwitch? _fullscreenSwitch;
    private HSlider? _masterVolumeSlider;
    private HSlider? _micGainSlider;
    private HSlider? _micLatencySlider;
    private HSlider? _leadInSlider;
    private HSlider? _countdownSlider;
    private OptionButton? _micDeviceOption;
    private Button? _micTestButton;
    private LevelMeter? _levelMeter;
    private Label? _statusLabel;

    private bool _isTestingMic;
    private bool _isLoading;
    private float _micGainMultiplier = 1.0f;
    private AudioStreamPlayer? _beepPlayer;

    public override void _Ready()
    {
        _beepPlayer = new AudioStreamPlayer();
        AddChild(_beepPlayer);

        BuildLayout();

        Microphone.GodotLiveMicrophoneService.Instance.Initialize(this);
        PopulateMicrophoneDevices();
        LoadSettings();
    }

    public override void _Process(double delta)
    {
        if (_isTestingMic && _levelMeter is not null)
        {
            _levelMeter.SetLevel(Microphone.GodotLiveMicrophoneService.Instance.GetLivePeakLevel(_micGainMultiplier));
        }
    }

    // ───────────────────────── Layout ─────────────────────────

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 22);
        margin.AddChild(column);

        // Header
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        column.AddChild(header);

        var back = UiKit.CreateButton("Back", "arrow-left", outline: true, UiKit.Soft, UiKit.Ink, OnBackPressed);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(back);

        var titleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleBox.AddThemeConstantOverride("separation", 0);
        header.AddChild(titleBox);
        titleBox.AddChild(UiKit.Heading("Settings", 32));
        titleBox.AddChild(UiKit.Body("Changes are saved automatically", 13));

        _statusLabel = UiKit.Body(string.Empty, 14, UiKit.Mint.Darkened(0.15f));
        _statusLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(_statusLabel);

        // Two columns of cards
        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 22);
        column.AddChild(columns);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 18);
        columns.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 18);
        columns.AddChild(right);

        left.AddChild(BuildSoundCard());
        left.AddChild(BuildRecordingCard());
        right.AddChild(BuildMicrophoneCard());
        right.AddChild(BuildDisplayCard());
    }

    private Control BuildSoundCard()
    {
        var test = UiKit.CreateButton("Test", "volume-2", outline: true, UiKit.Soft, UiKit.Ink, OnSpeakerTestPressed);
        test.CustomMinimumSize = new Vector2(0, 36);
        test.AddThemeFontSizeOverride("font_size", 14);
        var (card, content) = CreateCard("volume-2", UiKit.Sky, "Sound", test);

        _masterVolumeSlider = CreateSlider(0, 100, 1, UiKit.Sky);
        content.AddChild(CreateSliderRow("Volume", null, _masterVolumeSlider, v => $"{v:F0}%"));
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        return card;
    }

    private Control BuildRecordingCard()
    {
        var (card, content) = CreateCard("clapperboard", UiKit.Lilac, "Recording", null);

        _leadInSlider = CreateSlider(0, 3, 0.5, UiKit.Lilac);
        content.AddChild(CreateSliderRow("Lead-in", "How much of the scene plays before your line", _leadInSlider, v => $"{v:F1} s"));
        _leadInSlider.ValueChanged += _ => SaveIfReady();

        _countdownSlider = CreateSlider(0, 5, 1, UiKit.Lilac);
        content.AddChild(CreateSliderRow("Countdown", "Seconds to wait after pressing Record", _countdownSlider, v => v > 0 ? $"{v:F0} s" : "Off"));
        _countdownSlider.ValueChanged += _ => SaveIfReady();
        return card;
    }

    private Control BuildMicrophoneCard()
    {
        var (card, content) = CreateCard("mic", UiKit.Pink, "Microphone", null);

        _micDeviceOption = new OptionButton { CustomMinimumSize = new Vector2(0, 42), FitToLongestItem = false, ClipText = true };
        _micDeviceOption.AddThemeFontSizeOverride("font_size", 14);
        UiKit.StyleOptionButton(_micDeviceOption);
        content.AddChild(_micDeviceOption);

        _micGainSlider = CreateSlider(50, 300, 1, UiKit.Pink);
        content.AddChild(CreateSliderRow("Input gain", null, _micGainSlider, v => $"{v:F0}%"));
        _micGainSlider.ValueChanged += OnMicGainChanged;

        _micLatencySlider = CreateSlider(0, 400, 10, UiKit.Pink);
        content.AddChild(CreateSliderRow("Latency fix", "Shifts your takes earlier to line up with the video", _micLatencySlider, v => $"{v:F0} ms"));
        _micLatencySlider.ValueChanged += OnMicLatencyChanged;

        var testRow = new HBoxContainer();
        testRow.AddThemeConstantOverride("separation", 14);
        content.AddChild(testRow);

        _micTestButton = UiKit.CreateButton("Test mic", "mic", outline: true, UiKit.Soft, UiKit.Ink, OnMicTestPressed);
        _micTestButton.CustomMinimumSize = new Vector2(132, 38);
        _micTestButton.AddThemeFontSizeOverride("font_size", 14);
        testRow.AddChild(_micTestButton);

        _levelMeter = new LevelMeter { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        testRow.AddChild(_levelMeter);
        return card;
    }

    private Control BuildDisplayCard()
    {
        var (card, content) = CreateCard("maximize", UiKit.Mint, "Display", null);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        content.AddChild(row);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 0);
        row.AddChild(text);
        text.AddChild(UiKit.Heading("Fullscreen", 16));
        text.AddChild(UiKit.Body("Toggle anytime with F11", 12));

        _fullscreenSwitch = new ToggleSwitch();
        _fullscreenSwitch.Toggled += OnFullscreenToggled;
        row.AddChild(_fullscreenSwitch);
        return card;
    }

    private static (PanelContainer Card, VBoxContainer Content) CreateCard(string icon, Color accent, string title, Control? headerAction)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 20));

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 14);
        card.AddChild(stack);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        stack.AddChild(header);

        var bubble = new PanelContainer { CustomMinimumSize = new Vector2(38, 38), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var bubbleBox = UiKit.RoundedBox(accent, 19, padding: 9);
        bubbleBox.CornerDetail = 16;
        bubbleBox.ShadowColor = accent with { A = 0.3f };
        bubbleBox.ShadowSize = 8;
        bubble.AddThemeStyleboxOverride("panel", bubbleBox);
        bubble.AddChild(new TextureRect
        {
            Texture = UiIcons.Get(icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(20, 20),
        });
        header.AddChild(bubble);

        var heading = UiKit.Heading(title, 20);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(heading);

        if (headerAction is not null)
        {
            headerAction.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            header.AddChild(headerAction);
        }

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        stack.AddChild(content);
        return (card, content);
    }

    private static HSlider CreateSlider(double min, double max, double step, Color color)
    {
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        UiKit.StyleSlider(slider, color);
        return slider;
    }

    /// <summary>Label (+ optional caption) with a live value chip above a slider.</summary>
    private static Control CreateSliderRow(string title, string? caption, HSlider slider, Func<double, string> format)
    {
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var top = new HBoxContainer();
        row.AddChild(top);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 0);
        top.AddChild(text);
        text.AddChild(UiKit.Heading(title, 16));
        if (caption is not null) text.AddChild(UiKit.Body(caption, 12));

        var value = UiKit.Heading(format(slider.Value), 15);
        value.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        top.AddChild(value);
        slider.ValueChanged += v => value.Text = format(v);

        row.AddChild(slider);
        return row;
    }

    // ───────────────────────── Behaviour ─────────────────────────

    private void PopulateMicrophoneDevices()
    {
        if (_micDeviceOption is null) return;

        _micDeviceOption.Clear();

        var devices = Microphone.GodotLiveMicrophoneService.Instance.GetAvailableInputDevices();
        var currentDevice = Microphone.GodotLiveMicrophoneService.Instance.CurrentInputDevice;

        // Always add default first
        _micDeviceOption.AddItem("Default", 0);

        int selectIdx = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            var dev = devices[i];
            _micDeviceOption.AddItem(dev, i + 1);
            if (dev == currentDevice)
            {
                selectIdx = i + 1;
            }
        }

        _micDeviceOption.Select(selectIdx);
        _micDeviceOption.ItemSelected += OnDeviceSelected;
    }

    private void OnDeviceSelected(long index)
    {
        if (_micDeviceOption is null) return;

        var selectedText = _micDeviceOption.GetItemText((int)index);
        Microphone.GodotLiveMicrophoneService.Instance.SetInputDevice(index == 0 || selectedText.StartsWith("Default") ? "" : selectedText);
        SaveIfReady();
    }

    private void OnMasterVolumeChanged(double value)
    {
        var masterBusIndex = AudioServer.GetBusIndex("Master");
        if (masterBusIndex >= 0)
        {
            if (value <= 0)
            {
                AudioServer.SetBusMute(masterBusIndex, true);
            }
            else
            {
                AudioServer.SetBusMute(masterBusIndex, false);
                AudioServer.SetBusVolumeDb(masterBusIndex, Mathf.LinearToDb((float)(value / 100.0)));
            }
        }

        SaveIfReady();
    }

    private void OnMicGainChanged(double value)
    {
        _micGainMultiplier = (float)(value / 100.0);
        SaveIfReady();
    }

    private void OnMicLatencyChanged(double value)
    {
        Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = value / 1000.0;
        SaveIfReady();
    }

    private void OnMicTestPressed()
    {
        _isTestingMic = !_isTestingMic;
        if (_levelMeter is not null) _levelMeter.Active = _isTestingMic;
        if (_micTestButton is not null)
        {
            _micTestButton.Text = _isTestingMic ? "Stop" : "Test mic";
            UiIcons.Swap(_micTestButton, _isTestingMic ? "square" : "mic");
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
        }
        catch (Exception ex)
        {
            if (_statusLabel is not null) _statusLabel.Text = ex.Message;
        }
    }

    private void OnFullscreenToggled(bool isFullscreen)
    {
        LocalNavigationController.SetFullscreen(isFullscreen, this);
        SaveIfReady();
    }

    private void LoadSettings()
    {
        _isLoading = true;

        var config = new ConfigFile();
        var err = config.Load(ConfigPath);

        var currentMode = DisplayServer.WindowGetMode();
        var isFullscreenNow = currentMode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

        if (err == Error.Ok)
        {
            var isFullscreenSaved = (bool)config.GetValue("Display", "Fullscreen", isFullscreenNow);
            if (_fullscreenSwitch is not null) _fullscreenSwitch.IsOn = isFullscreenSaved;

            var masterVol = (double)config.GetValue("Audio", "MasterVolume", 100.0);
            var micGain = (double)config.GetValue("Audio", "MicGain", 100.0);
            var micLatency = (double)config.GetValue("Audio", "MicLatencyMs", 150.0);
            var micDevice = (string)config.GetValue("Audio", "MicDevice", "Default");

            var leadIn = (double)config.GetValue("Gameplay", "LeadInSeconds", 3.0);
            var countdown = (double)config.GetValue("Gameplay", "CountdownSeconds", 0.0);

            if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = masterVol;
            if (_micGainSlider is not null) _micGainSlider.Value = micGain;
            if (_micLatencySlider is not null) _micLatencySlider.Value = micLatency;
            if (_leadInSlider is not null) _leadInSlider.Value = leadIn;
            if (_countdownSlider is not null) _countdownSlider.Value = countdown;

            Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = micLatency / 1000.0;

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
            if (_fullscreenSwitch is not null) _fullscreenSwitch.IsOn = isFullscreenNow;
            if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = 100.0;
            if (_micGainSlider is not null) _micGainSlider.Value = 100.0;
            if (_micLatencySlider is not null) _micLatencySlider.Value = 150.0;
            if (_leadInSlider is not null) _leadInSlider.Value = 3.0;
            if (_countdownSlider is not null) _countdownSlider.Value = 0.0;
            Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = 0.15;
        }

        _isLoading = false;
    }

    private void SaveIfReady()
    {
        if (_isLoading) return;
        SaveSettings();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = "Saved";
            _statusLabel.Modulate = Colors.White;
            var tween = _statusLabel.CreateTween();
            tween.TweenInterval(1.2);
            tween.TweenProperty(_statusLabel, "modulate:a", 0.0f, 0.4f);
        }
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.Load(ConfigPath);

        var micLatency = _micLatencySlider?.Value ?? 150.0;
        var micDevice = _micDeviceOption is not null && _micDeviceOption.Selected >= 0
            ? _micDeviceOption.GetItemText(_micDeviceOption.Selected)
            : "Default";

        config.SetValue("Display", "Fullscreen", _fullscreenSwitch?.IsOn ?? false);
        config.SetValue("Audio", "MasterVolume", _masterVolumeSlider?.Value ?? 100.0);
        config.SetValue("Audio", "MicGain", _micGainSlider?.Value ?? 100.0);
        config.SetValue("Audio", "MicLatencyMs", micLatency);
        config.SetValue("Audio", "MicDevice", micDevice);
        config.SetValue("Gameplay", "LeadInSeconds", _leadInSlider?.Value ?? 3.0);
        config.SetValue("Gameplay", "CountdownSeconds", _countdownSlider?.Value ?? 0.0);
        config.Save(ConfigPath);

        Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = micLatency / 1000.0;
    }

    private void OnBackPressed()
    {
        _isTestingMic = false;
        SaveSettings();
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
