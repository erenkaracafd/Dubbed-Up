using System.Text.RegularExpressions;
using DubbedUp.Core.Characters;
using DubbedUp.Core.ProjectFormat;
using DubbedUp.Core.Scenes;
using DubbedUp.Core.Timeline;
using DubbedUp.Godot.Ai;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.UI.Controls;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

/// <summary>
/// Scene editor: video preview with transport controls, an editable list of dialogue lines
/// and a waveform timeline with draggable speech boxes. The layout is built in code with <see cref="UiKit"/>.
/// </summary>
public partial class SceneEditorScreen : BaseScreen
{
    private LineEdit? _titleInput;
    private SpinBox? _durationInput;
    private Label? _metaLabel;
    private AspectRatioContainer? _videoContainer;
    private VideoStreamPlayer? _videoPlayer;
    private Button? _playPauseButton;
    private Label? _videoTimeLabel;
    private HSlider? _timeSlider;
    private ScrollContainer? _linesScroll;
    private VBoxContainer? _slotsContainer;
    private Label? _lineCountLabel;
    private Label? _statusLabel;

    private TimelineWaveformEditor? _timelineEditor;
    private readonly List<EditableVoiceSlot> _editableSlots = [];
    private int _selectedSlotIndex = -1;
    private bool _isPreviewingSlot = false;
    private double _previewEndSec = 0.0;
    private bool _isSliderDragging = false;
    private bool _isPlaying = false;
    private bool _isDetecting = false;
    private double _totalDuration = 22.0;

    private float[]? _currentWaveform;
    private string? _currentWavPath;
    private double _maxSourceDuration = 0.0;

