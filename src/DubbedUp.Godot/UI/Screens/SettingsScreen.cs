using Godot;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;

namespace DubbedUp.Godot.UI.Screens;

public partial class SettingsScreen : BaseScreen
{
	private CheckButton? _fullscreenCheck;
	private HSlider? _masterVolumeSlider;
	private Label? _masterVolumeValueLabel;
	private HSlider? _micGainSlider;
	private Label? _micGainValueLabel;
	private HSlider? _micLatencySlider;
	private Label? _micLatencyValueLabel;
	private HSlider? _leadInSlider;
	private Label? _leadInValueLabel;
	private HSlider? _countdownSlider;
	private Label? _countdownValueLabel;
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
		_fullscreenCheck = GetNodeOrNull<CheckButton>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/FullscreenCheck");
		_masterVolumeSlider = GetNodeOrNull<HSlider>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/MasterVolHBox/MasterVolumeSlider");
		_masterVolumeValueLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/MasterVolHBox/MasterVolumeValueLabel");

		if (_fullscreenCheck is not null)
		{
			_fullscreenCheck.Toggled += OnFullscreenToggled;
		}

		_speakerTestButton = GetNodeOrNull<Button>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/SpeakerTestButton");
		_leadInSlider = GetNodeOrNull<HSlider>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/LeadInHBox/LeadInSlider");
		_leadInValueLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/LeadInHBox/LeadInValueLabel");
		_countdownSlider = GetNodeOrNull<HSlider>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/CountdownHBox/CountdownSlider");
		_countdownValueLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/LeftCol/CountdownHBox/CountdownValueLabel");

		_micDeviceLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicDeviceLabel");
		_micDeviceOption = GetNodeOrNull<OptionButton>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicDeviceOption");
		_micGainSlider = GetNodeOrNull<HSlider>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicGainHBox/MicGainSlider");
		_micGainValueLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicGainHBox/MicGainValueLabel");
		_micLatencySlider = GetNodeOrNull<HSlider>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicLatencyHBox/MicLatencySlider");
		_micLatencyValueLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicLatencyHBox/MicLatencyValueLabel");
		_micTestButton = GetNodeOrNull<Button>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/MicTestButton");
		_testMeterBar = GetNodeOrNull<ProgressBar>("CenterArea/SettingsCard/CardMargin/VBoxContainer/ColumnsHBox/RightCol/TestMeterBar");

		_statusLabel = GetNodeOrNull<Label>("CenterArea/SettingsCard/CardMargin/VBoxContainer/BottomRow/StatusLabel");
		_saveButton = GetNodeOrNull<Button>("CenterArea/SettingsCard/CardMargin/VBoxContainer/BottomRow/SaveButton");
		_backButton = GetNodeOrNull<Button>("CenterArea/SettingsCard/CardMargin/VBoxContainer/BottomRow/BackButton");

		ApplyStyling();

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

		if (_micLatencySlider is not null)
		{
			_micLatencySlider.ValueChanged += OnMicLatencyChanged;
		}

		if (_leadInSlider is not null)
		{
			_leadInSlider.ValueChanged += val =>
			{
				if (_leadInValueLabel is not null) _leadInValueLabel.Text = $"{val:F1}s";
				AutoSaveSettings();
			};
		}

		if (_countdownSlider is not null)
		{
			_countdownSlider.ValueChanged += val =>
			{
				if (_countdownValueLabel is not null)
				{
					_countdownValueLabel.Text = val > 0 ? $"{val:F0}s" : "Off (0s)";
				}
				AutoSaveSettings();
			};
		}

		if (_micTestButton is not null)
		{
			_micTestButton.Pressed += OnMicTestPressed;
			UiSoundManager.Attach(_micTestButton);
		}

		if (_speakerTestButton is not null)
		{
			_speakerTestButton.Pressed += OnSpeakerTestPressed;
			UiSoundManager.Attach(_speakerTestButton);
		}

		if (_saveButton is not null)
		{
			_saveButton.Pressed += OnSavePressed;
			UiSoundManager.Attach(_saveButton);
		}

		if (_backButton is not null)
		{
			_backButton.Pressed += OnBackPressed;
			UiSoundManager.Attach(_backButton);
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
		_micDeviceOption.AddItem("Default Microphone (System Default)", 0);

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
		if (index == 0 || selectedText.StartsWith("Default"))
		{
			Microphone.GodotLiveMicrophoneService.Instance.SetInputDevice("");
			if (_statusLabel is not null) _statusLabel.Text = "🎙 Switched to Default Microphone.";
		}
		else
		{
			Microphone.GodotLiveMicrophoneService.Instance.SetInputDevice(selectedText);
			if (_statusLabel is not null) _statusLabel.Text = $"🎙 Selected Microphone: {selectedText}";
		}
	}

