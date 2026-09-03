using System;
using System.Collections.Generic;
using System.Linq;
using DubbedUp.Core.Scenes;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Workshop;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class ScenePickerScreen : BaseScreen
{
    private PanelContainer? _topBar;
    private Button? _backButton;
    private Button? _createSceneButton;
    private Button? _openFolderButton;
    private Button? _refreshButton;
    private Button? _workshopButton;

    // Left Showcase Panel
    private PanelContainer? _showcasePanel;
    private PanelContainer? _thumbnailFrame;
    private TextureRect? _thumbnailTexture;
    private Label? _placeholderIcon;
    private Label? _showcaseTitle;
    private Label? _durationBadge;
    private Label? _slotsBadge;
    private Label? _categoryBadge;
    private Label? _charactersLabel;
    private Label? _linesPreviewLabel;
    private Button? _playSelectedButton;
    private Button? _editTimelineButton;
    private Button? _deleteSceneButton;

    // Right Carousel
    private LineEdit? _searchInput;
    private Label? _statusLabel;
    private VBoxContainer? _scenesListContainer;

    // Delete Modal
    private Control? _deleteConfirmModal;
    private PanelContainer? _dialogPanel;
    private Label? _confirmMessageLabel;
    private Button? _confirmDeleteButton;
    private Button? _cancelDeleteButton;
    private ScenePackage? _pendingDeletePackage;

    private readonly SteamWorkshopService _workshopService = new();
    private readonly List<ScenePackage> _availableScenes = [];
    private ScenePackage? _selectedPackage;
    private readonly Dictionary<ScenePackage, Control> _cardHolders = [];
    private readonly Dictionary<ScenePackage, PanelContainer> _cardNodes = [];
    private readonly Dictionary<ScenePackage, Controls.MarqueeLabel> _marqueeLabels = [];

    public override void _Ready()
    {
        _topBar = GetNodeOrNull<PanelContainer>("TopBar");
        _backButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/BackButton");
        _createSceneButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/CreateSceneButton");

        _showcasePanel = GetNodeOrNull<PanelContainer>("MainLayoutMargin/SplitHBox/ShowcasePanel");
        _thumbnailFrame = GetNodeOrNull<PanelContainer>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ThumbnailFrame");
        _thumbnailTexture = GetNodeOrNull<TextureRect>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ThumbnailFrame/ThumbnailTexture");
        _placeholderIcon = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ThumbnailFrame/PlaceholderIcon");
        _showcaseTitle = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ShowcaseTitle");
        _durationBadge = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/BadgesHBox/DurationBadge");
        _slotsBadge = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/BadgesHBox/SlotsBadge");
        _categoryBadge = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/BadgesHBox/CategoryBadge");
        _charactersLabel = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/CharactersLabel");
        _linesPreviewLabel = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/LinesPreviewLabel");
        _playSelectedButton = GetNodeOrNull<Button>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ShowcaseActionsHBox/PlaySelectedButton");
        _editTimelineButton = GetNodeOrNull<Button>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ShowcaseActionsHBox/EditTimelineButton");
        _deleteSceneButton = GetNodeOrNull<Button>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseVBox/ShowcaseActionsHBox/DeleteSceneButton");

        _searchInput = GetNodeOrNull<LineEdit>("MainLayoutMargin/SplitHBox/CarouselVBox/SearchHBox/SearchInput");
        _statusLabel = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/CarouselVBox/StatusLabel");
        _scenesListContainer = GetNodeOrNull<VBoxContainer>("MainLayoutMargin/SplitHBox/CarouselVBox/ScrollContainer/ScenesListMargin/ScenesListContainer");

        _deleteConfirmModal = GetNodeOrNull<Control>("DeleteConfirmModal");
        _dialogPanel = GetNodeOrNull<PanelContainer>("DeleteConfirmModal/CenterContainer/ModalCard");
        _confirmMessageLabel = GetNodeOrNull<Label>("DeleteConfirmModal/CenterContainer/ModalCard/ModalMargin/ModalVBox/ModalDescription");
        _confirmDeleteButton = GetNodeOrNull<Button>("DeleteConfirmModal/CenterContainer/ModalCard/ModalMargin/ModalVBox/ModalButtonsHBox/ConfirmDeleteButton");
        _cancelDeleteButton = GetNodeOrNull<Button>("DeleteConfirmModal/CenterContainer/ModalCard/ModalMargin/ModalVBox/ModalButtonsHBox/CancelDeleteButton");

        ApplyStyling();

        if (_backButton is not null) SetupButton(_backButton, OnBackPressed);
        if (_createSceneButton is not null) SetupButton(_createSceneButton, OnCreateScenePressed);
        if (_openFolderButton is not null) SetupButton(_openFolderButton, OnOpenFolderPressed);
        if (_refreshButton is not null) SetupButton(_refreshButton, OnRefreshPressed);
        if (_workshopButton is not null) SetupButton(_workshopButton, OnWorkshopPressed);
        if (_playSelectedButton is not null) SetupButton(_playSelectedButton, OnPlaySelectedPressed);
        if (_editTimelineButton is not null) SetupButton(_editTimelineButton, OnEditTimelinePressed);
        if (_deleteSceneButton is not null) SetupButton(_deleteSceneButton, OnDeleteScenePressed);

        if (_confirmDeleteButton is not null) SetupButton(_confirmDeleteButton, OnConfirmDeletePressed);
        if (_cancelDeleteButton is not null) SetupButton(_cancelDeleteButton, OnCancelDeletePressed);

        if (_searchInput is not null)
        {
            _searchInput.TextChanged += OnSearchTextChanged;
        }

        LoadAvailableScenes();
        PopulateSceneList();
    }

    private void SetupButton(Button btn, Action action)
    {
        btn.Pressed += action;
        UiSoundManager.Attach(btn);
    }

    private void ApplyStyling()
    {
        // Top bar styling
        var barStyle = new StyleBoxFlat
        {
            BgColor = new Color(1.0f, 1.0f, 1.0f, 0.90f),
            BorderWidthBottom = 1,
            BorderColor = new Color(0.886f, 0.902f, 0.941f, 0.8f),
            ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
            ShadowSize = 6
        };
        _topBar?.AddThemeStyleboxOverride("panel", barStyle);

        // Showcase panel styling
        if (_showcasePanel is not null)
        {
            var showcaseStyle = new StyleBoxFlat
            {
                BgColor = Colors.White,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f, 1.0f),
                CornerRadiusTopLeft = 24,
                CornerRadiusTopRight = 24,
                CornerRadiusBottomLeft = 24,
                CornerRadiusBottomRight = 24,
                ShadowColor = new Color(0.12f, 0.11f, 0.30f, 0.08f),
                ShadowSize = 16,
                ShadowOffset = new Vector2(0, 6)
            };
            _showcasePanel.AddThemeStyleboxOverride("panel", showcaseStyle);
        }

        // Thumbnail Frame styling (Clipped rounded frame)
        if (_thumbnailFrame is not null)
        {
            var frameBox = new StyleBoxFlat
            {
                BgColor = new Color(0.94f, 0.96f, 0.99f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f),
                CornerRadiusTopLeft = 16,
                CornerRadiusTopRight = 16,
                CornerRadiusBottomLeft = 16,
                CornerRadiusBottomRight = 16
            };
            _thumbnailFrame.AddThemeStyleboxOverride("panel", frameBox);
        }

        // Action Buttons Styling
        if (_playSelectedButton is not null)
        {
            StyleButton(_playSelectedButton, new Color(1.0f, 0.243f, 0.514f), new Color(1.0f, 0.35f, 0.6f), new Color(0.88f, 0.15f, 0.42f), Colors.White, 26);
        }
        if (_editTimelineButton is not null)
        {
            StyleButton(_editTimelineButton, new Color(0.561f, 0.396f, 0.973f), new Color(0.65f, 0.51f, 1.0f), new Color(0.47f, 0.29f, 0.92f), Colors.White, 26);
        }
        if (_deleteSceneButton is not null)
        {
            StyleButton(_deleteSceneButton, new Color(1.0f, 0.42f, 0.42f), new Color(1.0f, 0.52f, 0.52f), new Color(0.88f, 0.32f, 0.32f), Colors.White, 26);
        }

        // Create Scene button
        if (_createSceneButton is not null)
        {
            StyleButton(_createSceneButton, new Color(0.561f, 0.396f, 0.973f), new Color(0.65f, 0.51f, 1.0f), new Color(0.47f, 0.29f, 0.92f), Colors.White, 18);
        }

        // Outline buttons
        if (_backButton is not null) StyleOutline(_backButton, 18);
        if (_openFolderButton is not null) StyleOutline(_openFolderButton, 18);
        if (_refreshButton is not null) StyleOutline(_refreshButton, 18);
        if (_workshopButton is not null) StyleOutline(_workshopButton, 18);

        // Search Input styling
        if (_searchInput is not null)
        {
            var searchBox = new StyleBoxFlat
            {
                BgColor = Colors.White,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.886f, 0.902f, 0.941f, 1.0f),
                CornerRadiusTopLeft = 16,
                CornerRadiusTopRight = 16,
                CornerRadiusBottomLeft = 16,
                CornerRadiusBottomRight = 16,
                ContentMarginLeft = 16,
                ContentMarginRight = 16
            };
            _searchInput.AddThemeStyleboxOverride("normal", searchBox);
            _searchInput.AddThemeStyleboxOverride("focus", searchBox);
            _searchInput.AddThemeColorOverride("font_color", new Color(0.118f, 0.106f, 0.294f));
        }

        // Dialog Panel
        if (_dialogPanel is not null)
        {
            var dialogStyle = new StyleBoxFlat
            {
                BgColor = Colors.White,
                CornerRadiusTopLeft = 20,
                CornerRadiusTopRight = 20,
                CornerRadiusBottomLeft = 20,
                CornerRadiusBottomRight = 20,
                ShadowColor = new Color(0, 0, 0, 0.25f),
                ShadowSize = 24
            };
            _dialogPanel.AddThemeStyleboxOverride("panel", dialogStyle);
        }
    }

    private static void StyleButton(Button btn, Color normal, Color hover, Color pressed, Color textColor, int radius)
    {
        var sNormal = new StyleBoxFlat { BgColor = normal, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 6, ShadowColor = new Color(normal.R, normal.G, normal.B, 0.3f) };
        var sHover = new StyleBoxFlat { BgColor = hover, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 10, ShadowColor = new Color(normal.R, normal.G, normal.B, 0.4f) };
        var sPressed = new StyleBoxFlat { BgColor = pressed, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ShadowSize = 1 };

        btn.AddThemeStyleboxOverride("normal", sNormal);
        btn.AddThemeStyleboxOverride("hover", sHover);
        btn.AddThemeStyleboxOverride("pressed", sPressed);
        btn.AddThemeStyleboxOverride("focus", sHover);
        btn.AddThemeColorOverride("font_color", textColor);
        btn.AddThemeColorOverride("font_hover_color", textColor);
    }

    private static void StyleOutline(Button btn, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = Colors.White, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.886f, 0.902f, 0.941f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        var hover = new StyleBoxFlat { BgColor = new Color(0.95f, 0.97f, 1.0f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.294f, 0.322f, 0.439f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.118f, 0.106f, 0.294f));
    }

    private void LoadAvailableScenes()
    {
        _availableScenes.Clear();
        _workshopService.Refresh();

        var scenes = _workshopService.GetAvailableScenes();
        _availableScenes.AddRange(scenes);

        if (_availableScenes.Count == 0)
        {
            var defaultDoc = LocalSessionCoordinator.CreateDefaultScene();
            var fallbackPackage = new ScenePackage(defaultDoc, "builtin://museum-mixup", null, null);
            _availableScenes.Add(fallbackPackage);
        }

        // Default selection
        _selectedPackage = Coordinator?.SelectedScenePackage ?? _availableScenes[0];
        UpdateShowcase(_selectedPackage);
    }

    private void PopulateSceneList(string filter = "")
    {
        if (_scenesListContainer is null) return;
        _cardNodes.Clear();
        foreach (var child in _scenesListContainer.GetChildren())
        {
            child.QueueFree();
        }
        _cardHolders.Clear();
        _cardNodes.Clear();
        _marqueeLabels.Clear();

        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _availableScenes
            : _availableScenes.Where(s => s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                          s.Document.Characters.Any(c => c.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToList();

        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Showing {filtered.Count} of {_availableScenes.Count} playable scenes";
        }

        int index = 0;
        int total = filtered.Count;
        foreach (var package in filtered)
        {
            var holder = CreateOsuBeatmapCard(package, index, total, out var card, out var marquee);
            _cardHolders[package] = holder;
            _cardNodes[package] = card;
            _marqueeLabels[package] = marquee;
            _scenesListContainer.AddChild(holder);
            index++;
        }

        HighlightSelectedCard();
    }

    private Control CreateOsuBeatmapCard(ScenePackage package, int index, int total, out PanelContainer card, out Controls.MarqueeLabel marquee)
    {
        var isSelected = package == _selectedPackage;
        int naturalZIndex = Math.Max(0, 50 - index);

        // Outer holder handles the 50px step in ScenesListContainer with descending natural ZIndex
        var holder = new Control
        {
            CustomMinimumSize = new Vector2(0, 50),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
            ZIndex = isSelected ? 80 : naturalZIndex
        };

        // Inner card is 72px high, overlapping the card below it by 22px
        card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 72),
            AnchorsPreset = (int)Control.LayoutPreset.TopWide,
            AnchorRight = 1.0f,
            OffsetLeft = isSelected ? 10 : 32,
            OffsetRight = 0,
            OffsetTop = 0,
            OffsetBottom = 72,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = isSelected ? 80 : naturalZIndex,
            PivotOffset = new Vector2(0, 36)
        };
        holder.AddChild(card);

        ApplyCardStyle(card, isSelected);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        card.AddChild(margin);

        var hbox = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(hbox);

        // Mini 16:9 Thumbnail Framed and Clipped
        var thumbFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(90, 50),
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var thumbStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.93f, 0.95f, 0.98f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.886f, 0.902f, 0.941f)
        };
        thumbFrame.AddThemeStyleboxOverride("panel", thumbStyle);
        hbox.AddChild(thumbFrame);

        var thumb = VideoPlayback.VideoThumbnailHelper.GetOrExtractThumbnail(package.PackageDirectory ?? string.Empty);
        if (thumb is not null)
        {
            var texRect = new TextureRect
            {
                Texture = thumb,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            thumbFrame.AddChild(texRect);
        }
        else
        {
            var iconLabel = new Label
            {
                Text = "🎬",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            iconLabel.AddThemeFontSizeOverride("font_size", 22);
            thumbFrame.AddChild(iconLabel);
        }

        // Info VBox
        var infoVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        infoVBox.AddThemeConstantOverride("separation", 2);
        hbox.AddChild(infoVBox);

        // Marquee title label: flows on hover if text is long!
        marquee = new Controls.MarqueeLabel
        {
            Text = package.Title,
            FontSize = 16,
            FontColor = new Color(0.118f, 0.106f, 0.294f)
        };
        infoVBox.AddChild(marquee);

        var charsText = string.Join(", ", package.Document.Characters.Select(c => c.DisplayName));
        var charsLabel = new Label
        {
            Text = string.IsNullOrEmpty(charsText) ? "No characters defined" : $"🎭 {charsText}",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            AutowrapMode = TextServer.AutowrapMode.Off
        };
        charsLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.38f, 0.50f));
        charsLabel.AddThemeFontSizeOverride("font_size", 12);
        infoVBox.AddChild(charsLabel);

        // Right side tags
        var rightVBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        rightVBox.AddThemeConstantOverride("separation", 3);
        hbox.AddChild(rightVBox);

        var durSec = package.Document.DurationMilliseconds / 1000.0;
        var durLabel = new Label
        {
            Text = $"⏱️ {durSec:F1}s",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        durLabel.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.40f));
        durLabel.AddThemeFontSizeOverride("font_size", 12);
        rightVBox.AddChild(durLabel);

        var isCustom = package.PackageDirectory is not null && !package.PackageDirectory.Contains("OfficialScenes");
        var badgeLabel = new Label
        {
            Text = isCustom ? "Custom" : $"{package.Document.VoiceSlots.Count} Lines",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        badgeLabel.AddThemeColorOverride("font_color", isCustom ? new Color(0.561f, 0.396f, 0.973f) : new Color(1.0f, 0.243f, 0.514f));
        badgeLabel.AddThemeFontSizeOverride("font_size", 11);
        rightVBox.AddChild(badgeLabel);

        // Interactive Accordion Hover: pops completely out of the overlap, casts glow, scrolls marquee!
        var capturedCard = card;
        var capturedHolder = holder;
        var capturedMarquee = marquee;

        capturedCard.MouseEntered += () =>
        {
            UiSoundManager.Instance.PlayHover();
            capturedHolder.ZIndex = 150; // Pop holder above all other card rows
            capturedCard.ZIndex = 150;
            var tween = capturedCard.CreateTween();
            tween?.SetParallel(true);
            tween?.TweenProperty(capturedCard, "offset_left", -12.0f, 0.12f)
                  .SetTrans(Tween.TransitionType.Back)
                  .SetEase(Tween.EaseType.Out);
            tween?.TweenProperty(capturedCard, "scale", new Vector2(1.03f, 1.03f), 0.12f);

            ApplyCardHoverStyle(capturedCard);
            capturedMarquee.OnCardHovered(); // Flow long text!
            UpdateShowcase(package);
        };

        capturedCard.MouseExited += () =>
        {
            var isCurSelected = package == _selectedPackage;
            capturedHolder.ZIndex = isCurSelected ? 80 : naturalZIndex;
            capturedCard.ZIndex = isCurSelected ? 80 : naturalZIndex;
            var tween = capturedCard.CreateTween();
            tween?.SetParallel(true);
            tween?.TweenProperty(capturedCard, "offset_left", isCurSelected ? 10.0f : 32.0f, 0.10f)
                  .SetTrans(Tween.TransitionType.Cubic)
                  .SetEase(Tween.EaseType.Out);
            tween?.TweenProperty(capturedCard, "scale", new Vector2(1.0f, 1.0f), 0.10f);

            ApplyCardStyle(capturedCard, isCurSelected);
            capturedMarquee.OnCardUnhovered(); // Reset marquee scroll!

            if (!isCurSelected && _selectedPackage is not null)
            {
                UpdateShowcase(_selectedPackage);
            }
        };

        capturedCard.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                UiSoundManager.Instance.PlayClick();
                SelectPackage(package);
            }
        };

        return holder;
    }

    private static void ApplyCardStyle(PanelContainer card, bool isSelected)
    {
        var style = new StyleBoxFlat
        {
            BgColor = isSelected ? new Color(1.0f, 0.965f, 0.985f) : Colors.White,
            BorderWidthLeft = isSelected ? 4 : 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            BorderColor = isSelected ? new Color(1.0f, 0.243f, 0.514f) : new Color(0.86f, 0.88f, 0.93f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ShadowColor = isSelected ? new Color(1.0f, 0.243f, 0.514f, 0.25f) : new Color(0.12f, 0.10f, 0.28f, 0.14f),
            ShadowSize = isSelected ? 12 : 8,
            ShadowOffset = new Vector2(0, 5)
        };
        card.AddThemeStyleboxOverride("panel", style);
    }

    private static void ApplyCardHoverStyle(PanelContainer card)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(1.0f, 0.98f, 0.99f),
            BorderWidthLeft = 4,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 3,
            BorderColor = new Color(1.0f, 0.243f, 0.514f),
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ShadowColor = new Color(1.0f, 0.243f, 0.514f, 0.35f),
            ShadowSize = 16,
            ShadowOffset = new Vector2(-2, 6)
        };
        card.AddThemeStyleboxOverride("panel", style);
    }

    private void SelectPackage(ScenePackage package)
    {
        _selectedPackage = package;
        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = package;
        }

        UpdateShowcase(package);
        HighlightSelectedCard();
    }

    private void HighlightSelectedCard()
    {
        int index = 0;
        foreach (var (pkg, card) in _cardNodes)
        {
            var isSelected = pkg == _selectedPackage;
            int naturalZIndex = Math.Max(0, 50 - index);

            var holder = _cardHolders.TryGetValue(pkg, out var h) ? h : null;
            if (holder is not null)
            {
                holder.ZIndex = isSelected ? 80 : naturalZIndex;
            }
            card.ZIndex = isSelected ? 80 : naturalZIndex;
            ApplyCardStyle(card, isSelected);

            var tween = card.CreateTween();
            tween?.TweenProperty(card, "offset_left", isSelected ? 10.0f : 32.0f, 0.10f)
                  .SetTrans(Tween.TransitionType.Cubic)
                  .SetEase(Tween.EaseType.Out);
            index++;
        }
    }

    private void UpdateShowcase(ScenePackage? package)
    {
        if (package is null) return;

        if (_showcaseTitle is not null)
        {
            _showcaseTitle.Text = package.Title;
        }

        var durSec = package.Document.DurationMilliseconds / 1000.0;
        if (_durationBadge is not null)
        {
            _durationBadge.Text = $"⏱️ {durSec:F1}s";
        }

        if (_slotsBadge is not null)
        {
            _slotsBadge.Text = $"🎙️ {package.Document.VoiceSlots.Count} Dialogue Lines";
        }

        var isCustom = package.PackageDirectory is not null && !package.PackageDirectory.Contains("OfficialScenes");
        if (_categoryBadge is not null)
        {
            _categoryBadge.Text = isCustom ? "• Custom Workshop Scene •" : "• Official Scene •";
            _categoryBadge.AddThemeColorOverride("font_color", isCustom ? new Color(0.561f, 0.396f, 0.973f) : new Color(1.0f, 0.243f, 0.514f));
        }

        if (_charactersLabel is not null)
        {
            var chars = package.Document.Characters.Select(c => c.DisplayName).ToList();
            _charactersLabel.Text = chars.Count > 0
                ? $"Characters: {string.Join(", ", chars)}"
                : "Characters: None defined";
        }

        if (_linesPreviewLabel is not null)
        {
            var sampleLines = package.Document.VoiceSlots
                .Take(2)
                .Select(s => $"• {s.Prompt}")
                .ToList();
            if (package.Document.VoiceSlots.Count > 2)
            {
                sampleLines.Add($"• (+{package.Document.VoiceSlots.Count - 2} more dialogue lines)");
            }
            _linesPreviewLabel.Text = sampleLines.Count > 0
                ? $"Dialogue Lines:\n{string.Join("\n", sampleLines)}"
                : "Dialogue Lines: No voice lines in this scene.";
        }

        if (_deleteSceneButton is not null)
        {
            _deleteSceneButton.Visible = isCustom;
        }

        // Thumbnail
        if (_thumbnailTexture is not null)
        {
            var thumb = VideoPlayback.VideoThumbnailHelper.GetOrExtractThumbnail(package.PackageDirectory ?? string.Empty);
            _thumbnailTexture.Texture = thumb;
            _thumbnailTexture.Visible = thumb is not null;
            if (_placeholderIcon is not null)
            {
                _placeholderIcon.Visible = thumb is null;
            }
        }
    }

    private void OnSearchTextChanged(string newText)
    {
        PopulateSceneList(newText.Trim());
    }

    private void OnPlaySelectedPressed()
    {
        if (_selectedPackage is null) return;

        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = _selectedPackage;
        }

        Navigator?.NavigateTo(AppScreen.Setup);
    }

    private void OnEditTimelinePressed()
    {
        if (_selectedPackage is null) return;

        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = _selectedPackage;
        }

        Navigator?.NavigateTo(AppScreen.SceneEditor);
    }

    private void OnDeleteScenePressed()
    {
        if (_selectedPackage is null) return;
        _pendingDeletePackage = _selectedPackage;

        if (_confirmMessageLabel is not null)
        {
            _confirmMessageLabel.Text = $"Are you sure you want to permanently delete \"{_selectedPackage.Title}\"?\nThis cannot be undone.";
        }

        if (_deleteConfirmModal is not null)
        {
            _deleteConfirmModal.Visible = true;
        }
    }

    private void OnConfirmDeletePressed()
    {
        if (_pendingDeletePackage is not null)
        {
            var dir = _pendingDeletePackage.PackageDirectory;
            if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
            {
                try
                {
                    System.IO.Directory.Delete(dir, true);
                    GD.Print($"[ScenePicker] Deleted scene package at: {dir}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[ScenePicker] Error deleting scene package: {ex.Message}");
                }
            }
            _pendingDeletePackage = null;
        }

        if (_deleteConfirmModal is not null)
        {
            _deleteConfirmModal.Visible = false;
        }

        LoadAvailableScenes();
        PopulateSceneList(_searchInput?.Text ?? "");
    }

    private void OnCancelDeletePressed()
    {
        _pendingDeletePackage = null;
        if (_deleteConfirmModal is not null)
        {
            _deleteConfirmModal.Visible = false;
        }
    }

    private void OnCreateScenePressed()
    {
        Navigator?.NavigateTo(AppScreen.SceneCreator);
    }

    private void OnOpenFolderPressed()
    {
        _workshopService.OpenLocalScenesFolder();
    }

    private void OnRefreshPressed()
    {
        LoadAvailableScenes();
        PopulateSceneList(_searchInput?.Text ?? "");
    }

    private void OnWorkshopPressed()
    {
        _workshopService.OpenWorkshopInBrowser();
    }

    private void OnBackPressed()
    {
        Navigator?.NavigateTo(AppScreen.MainMenu);
    }
}