    private sealed class EditableVoiceSlot
    {
        public string SlotId { get; set; } = "slot-1";
        public string CharacterId { get; set; } = "char-1";
        public string CharacterName { get; set; } = "Character";
        public string Prompt { get; set; } = "Subtitle prompt";
        public double StartSeconds { get; set; } = 0.0;
        public double EndSeconds { get; set; } = 4.0;
    }

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);
        LoadSceneData();

        // Dev aid alongside --start-screen: `-- --start-screen=SceneEditor --start-scene=<id> --auto-detect`.
        if (OS.GetCmdlineUserArgs().Contains("--auto-detect"))
        {
            Callable.From(OnAutoDetectPressed).CallDeferred();
        }
    }

    public override void _Ready()
    {
        BuildLayout();
        LocalAiSceneExtractor.WarmUpWhisper();

        if (Coordinator is not null)
        {
            LoadSceneData();
        }
    }

    // ───────────────────────── Layout ─────────────────────────

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 24);
        }
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        margin.AddChild(column);

        column.AddChild(BuildHeader());

        var main = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        main.AddThemeConstantOverride("separation", 16);
        column.AddChild(main);
        main.AddChild(BuildPreviewColumn());
        main.AddChild(BuildLinesCard());

        column.AddChild(BuildTimelineCard());
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);

        var back = UiKit.CreateButton("Back", "arrow-left", outline: true, UiKit.Soft, UiKit.Ink, OnBackPressed);
        back.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(back);

        var titleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleBox.AddThemeConstantOverride("separation", -2);
        header.AddChild(titleBox);

        _titleInput = new LineEdit { PlaceholderText = "Scene title", MaxLength = 80, CaretBlink = true };
        StyleTitleInput(_titleInput);
        titleBox.AddChild(_titleInput);

        _metaLabel = UiKit.Body(string.Empty, 13);
        var metaRow = new HBoxContainer();
        metaRow.AddThemeConstantOverride("separation", 10);
        metaRow.AddChild(new Control { CustomMinimumSize = new Vector2(2, 0) });
        metaRow.AddChild(_metaLabel);
        titleBox.AddChild(metaRow);

        var lengthBox = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        lengthBox.AddThemeConstantOverride("separation", 8);
        header.AddChild(lengthBox);
        var lengthLabel = UiKit.Body("Length", 13);
        lengthLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        lengthBox.AddChild(lengthLabel);
        _durationInput = new SpinBox { MinValue = 1.0, MaxValue = 600.0, Step = 0.5, Value = 22.0, Suffix = "s", CustomMinimumSize = new Vector2(104, 40) };
        StyleFieldInput(_durationInput.GetLineEdit(), 40);
        _durationInput.ValueChanged += OnDurationChanged;
        lengthBox.AddChild(_durationInput);

        _statusLabel = UiKit.Body(string.Empty, 13);
        _statusLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(_statusLabel);

        var save = UiKit.CreateButton("Save", "save", outline: true, UiKit.Soft, UiKit.Ink, OnSavePressed);
        save.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(save);

        var saveAndExit = UiKit.CreateButton("Save & Exit", "check", outline: false, UiKit.Pink, Colors.White, OnSaveAndProceedPressed);
        saveAndExit.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(saveAndExit);

        return header;
    }

    private Control BuildPreviewColumn()
    {
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.25f };
        left.AddThemeConstantOverride("separation", 10);

        // Rounded dark frame; the video keeps its aspect ratio inside it
        var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill, ClipChildren = ClipChildrenMode.AndDraw };
        var frameBox = UiKit.RoundedBox(new Color(0.07f, 0.06f, 0.16f), 22);
        frameBox.ShadowColor = new Color(0.07f, 0.06f, 0.16f, 0.22f);
        frameBox.ShadowSize = 18;
        frameBox.ShadowOffset = new Vector2(0, 8);
        frame.AddThemeStyleboxOverride("panel", frameBox);
        left.AddChild(frame);

        _videoContainer = new AspectRatioContainer
        {
            Ratio = 16f / 9f,
            AlignmentHorizontal = AspectRatioContainer.AlignmentMode.Center,
            AlignmentVertical = AspectRatioContainer.AlignmentMode.Center,
        };
        frame.AddChild(_videoContainer);
        _videoPlayer = new VideoStreamPlayer { Expand = true };
        _videoContainer.AddChild(_videoPlayer);

        // Transport bar
        var transport = new PanelContainer();
        transport.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 8));
        left.AddChild(transport);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        transport.AddChild(row);

        row.AddChild(CreateSmallButton("-5s", () => SeekRelative(-5.0)));

        _playPauseButton = UiKit.CreateButton("Play", "play", outline: false, UiKit.Sky, Colors.White, OnPlayPausePressed);
        _playPauseButton.CustomMinimumSize = new Vector2(104, 40);
        row.AddChild(_playPauseButton);

        row.AddChild(CreateSmallButton("+5s", () => SeekRelative(5.0)));
        row.AddChild(UiKit.CreateIconButton("rotate-ccw", UiKit.Soft, UiKit.Ink, OnStopPressed, 40));

        _timeSlider = new HSlider { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, Step = 0.1, MaxValue = 22.0 };
        StyleSlider(_timeSlider);
        _timeSlider.DragStarted += () => _isSliderDragging = true;
        _timeSlider.DragEnded += _ =>
        {
            _isSliderDragging = false;
            if (_timeSlider is not null) SeekTo(_timeSlider.Value);
        };
        _timeSlider.ValueChanged += val =>
        {
            if (_isSliderDragging) SeekTo(val);
        };
        row.AddChild(_timeSlider);

        _videoTimeLabel = UiKit.Heading("0.0s / 22.0s", 14);
        _videoTimeLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _videoTimeLabel.CustomMinimumSize = new Vector2(96, 0);
        _videoTimeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(_videoTimeLabel);

        return left;
    }

    private Control BuildLinesCard()
    {
        var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        card.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 16));

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        card.AddChild(stack);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        stack.AddChild(header);

        var title = UiKit.Heading("Lines", 20);
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(title);
        _lineCountLabel = UiKit.Body(string.Empty, 13);
        _lineCountLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _lineCountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_lineCountLabel);

        var autoDetect = UiKit.CreateButton("Auto-detect", "audio-waveform", outline: true, new Color(0.96f, 0.95f, 1.0f), UiKit.Lilac, OnAutoDetectPressed);
        autoDetect.CustomMinimumSize = new Vector2(0, 38);
        autoDetect.AddThemeFontSizeOverride("font_size", 14);
        header.AddChild(autoDetect);

        var add = UiKit.CreateButton("Add", "plus", outline: false, UiKit.Lilac, Colors.White, OnAddBoxPressed);
        add.CustomMinimumSize = new Vector2(0, 38);
        add.AddThemeFontSizeOverride("font_size", 14);
        header.AddChild(add);

        _linesScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        UiKit.StyleScrollBar(_linesScroll.GetVScrollBar());
        stack.AddChild(_linesScroll);

        var padding = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        padding.AddThemeConstantOverride("margin_right", 8);
        _linesScroll.AddChild(padding);

        _slotsContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _slotsContainer.AddThemeConstantOverride("separation", 8);
        padding.AddChild(_slotsContainer);

        return card;
    }

    private Control BuildTimelineCard()
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UiKit.CardBox(padding: 14));

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        card.AddChild(stack);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 8);
        stack.AddChild(toolbar);

        var title = UiKit.Heading("Timeline", 18);
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        toolbar.AddChild(title);
        var hint = UiKit.Body("Drag the dot to move a box, its edges to resize", 12);
        hint.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        toolbar.AddChild(hint);

        var split = UiKit.CreateButton("Split (C)", "scissors", outline: true, new Color(1.0f, 0.97f, 0.92f), new Color(0.85f, 0.50f, 0.10f), OnCutSelectedBoxPressed);
        split.CustomMinimumSize = new Vector2(0, 36);
        split.AddThemeFontSizeOverride("font_size", 14);
        toolbar.AddChild(split);

        var delete = UiKit.CreateButton("Delete", "trash-2", outline: true, new Color(1.0f, 0.95f, 0.96f), UiKit.Danger, OnDeleteSelectedBoxPressed);
        delete.CustomMinimumSize = new Vector2(0, 36);
        delete.AddThemeFontSizeOverride("font_size", 14);
        toolbar.AddChild(delete);

        _timelineEditor = new TimelineWaveformEditor { CustomMinimumSize = new Vector2(0, 124) };
        stack.AddChild(_timelineEditor);
        _timelineEditor.SeekRequested += OnTimelineSeekRequested;
        _timelineEditor.SlotSelected += OnTimelineSlotSelected;
        _timelineEditor.SlotChanged += OnTimelineSlotChanged;
        _timelineEditor.SlotDeleteRequested += DeleteSlotAtIndex;
        _timelineEditor.SlotSplitRequested += SplitSlotAtIndex;

        return card;
    }

    private static Button CreateSmallButton(string text, Action onPressed)
    {
        var button = UiKit.CreateButton(text, null, outline: true, UiKit.Soft, UiKit.Ink, onPressed);
        button.CustomMinimumSize = new Vector2(58, 40);
        button.AddThemeFontSizeOverride("font_size", 14);
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            if (button.GetThemeStylebox(state) is StyleBoxFlat box)
            {
                box.ContentMarginLeft = 10;
                box.ContentMarginRight = 10;
            }
        }
        return button;
    }

    private static void StyleTitleInput(LineEdit input)
    {
        StyleBoxFlat Make(Color bg, Color border) => new()
        {
            BgColor = bg,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = border,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            AntiAliasing = true,
        };

        input.AddThemeStyleboxOverride("normal", Make(new Color(1, 1, 1, 0), new Color(1, 1, 1, 0)));
        input.AddThemeStyleboxOverride("focus", Make(new Color(1, 1, 1, 0.9f), new Color(0.38f, 0.66f, 1.0f)));
        input.AddThemeColorOverride("font_color", UiKit.Ink);
        input.AddThemeColorOverride("font_placeholder_color", UiKit.Muted with { A = 0.7f });
        input.AddThemeFontSizeOverride("font_size", 28);
        var headingFont = ThemeDB.GetProjectTheme()?.GetFont("font", "HeadingLabel");
        if (headingFont is not null) input.AddThemeFontOverride("font", headingFont);
    }

    private static void StyleFieldInput(LineEdit input, int height = 34)
    {
        StyleBoxFlat Make(Color border) => new()
        {
            BgColor = Colors.White,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = border,
            ContentMarginLeft = 10,
            ContentMarginRight = 8,
            AntiAliasing = true,
        };

        input.CustomMinimumSize = new Vector2(input.CustomMinimumSize.X, height);
        input.AddThemeStyleboxOverride("normal", Make(UiKit.Line));
        input.AddThemeStyleboxOverride("focus", Make(new Color(0.38f, 0.66f, 1.0f)));
        input.AddThemeColorOverride("font_color", UiKit.Ink);
        input.AddThemeColorOverride("font_placeholder_color", UiKit.Muted with { A = 0.8f });
        input.AddThemeFontSizeOverride("font_size", 14);
    }

    private static void StyleSlider(HSlider slider)
    {
        var track = UiKit.RoundedBox(new Color(0.88f, 0.90f, 0.96f), 3);
        track.ContentMarginTop = 3;
        track.ContentMarginBottom = 3;
        var fill = UiKit.RoundedBox(UiKit.Sky, 3);
        fill.ContentMarginTop = 3;
        fill.ContentMarginBottom = 3;
        slider.AddThemeStyleboxOverride("slider", track);
        slider.AddThemeStyleboxOverride("grabber_area", fill);
        slider.AddThemeStyleboxOverride("grabber_area_highlight", fill);

        var knob = CreateKnobTexture(UiKit.Sky);
        slider.AddThemeIconOverride("grabber", knob);
        slider.AddThemeIconOverride("grabber_highlight", knob);
    }

    private static ImageTexture CreateKnobTexture(Color color)
    {
        const int size = 18;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = new Vector2(x, y).DistanceTo(center);
                var outer = Mathf.Clamp(size / 2f - d, 0, 1);       // anti-aliased white ring
                var inner = Mathf.Clamp(size / 2f - 3 - d, 0, 1);   // coloured core
                var pixel = Colors.White.Lerp(color, inner);
                image.SetPixel(x, y, pixel with { A = outer });
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private void UpdateMeta()
    {
        var count = _editableSlots.Count;
        var lineText = count == 1 ? "1 line" : $"{count} lines";
        var characters = _editableSlots.Select(s => s.CharacterName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count();
        if (_metaLabel is not null) _metaLabel.Text = $"{lineText}  ·  {characters} characters  ·  {_totalDuration:F1}s";
        if (_lineCountLabel is not null) _lineCountLabel.Text = lineText;
    }

    // ───────────────────────── Loading ─────────────────────────

    private void LoadSceneData()
    {
        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        if (doc is null)
        {
            if (_statusLabel is not null) _statusLabel.Text = "No scene loaded.";
            return;
        }

        if (_titleInput is not null) _titleInput.Text = doc.Title;

        _editableSlots.Clear();
        foreach (var slot in doc.VoiceSlots)
        {
            var timeline = doc.Timeline.FirstOrDefault(t => t.VoiceSlotId == slot.VoiceSlotId);
            var charDef = doc.Characters.FirstOrDefault(c => c.CharacterId == slot.CharacterId);

            _editableSlots.Add(new EditableVoiceSlot
            {
                SlotId = slot.VoiceSlotId,
                CharacterId = slot.CharacterId,
                CharacterName = charDef?.DisplayName ?? slot.CharacterId,
                Prompt = slot.Prompt,
                StartSeconds = (timeline?.StartMilliseconds ?? 0) / 1000.0,
                EndSeconds = (timeline?.EndMilliseconds ?? 4000) / 1000.0
            });
        }

        LoadVideoAndAudioWaveform();
        RebuildSlotsUi();
        SyncTimelineData();
        SeekTo(0.0);

        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.Empty;
        }
    }

    private void LoadVideoAndAudioWaveform()
    {
        if (_videoPlayer is null) return;

        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        if (doc is null) return;

        var videoAsset = doc.SourceMedia.FirstOrDefault(m => m.Role == SourceMediaRole.SceneVideo);
        var relPath = videoAsset?.RelativePath;

        _videoPlayer.Stream = VideoPlayback.MediaTranscoder.LoadVideoStream(folderPath, relPath);

        // Exhaustive audio waveform search
        string? wavPath = null;
        var candidates = new List<string>();

        if (!string.IsNullOrEmpty(folderPath))
        {
            candidates.Add(System.IO.Path.Combine(folderPath, "media", "vocals.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "media", "audio.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "vocals.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "audio.wav"));
            candidates.Add(System.IO.Path.Combine(folderPath, "media", "background.wav"));
        }

        if (doc is not null)
        {
            var id = doc.SceneId;
            var idUnderscore = id.Replace("-", "_");
            var idHyphen = id.Replace("_", "-");

            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{id}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idUnderscore}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idHyphen}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{id}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idUnderscore}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://Content/OfficialScenes/{idHyphen}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{id}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{idUnderscore}/media/vocals.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{id}/media/audio.wav"));
            candidates.Add(ProjectSettings.GlobalizePath($"res://scenes/{idUnderscore}/media/audio.wav"));
        }

        foreach (var c in candidates)
        {
            if (System.IO.File.Exists(c))
            {
                wavPath = c;
                break;
            }
        }

        if (wavPath is null && !string.IsNullOrEmpty(folderPath))
        {
            wavPath = VideoPlayback.MediaTranscoder.EnsureAudioExtracted(folderPath);
        }

        _currentWavPath = wavPath;
        if (wavPath is not null)
        {
            _maxSourceDuration = AudioWaveformLoader.GetAudioDurationSeconds(wavPath);
            GD.Print($"[SceneEditor] Audio source duration detected: {_maxSourceDuration:F2}s from '{wavPath}'");
        }

        var docDurationSec = (doc?.DurationMilliseconds ?? 22000) / 1000.0;
        if (_maxSourceDuration > 0)
        {
            _totalDuration = docDurationSec > 0 ? Math.Min(docDurationSec, _maxSourceDuration) : _maxSourceDuration;
            if (_durationInput is not null)
            {
                _durationInput.MaxValue = Math.Ceiling(_maxSourceDuration);
                _durationInput.MinValue = 1.0;
                _durationInput.Value = _totalDuration;
            }
        }
        else
        {
            _totalDuration = Math.Max(1.0, docDurationSec > 0 ? docDurationSec : 22.0);
            if (_durationInput is not null)
            {
                _durationInput.MaxValue = 600.0;
                _durationInput.MinValue = 1.0;
                _durationInput.Value = _totalDuration;
            }
        }

        if (_timeSlider is not null)
        {
            _timeSlider.MaxValue = _totalDuration;
            _timeSlider.Value = 0.0;
        }

        if (_currentWavPath is not null)
        {
            _currentWaveform = AudioWaveformLoader.ExtractWaveformSegment(_currentWavPath, 0.0, _totalDuration, 350);
            _timelineEditor?.SetWaveform(_currentWaveform);
            GD.Print($"[SceneEditor] Audio waveform loaded for duration [0 - {_totalDuration:F1}s]");
        }
        else
        {
            GD.PrintErr($"[SceneEditor] No audio file found for scene '{doc?.SceneId}'");
        }
    }

    private void OnDurationChanged(double newDuration)
    {
        _totalDuration = Math.Max(1.0, newDuration);
        if (_maxSourceDuration > 0 && _totalDuration > _maxSourceDuration)
        {
            _totalDuration = _maxSourceDuration;
        }

        if (_timeSlider is not null)
        {
            _timeSlider.MaxValue = _totalDuration;
            if (_timeSlider.Value > _totalDuration) _timeSlider.Value = _totalDuration;
        }

        // Re-extract waveform for new clamped time window [0.0 to _totalDuration]
        if (_currentWavPath is not null)
        {
            _currentWaveform = AudioWaveformLoader.ExtractWaveformSegment(_currentWavPath, 0.0, _totalDuration, 350);
        }

        // Clamp any slots that exceed the new duration
        bool slotChanged = false;
        foreach (var s in _editableSlots)
        {
            if (s.EndSeconds > _totalDuration)
            {
                s.EndSeconds = Math.Round(_totalDuration, 1);
                slotChanged = true;
            }
            if (s.StartSeconds >= s.EndSeconds)
            {
                s.StartSeconds = Math.Round(Math.Max(0.0, s.EndSeconds - 0.5), 1);
                slotChanged = true;
            }
        }

        if (slotChanged)
        {
            RebuildSlotsUi();
        }

        SyncTimelineData();
        UpdateMeta();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.Empty;
        }
    }

    private void SyncTimelineData()
    {
        if (_timelineEditor is null) return;

        var timelineSlots = _editableSlots.Select(s => new TimelineWaveformEditor.TimelineSlotData
        {
            SlotId = s.SlotId,
            CharacterName = s.CharacterName,
            Prompt = s.Prompt,
            StartSeconds = s.StartSeconds,
            EndSeconds = s.EndSeconds,
        }).ToList();

        _timelineEditor.SetData(_totalDuration, timelineSlots, _currentWaveform);
    }

    // ───────────────────────── Timeline & selection ─────────────────────────

    private void OnTimelineSeekRequested(double targetSeconds)
    {
        SeekTo(targetSeconds);
    }

    private void OnTimelineSlotSelected(int slotIndex)
    {
        _selectedSlotIndex = slotIndex;
        UpdateSlotCardsHighlight();
    }

    private void SelectSlotIndex(int index)
    {
        _selectedSlotIndex = (index >= 0 && index < _editableSlots.Count) ? index : -1;
        _timelineEditor?.SelectSlot(_selectedSlotIndex);
        UpdateSlotCardsHighlight();
    }

    private void OnTimelineSlotChanged(int slotIndex)
    {
        if (_timelineEditor is not null && slotIndex >= 0 && slotIndex < _editableSlots.Count)
        {
            var editorSlot = _timelineEditor.GetSlot(slotIndex);
            if (editorSlot is not null)
            {
                _editableSlots[slotIndex].StartSeconds = editorSlot.StartSeconds;
                _editableSlots[slotIndex].EndSeconds = editorSlot.EndSeconds;
            }
        }
        _selectedSlotIndex = slotIndex;
        RebuildSlotsUi();
    }

    // ───────────────────────── Playback ─────────────────────────

    private void OnPlayPausePressed()
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        if (_isPlaying)
        {
            PauseVideo();
        }
        else
        {
            PlayVideo();
        }
    }

    private void PlayVideo()
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        _isPreviewingSlot = false;
        _isPlaying = true;
        _videoPlayer.Paused = false;
        if (!_videoPlayer.IsPlaying())
        {
            _videoPlayer.Play();
        }

        if (_playPauseButton is not null) _playPauseButton.Text = "Pause";
        UiIcons.Swap(_playPauseButton, "pause");
    }

    private void PauseVideo()
    {
        if (_videoPlayer is null) return;

        _isPlaying = false;
        _videoPlayer.Paused = true;
        if (_playPauseButton is not null) _playPauseButton.Text = "Play";
        UiIcons.Swap(_playPauseButton, "play");
    }

    private void OnStopPressed()
    {
        SeekTo(0.0);
        PauseVideo();
    }

    private void SeekRelative(double deltaSec)
    {
        var current = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        SeekTo(current + deltaSec);
    }

    private void SeekTo(double targetSec)
    {
        var clamped = Math.Clamp(targetSec, 0.0, _totalDuration);
        if (_videoPlayer is not null && _videoPlayer.Stream is not null)
        {
            _videoPlayer.StreamPosition = clamped;
        }

        if (_timeSlider is not null && !_isSliderDragging)
        {
            _timeSlider.Value = clamped;
        }

        if (_videoTimeLabel is not null)
        {
            _videoTimeLabel.Text = $"{clamped:F1}s / {_totalDuration:F1}s";
        }

        _timelineEditor?.SetPlayhead(clamped);
    }

    public override void _Process(double delta)
    {
        if (_videoPlayer is not null && _videoContainer is not null)
        {
            var tex = _videoPlayer.GetVideoTexture();
            if (tex is not null && tex.GetHeight() > 0)
            {
                float r = (float)tex.GetWidth() / tex.GetHeight();
                if (Math.Abs(_videoContainer.Ratio - r) > 0.01f)
                {
                    _videoContainer.Ratio = r;
                }
            }
        }

        if (_videoPlayer is not null && _videoPlayer.Stream is not null && _videoPlayer.IsPlaying() && !_videoPlayer.Paused)
        {
            var pos = _videoPlayer.GetStreamPosition();
            _isPlaying = true;

            if (_timeSlider is not null && !_isSliderDragging)
            {
                _timeSlider.Value = pos;
            }

            if (_videoTimeLabel is not null)
            {
                _videoTimeLabel.Text = $"{pos:F1}s / {_totalDuration:F1}s";
            }

            _timelineEditor?.SetPlayhead(pos);

            if (_isPreviewingSlot && pos >= _previewEndSec)
            {
                PauseVideo();
                _isPreviewingSlot = false;
            }

            if (pos >= _totalDuration)
            {
                PauseVideo();
                SeekTo(0.0);
            }
        }
    }

    private void PreviewSlot(EditableVoiceSlot slot)
    {
        if (_videoPlayer is null || _videoPlayer.Stream is null) return;

        _isPreviewingSlot = true;
        _previewEndSec = slot.EndSeconds;
        var leadInStart = Math.Max(0.0, slot.StartSeconds - 3.0);
        SeekTo(leadInStart);
        PlayVideo();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.Empty;
        }
    }

    // ───────────────────────── Line editing ─────────────────────────

    private async void OnAutoDetectPressed()
    {
        if (_isDetecting) return;
        _isDetecting = true;
        PauseVideo();

        var packageDir = Coordinator?.SelectedScenePackage?.PackageDirectory;
        var wavPath = _currentWavPath;
        var overlay = BusyOverlay.Open(this, "Detecting lines", "Listening to the dialogue and writing subtitles.");

        // Audio extraction and Whisper both run external processes; keep them off the main thread.
        var (resolvedWav, detected) = await System.Threading.Tasks.Task.Run(() =>
        {
            if ((string.IsNullOrEmpty(wavPath) || !System.IO.File.Exists(wavPath)) && !string.IsNullOrEmpty(packageDir))
            {
                wavPath = VideoPlayback.MediaTranscoder.EnsureAudioExtracted(packageDir);
            }

            if (string.IsNullOrEmpty(wavPath) || !System.IO.File.Exists(wavPath))
            {
                return (wavPath, (IReadOnlyList<DubbedUp.Core.Ai.DetectedSpeechSegment>?)null);
            }

            // Prioritize isolated vocals.wav stem if available for clean speech recognition
            if (!string.IsNullOrEmpty(packageDir))
            {
                var vocalsWav = System.IO.Path.Combine(packageDir, "media", "vocals.wav");
                if (System.IO.File.Exists(vocalsWav))
                {
                    wavPath = vocalsWav;
                }
            }

            return (wavPath, LocalAiSceneExtractor.DetectSpeechFromWavFile(wavPath, maxSlots: 30));
        });

        overlay.Close();
        _isDetecting = false;
        if (!IsInsideTree()) return;

        _currentWavPath = resolvedWav;
        if (detected is null)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "No audio found.";
            }
            return;
        }

        if (detected.Count == 0)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "No speech detected.";
            }
            return;
        }

        _editableSlots.Clear();
        for (int i = 0; i < detected.Count; i++)
        {
            var seg = detected[i];
            _editableSlots.Add(new EditableVoiceSlot
            {
                SlotId = $"slot-{i + 1}",
                CharacterId = seg.CharacterId,
                CharacterName = seg.SpeakerDisplayName,
                Prompt = seg.Prompt,
                StartSeconds = Math.Round(seg.StartMilliseconds / 1000.0, 2),
                EndSeconds = Math.Round(seg.EndMilliseconds / 1000.0, 2)
            });
        }

        RebuildSlotsUi();
        SyncTimelineData();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"{detected.Count} lines detected";
        }
    }

    private void OnCutSelectedBoxPressed()
    {
        var selectedIdx = _timelineEditor?.GetSelectedSlotIndex() ?? -1;
        if (selectedIdx < 0 || selectedIdx >= _editableSlots.Count)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Select a box to split.";
            }
            return;
        }

        var currentPos = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        SplitSlotAtIndex(selectedIdx, currentPos);
    }

    private void SplitSlotAtIndex(int slotIndex, double splitSeconds)
    {
        if (slotIndex < 0 || slotIndex >= _editableSlots.Count) return;

        var slot = _editableSlots[slotIndex];
        var splitSec = Math.Round(splitSeconds, 2);

        if (splitSec <= slot.StartSeconds + 0.3 || splitSec >= slot.EndSeconds - 0.3)
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Move the playhead inside the box to split it.";
            }
            return;
        }

        var oldEnd = slot.EndSeconds;
        slot.EndSeconds = splitSec;

        var nextIdx = _editableSlots.Count + 1;
        var newSlot = new EditableVoiceSlot
        {
            SlotId = $"slot-{nextIdx}",
            CharacterId = slot.CharacterId,
            CharacterName = slot.CharacterName,
            Prompt = slot.Prompt,
            StartSeconds = splitSec,
            EndSeconds = oldEnd
        };

        _editableSlots.Insert(slotIndex + 1, newSlot);
        _selectedSlotIndex = slotIndex + 1;
        RebuildSlotsUi();
        SyncTimelineData();
        _timelineEditor?.SelectSlot(slotIndex + 1);

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Split at {splitSec:F1}s";
        }
    }

    private void OnAddBoxPressed()
    {
        var currentPos = _videoPlayer is not null && _videoPlayer.IsPlaying() ? _videoPlayer.GetStreamPosition() : (_timeSlider?.Value ?? 0.0);
        var start = Math.Round(currentPos, 1);
        var end = Math.Round(Math.Min(_totalDuration, start + 3.0), 1);
        if (end <= start) end = Math.Min(_totalDuration, start + 1.0);

        var nextIdx = _editableSlots.Count + 1;
        var speakerNum = ((_editableSlots.Count) % 2) + 1;
        var newSlot = new EditableVoiceSlot
        {
            SlotId = $"slot-{nextIdx}",
            CharacterId = $"char-{speakerNum}",
            CharacterName = $"Character {speakerNum}",
            Prompt = $"Line {nextIdx} dialogue prompt",
            StartSeconds = start,
            EndSeconds = end
        };

        _editableSlots.Add(newSlot);
        _selectedSlotIndex = _editableSlots.Count - 1;
        RebuildSlotsUi();
        SyncTimelineData();
        _timelineEditor?.SelectSlot(_editableSlots.Count - 1);

        if (_statusLabel is not null)
        {
            _statusLabel.Text = string.Empty;
        }
    }

    private void OnDeleteSelectedBoxPressed()
    {
        var idx = _timelineEditor?.GetSelectedSlotIndex() ?? -1;
        if (idx >= 0 && idx < _editableSlots.Count)
        {
            DeleteSlotAtIndex(idx);
        }
        else if (_editableSlots.Count > 0)
        {
            DeleteSlotAtIndex(_editableSlots.Count - 1);
        }
    }

    private void DeleteSlotAtIndex(int index)
    {
        if (index >= 0 && index < _editableSlots.Count)
        {
            _editableSlots.RemoveAt(index);
            _selectedSlotIndex = Math.Min(index, _editableSlots.Count - 1);
            RebuildSlotsUi();
            SyncTimelineData();
            _timelineEditor?.SelectSlot(_selectedSlotIndex);
            if (_statusLabel is not null)
            {
                _statusLabel.Text = string.Empty;
            }
        }
    }

    private void RebuildSlotsUi()
    {
        UpdateMeta();
        if (_slotsContainer is null) return;

        foreach (var child in _slotsContainer.GetChildren())
        {
            _slotsContainer.RemoveChild(child);
            child.QueueFree();
        }

        if (_editableSlots.Count == 0)
        {
            var empty = UiKit.Body("No lines yet. Add one or auto-detect them from the audio.", 14);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            empty.CustomMinimumSize = new Vector2(0, 80);
            empty.VerticalAlignment = VerticalAlignment.Center;
            _slotsContainer.AddChild(empty);
            return;
        }

        for (int i = 0; i < _editableSlots.Count; i++)
        {
            _slotsContainer.AddChild(CreateLineCard(i, _editableSlots[i]));
        }

        UpdateSlotCardsHighlight();
    }

    private PanelContainer CreateLineCard(int index, EditableVoiceSlot slot)
    {
        var color = TimelineWaveformEditor.GetPresetColor(index);
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
        card.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) SelectSlotIndex(index);
        };

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        card.AddChild(stack);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        stack.AddChild(row);

        var badge = new PanelContainer { CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        var badgeBox = UiKit.RoundedBox(color, 15);
        badgeBox.CornerDetail = 16;
        badge.AddThemeStyleboxOverride("panel", badgeBox);
        var number = UiKit.Heading($"{index + 1}", 13, Colors.White);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.VerticalAlignment = VerticalAlignment.Center;
        badge.AddChild(number);
        row.AddChild(badge);

        var nameInput = new LineEdit { Text = slot.CharacterName, PlaceholderText = "Character", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StyleFieldInput(nameInput);
        nameInput.FocusEntered += () => SelectSlotIndex(index);
        nameInput.TextChanged += text =>
        {
            slot.CharacterName = text;
            slot.CharacterId = ToKebabCaseId(text);
            SyncTimelineData();
            UpdateMeta();
        };
        row.AddChild(nameInput);

        SpinBox? endInput = null;
        var startInput = CreateTimeInput(slot.StartSeconds, 0.0);
        startInput.FocusEntered += () => SelectSlotIndex(index);
        row.AddChild(startInput);

        var dash = UiKit.Body("–", 14);
        dash.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(dash);

        endInput = CreateTimeInput(slot.EndSeconds, 0.1);
        endInput.FocusEntered += () => SelectSlotIndex(index);
        row.AddChild(endInput);

        startInput.ValueChanged += val =>
        {
            slot.StartSeconds = Math.Round(val, 1);
            if (slot.EndSeconds <= slot.StartSeconds)
            {
                slot.EndSeconds = Math.Round(slot.StartSeconds + 0.5, 1);
                if (endInput is not null) endInput.Value = slot.EndSeconds;
            }
            SyncTimelineData();
        };

        endInput.ValueChanged += val =>
        {
            slot.EndSeconds = Math.Round(val, 1);
            if (slot.EndSeconds <= slot.StartSeconds)
            {
                slot.StartSeconds = Math.Round(Math.Max(0, slot.EndSeconds - 0.5), 1);
                startInput.Value = slot.StartSeconds;
            }
            SyncTimelineData();
        };

        row.AddChild(UiKit.CreateIconButton("play", UiKit.Sky with { A = 0.14f }, UiKit.Sky, () =>
        {
            SelectSlotIndex(index);
            PreviewSlot(slot);
        }, 34));
        row.AddChild(UiKit.CreateIconButton("trash-2", UiKit.Danger with { A = 0.12f }, UiKit.Danger, () => DeleteSlotAtIndex(index), 34));

        var promptInput = new LineEdit { Text = slot.Prompt, PlaceholderText = "What the character says", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StyleFieldInput(promptInput);
        promptInput.FocusEntered += () => SelectSlotIndex(index);
        promptInput.TextChanged += text =>
        {
            slot.Prompt = text;
            SyncTimelineData();
        };
        stack.AddChild(promptInput);

        return card;
    }

    private static SpinBox CreateTimeInput(double value, double min)
    {
        var input = new SpinBox
        {
            MinValue = min,
            MaxValue = 600.0,
            Step = 0.1,
            Value = value,
            Suffix = "s",
            CustomMinimumSize = new Vector2(74, 34),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        StyleFieldInput(input.GetLineEdit());
        input.GetLineEdit().Alignment = HorizontalAlignment.Center;
        return input;
    }

    private void UpdateSlotCardsHighlight()
    {
        if (_slotsContainer is null) return;

        var children = _slotsContainer.GetChildren();
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not PanelContainer card) continue;

            var selected = i == _selectedSlotIndex;
            var color = TimelineWaveformEditor.GetPresetColor(i);
            var box = UiKit.RoundedBox(selected ? color.Lerp(Colors.White, 0.9f) : UiKit.Soft, 18, padding: 10);
            box.BorderWidthLeft = 2;
            box.BorderWidthTop = 2;
            box.BorderWidthRight = 2;
            box.BorderWidthBottom = 2;
            box.BorderColor = selected ? color : new Color(1, 1, 1, 0);
            if (selected)
            {
                box.ShadowColor = color with { A = 0.25f };
                box.ShadowSize = 10;
                box.ShadowOffset = new Vector2(0, 3);
            }
            card.AddThemeStyleboxOverride("panel", box);

            if (selected && _linesScroll is not null)
            {
                var target = card;
                Callable.From(() => _linesScroll?.EnsureControlVisible(target)).CallDeferred();
            }
        }
    }

    // ───────────────────────── Saving ─────────────────────────

    private static string ToKebabCaseId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "slot";

        var sanitized = input
            .Replace("ç", "c").Replace("Ç", "c")
            .Replace("ğ", "g").Replace("Ğ", "g")
            .Replace("ı", "i").Replace("I", "i").Replace("İ", "i")
            .Replace("ö", "o").Replace("Ö", "o")
            .Replace("ş", "s").Replace("Ş", "s")
            .Replace("ü", "u").Replace("Ü", "u");

        sanitized = Regex.Replace(sanitized, @"[^a-zA-Z0-9\s-]", "");
        sanitized = Regex.Replace(sanitized, @"\s+", "-").Trim('-').ToLowerInvariant();
        sanitized = Regex.Replace(sanitized, @"-+", "-");

        return string.IsNullOrEmpty(sanitized) ? "slot" : sanitized;
    }

    private bool SaveSceneData()
    {
        var doc = Coordinator?.SelectedScenePackage?.Document ?? Coordinator?.CurrentScene;
        if (doc is null)
        {
            if (_statusLabel is not null) _statusLabel.Text = "No scene loaded.";
            return false;
        }

        if (_editableSlots.Count == 0)
        {
            if (_statusLabel is not null) _statusLabel.Text = "Add at least one line.";
            return false;
        }

        var newTitle = _titleInput?.Text?.Trim() ?? doc.Title;
        if (string.IsNullOrWhiteSpace(newTitle)) newTitle = doc.Title;

        var inputDurationMs = (int)((_durationInput?.Value ?? 22.0) * 1000);
        var maxSlotEndMs = (int)(_editableSlots.Max(s => s.EndSeconds) * 1000);
        var newDurationMs = Math.Max(inputDurationMs, maxSlotEndMs + 500);

        var newCharacters = new List<CharacterDefinition>();
        var newVoiceSlots = new List<VoiceSlotDefinition>();
        var newTimeline = new List<TimelineEntry>();

        var sortedSlots = _editableSlots.OrderBy(s => s.StartSeconds).ToList();
        var charMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sortedSlots.Count; i++)
        {
            var slot = sortedSlots[i];
            var charKey = string.IsNullOrWhiteSpace(slot.CharacterName) ? $"Character {i + 1}" : slot.CharacterName.Trim();
            if (!charMap.TryGetValue(charKey, out var charId))
            {
                charId = ToKebabCaseId(charKey);
                if (newCharacters.Any(c => c.CharacterId == charId)) charId = $"{charId}-{i + 1}";

                charMap[charKey] = charId;
                newCharacters.Add(new CharacterDefinition
                {
                    CharacterId = charId,
                    DisplayName = charKey
                });
            }

            var slotId = $"slot-{i + 1}-{charId}";
            var prompt = string.IsNullOrWhiteSpace(slot.Prompt) ? "Dialogue line" : slot.Prompt.Trim();

            newVoiceSlots.Add(new VoiceSlotDefinition
            {
                VoiceSlotId = slotId,
                CharacterId = charId,
                Prompt = prompt
            });

            var startMs = (int)(slot.StartSeconds * 1000);
            var endMs = (int)(slot.EndSeconds * 1000);
            if (endMs <= startMs) endMs = startMs + 1000;

            newTimeline.Add(new TimelineEntry
            {
                TimelineEntryId = $"entry-{i + 1}-{slotId}",
                VoiceSlotId = slotId,
                StartMilliseconds = startMs,
                EndMilliseconds = endMs
            });
        }

        var updatedDoc = new OfficialSceneDocument
        {
            SchemaVersion = doc.SchemaVersion,
            SceneId = doc.SceneId,
            Title = newTitle,
            DurationMilliseconds = newDurationMs,
            SourceMedia = doc.SourceMedia,
            Characters = newCharacters,
            VoiceSlots = newVoiceSlots,
            Timeline = newTimeline
        };

        try
        {
            ProjectValidator.Validate(updatedDoc);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SceneEditor] Validation failed: {ex.Message}");
            if (_statusLabel is not null) _statusLabel.Text = ex.Message;
            return false;
        }

        var folderPath = Coordinator?.SelectedScenePackage?.PackageDirectory;
        if (!string.IsNullOrEmpty(folderPath))
        {
            var sceneJsonPath = System.IO.Path.Combine(folderPath, "scene.json");
            var json = ProjectJsonSerializer.SerializeScene(updatedDoc);
            System.IO.File.WriteAllText(sceneJsonPath, json);
            GD.Print($"[SceneEditor] Saved updated scene to: {sceneJsonPath}");

            if (Coordinator is not null)
            {
                try
                {
                    Coordinator.SelectedScenePackage = ScenePackageLoader.LoadPackageFromDirectory(folderPath);
                    Coordinator.CurrentScene = Coordinator.SelectedScenePackage.Document;
                }
                catch
                {
                    Coordinator.CurrentScene = updatedDoc;
                }
            }
        }
        else if (Coordinator is not null)
        {
            Coordinator.CurrentScene = updatedDoc;
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Text = "Saved";
        }

        return true;
    }

    private void OnSavePressed()
    {
        SaveSceneData();
    }

    private void OnSaveAndProceedPressed()
    {
        if (SaveSceneData())
        {
            PauseVideo();
            Navigator?.NavigateTo(AppScreen.ScenePicker);
        }
    }

    private void OnBackPressed()
    {
        PauseVideo();
        Navigator?.NavigateTo(AppScreen.Setup);
    }
}