	private void OnMasterVolumeChanged(double value)
	{
		if (_masterVolumeValueLabel is not null)
		{
			_masterVolumeValueLabel.Text = $"{value:F0}%";
		}

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
				// Linear to dB: 0-100 -> -40dB to +6dB
				var db = Mathf.LinearToDb((float)(value / 100.0));
				AudioServer.SetBusVolumeDb(masterBusIndex, db);
			}
		}
	}

	private void OnMicGainChanged(double value)
	{
		if (_micGainValueLabel is not null)
		{
			_micGainValueLabel.Text = $"{value:F0}%";
		}

		_micGainMultiplier = (float)(value / 100.0);
	}

	private void OnMicLatencyChanged(double value)
	{
		if (_micLatencyValueLabel is not null)
		{
			_micLatencyValueLabel.Text = $"-{value:F0}ms";
		}

		Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = value / 1000.0;
	}

	private void OnMicTestPressed()
	{
		_isTestingMic = !_isTestingMic;

		if (_testMeterBar is not null)
		{
			_testMeterBar.Visible = _isTestingMic;
		}

		if (_micTestButton is not null)
		{
			_micTestButton.Text = _isTestingMic ? "⏹ Stop Microphone Test" : "🎙 Test Microphone Level";
		}

		if (_statusLabel is not null)
		{
			_statusLabel.Text = _isTestingMic
				? "🎙 Mic test active — speak into your microphone to see the level bar move!"
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

	private void OnFullscreenToggled(bool isFullscreen)
	{
		LocalNavigationController.SetFullscreen(isFullscreen, this);
	}

	private void LoadSettings()
	{
		var config = new ConfigFile();
		var err = config.Load("user://audio_settings.cfg");

		var currentMode = DisplayServer.WindowGetMode();
		var isFullscreenNow = currentMode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

		if (err == Error.Ok)
		{
			var isFullscreenSaved = (bool)config.GetValue("Display", "Fullscreen", isFullscreenNow);
			if (_fullscreenCheck is not null) _fullscreenCheck.ButtonPressed = isFullscreenSaved;

			var masterVol = (double)config.GetValue("Audio", "MasterVolume", 100.0);
			var micGain = (double)config.GetValue("Audio", "MicGain", 100.0);
			var micLatency = (double)config.GetValue("Audio", "MicLatencyMs", 150.0);
			var micDevice = (string)config.GetValue("Audio", "MicDevice", "Default");

			var leadIn = (double)config.GetValue("Gameplay", "LeadInSeconds", 3.0);
			var countdown = (double)config.GetValue("Gameplay", "CountdownSeconds", 0.0);

			if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = masterVol;
			if (_micGainSlider is not null) _micGainSlider.Value = micGain;
			if (_micLatencySlider is not null) _micLatencySlider.Value = micLatency;

			if (_leadInSlider is not null)
			{
				_leadInSlider.Value = leadIn;
				if (_leadInValueLabel is not null) _leadInValueLabel.Text = $"{leadIn:F1}s";
			}

			if (_countdownSlider is not null)
			{
				_countdownSlider.Value = countdown;
				if (_countdownValueLabel is not null)
				{
					_countdownValueLabel.Text = countdown > 0 ? $"{countdown:F0}s" : "Off (0s)";
				}
			}

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
			if (_fullscreenCheck is not null) _fullscreenCheck.ButtonPressed = isFullscreenNow;
			if (_masterVolumeSlider is not null) _masterVolumeSlider.Value = 100.0;
			if (_micGainSlider is not null) _micGainSlider.Value = 100.0;
			if (_micLatencySlider is not null) _micLatencySlider.Value = 150.0;
			if (_leadInSlider is not null) _leadInSlider.Value = 3.0;
			if (_countdownSlider is not null) _countdownSlider.Value = 0.0;
			Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = 0.15;
		}
	}

	private void OnSavePressed()
	{
		var config = new ConfigFile();
		config.Load("user://audio_settings.cfg");

		var isFullscreen = _fullscreenCheck?.ButtonPressed ?? false;
		var masterVol = _masterVolumeSlider?.Value ?? 100.0;
		var micGain = _micGainSlider?.Value ?? 100.0;
		var micLatency = _micLatencySlider?.Value ?? 150.0;
		var micDevice = _micDeviceOption is not null
			? _micDeviceOption.GetItemText(_micDeviceOption.Selected)
			: "Default";

		var leadIn = _leadInSlider?.Value ?? 3.0;
		var countdown = _countdownSlider?.Value ?? 0.0;

		config.SetValue("Display", "Fullscreen", isFullscreen);
		config.SetValue("Audio", "MasterVolume", masterVol);
		config.SetValue("Audio", "MicGain", micGain);
		config.SetValue("Audio", "MicLatencyMs", micLatency);
		config.SetValue("Audio", "MicDevice", micDevice);
		config.SetValue("Gameplay", "LeadInSeconds", leadIn);
		config.SetValue("Gameplay", "CountdownSeconds", countdown);
		config.Save("user://audio_settings.cfg");

		Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = micLatency / 1000.0;

		if (_statusLabel is not null)
		{
			_statusLabel.Text = "✅ Settings saved!";
		}
	}

	private void AutoSaveSettings()
	{
		var config = new ConfigFile();
		config.Load("user://audio_settings.cfg");

		var isFullscreen = _fullscreenCheck?.ButtonPressed ?? false;
		var masterVol = _masterVolumeSlider?.Value ?? 100.0;
		var micGain = _micGainSlider?.Value ?? 100.0;
		var micLatency = _micLatencySlider?.Value ?? 150.0;
		var micDevice = _micDeviceOption is not null
			? _micDeviceOption.GetItemText(_micDeviceOption.Selected)
			: "Default";

		var leadIn = _leadInSlider?.Value ?? 3.0;
		var countdown = _countdownSlider?.Value ?? 0.0;

		config.SetValue("Display", "Fullscreen", isFullscreen);
		config.SetValue("Audio", "MasterVolume", masterVol);
		config.SetValue("Audio", "MicGain", micGain);
		config.SetValue("Audio", "MicLatencyMs", micLatency);
		config.SetValue("Audio", "MicDevice", micDevice);
		config.SetValue("Gameplay", "LeadInSeconds", leadIn);
		config.SetValue("Gameplay", "CountdownSeconds", countdown);
		config.Save("user://audio_settings.cfg");

		Microphone.GodotLiveMicrophoneService.Instance.LatencyCompensationSeconds = micLatency / 1000.0;
	}

	private void OnBackPressed()
	{
		_isTestingMic = false;
		AutoSaveSettings();
		Navigator?.NavigateTo(AppScreen.MainMenu);
	}

	private void ApplyStyling()
	{
		var card = GetNodeOrNull<PanelContainer>("CenterArea/SettingsCard");
		if (card is not null)
		{
			var cardStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.965f, 0.980f, 1.000f),
				BorderWidthLeft = 2,
				BorderWidthTop = 2,
				BorderWidthRight = 2,
				BorderWidthBottom = 2,
				BorderColor = new Color(0.780f, 0.850f, 0.950f),
				CornerRadiusTopLeft = 24,
				CornerRadiusTopRight = 24,
				CornerRadiusBottomLeft = 24,
				CornerRadiusBottomRight = 24,
				ShadowColor = new Color(0.12f, 0.18f, 0.35f, 0.10f),
				ShadowSize = 18,
				ShadowOffset = new Vector2(0, 6)
			};
			card.AddThemeStyleboxOverride("panel", cardStyle);
		}

		if (_saveButton is not null)
		{
			var normal = new StyleBoxFlat { BgColor = new Color(1.0f, 0.540f, 0.680f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, ShadowSize = 8, ShadowColor = new Color(1.0f, 0.540f, 0.680f, 0.35f) };
			var hover = new StyleBoxFlat { BgColor = new Color(1.0f, 0.660f, 0.780f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, ShadowSize = 12, ShadowColor = new Color(1.0f, 0.540f, 0.680f, 0.45f) };
			var pressed = new StyleBoxFlat { BgColor = new Color(0.900f, 0.420f, 0.560f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, ShadowSize = 2 };
			_saveButton.AddThemeStyleboxOverride("normal", normal);
			_saveButton.AddThemeStyleboxOverride("hover", hover);
			_saveButton.AddThemeStyleboxOverride("pressed", pressed);
			_saveButton.AddThemeStyleboxOverride("focus", hover);
			_saveButton.AddThemeColorOverride("font_color", Colors.White);
			AudioPlayback.UiSoundManager.Attach(_saveButton);
		}

		if (_backButton is not null)
		{
			var normal = new StyleBoxFlat { BgColor = new Color(0.955f, 0.975f, 1.0f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.780f, 0.850f, 0.950f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22 };
			var hover = new StyleBoxFlat { BgColor = new Color(0.910f, 0.945f, 0.990f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22 };
			_backButton.AddThemeStyleboxOverride("normal", normal);
			_backButton.AddThemeStyleboxOverride("hover", hover);
			_backButton.AddThemeStyleboxOverride("focus", hover);
			_backButton.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
			AudioPlayback.UiSoundManager.Attach(_backButton);
		}

		if (_speakerTestButton is not null)
		{
			AudioPlayback.UiSoundManager.Attach(_speakerTestButton);
		}

		if (_micTestButton is not null)
		{
			AudioPlayback.UiSoundManager.Attach(_micTestButton);
		}
	}
}
