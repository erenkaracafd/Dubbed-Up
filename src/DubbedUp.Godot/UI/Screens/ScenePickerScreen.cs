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
    private readonly Dictionary<ScenePackage, Color> _sceneAccentColors = [];

    public override void _Ready()
    {
        _topBar = GetNodeOrNull<PanelContainer>("TopBar");
        _backButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/BackButton");
        _createSceneButton = GetNodeOrNull<Button>("TopBar/CenterContainer/CreateSceneButton")
            ?? GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/CreateSceneButton");

        _showcasePanel = GetNodeOrNull<PanelContainer>("MainLayoutMargin/SplitHBox/ShowcasePanel");
        _thumbnailFrame = GetNodeOrNull<PanelContainer>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ThumbnailFrame");
        _thumbnailTexture = GetNodeOrNull<TextureRect>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ThumbnailFrame/ThumbnailTexture");
        _placeholderIcon = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ThumbnailFrame/PlaceholderIcon");
        _showcaseTitle = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ShowcaseTitle");
        _durationBadge = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/BadgesHBox/DurationBadge");
        _slotsBadge = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/BadgesHBox/SlotsBadge");
        _categoryBadge = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/BadgesHBox/CategoryBadge");
        _charactersLabel = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/CharactersLabel");
        _linesPreviewLabel = GetNodeOrNull<Label>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/LinesPreviewLabel");
        _playSelectedButton = GetNodeOrNull<Button>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ShowcaseActionsHBox/PlaySelectedButton");
        _editTimelineButton = GetNodeOrNull<Button>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ShowcaseActionsHBox/EditTimelineButton");
        _deleteSceneButton = GetNodeOrNull<Button>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard/InnerMargin/ShowcaseVBox/ShowcaseActionsHBox/DeleteSceneButton");

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
            StyleButton(_playSelectedButton, new Color(1.0f, 0.540f, 0.680f), new Color(1.0f, 0.660f, 0.780f), new Color(0.900f, 0.420f, 0.560f), Colors.White, 26);
        }
        if (_editTimelineButton is not null)
        {
            StyleButton(_editTimelineButton, new Color(0.600f, 0.480f, 0.950f), new Color(0.700f, 0.600f, 1.000f), new Color(0.500f, 0.360f, 0.880f), Colors.White, 26);
        }
        if (_deleteSceneButton is not null)
        {
            StyleButton(_deleteSceneButton, new Color(0.960f, 0.450f, 0.550f), new Color(0.980f, 0.550f, 0.650f), new Color(0.850f, 0.350f, 0.450f), Colors.White, 26);
        }

        // Create Scene button (Centered tactile 3D pill)
        if (_createSceneButton is not null)
        {
            StyleCreateSceneButton(_createSceneButton);
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
                BgColor = new Color(0.955f, 0.975f, 1.0f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.780f, 0.850f, 0.950f),
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

    private static void StyleCreateSceneButton(Button btn)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.460f, 0.380f, 0.920f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.760f, 0.700f, 1.000f),
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21,
            ShadowSize = 8,
            ShadowColor = new Color(0.35f, 0.25f, 0.75f, 0.35f),
            ShadowOffset = new Vector2(0, 3),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };

        var hover = new StyleBoxFlat
        {
            BgColor = new Color(0.550f, 0.460f, 0.980f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = Colors.White,
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21,
            ShadowSize = 14,
            ShadowColor = new Color(0.45f, 0.35f, 0.95f, 0.50f),
            ShadowOffset = new Vector2(0, 4),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };

        var pressed = new StyleBoxFlat
        {
            BgColor = new Color(0.360f, 0.280f, 0.800f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.650f, 0.580f, 0.950f),
            CornerRadiusTopLeft = 21,
            CornerRadiusTopRight = 21,
            CornerRadiusBottomLeft = 21,
            CornerRadiusBottomRight = 21,
            ShadowSize = 2,
            ShadowColor = new Color(0.25f, 0.15f, 0.60f, 0.30f),
            ShadowOffset = new Vector2(0, 1),
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 5,
            ContentMarginBottom = 3
        };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.95f, 0.95f, 1.0f));
        btn.AddThemeColorOverride("font_shadow_color", new Color(0.15f, 0.10f, 0.40f, 0.45f));
        btn.AddThemeConstantOverride("shadow_offset_y", 1);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        btn.PivotOffset = new Vector2(125, 21);

        btn.MouseEntered += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.12)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        };
        btn.MouseExited += () =>
        {
            var tween = btn.CreateTween();
            tween.TweenProperty(btn, "scale", Vector2.One, 0.10)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
        };
    }

    private static void StyleOutline(Button btn, int radius)
    {
        var normal = new StyleBoxFlat { BgColor = new Color(0.955f, 0.975f, 1.0f), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1, BorderColor = new Color(0.780f, 0.850f, 0.950f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        var hover = new StyleBoxFlat { BgColor = new Color(0.910f, 0.945f, 0.990f), BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2, BorderColor = new Color(0.38f, 0.71f, 1.0f), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", new Color(0.25f, 0.28f, 0.42f));
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

    private static readonly Color[] CuratedPalette =
    [
        new Color(1.0f, 0.540f, 0.680f), // Soft Blossom Pink (#FF8AAD)
        new Color(0.280f, 0.650f, 0.950f), // Soft Sky Blue (#47A6F2)
        new Color(0.620f, 0.480f, 0.960f), // Soft Lilac Violet (#9E7AF5)
        new Color(0.180f, 0.750f, 0.620f), // Soft Mint Emerald (#2EC09E)
        new Color(0.980f, 0.640f, 0.250f), // Soft Peach Amber (#FAA440)
        new Color(0.960f, 0.450f, 0.650f), // Soft Rose Quartz (#F573A6)
        new Color(0.420f, 0.500f, 0.950f), // Soft Periwinkle (#6B80F2)
        new Color(0.180f, 0.720f, 0.900f), // Soft Aqua Teal (#2EB8E6)
    ];

    private Color GetSceneAccentColor(ScenePackage package)
    {
        if (_sceneAccentColors.TryGetValue(package, out var cached))
        {
            return cached;
        }

        Color resultColor;
        var thumb = VideoPlayback.VideoThumbnailHelper.GetOrExtractThumbnail(package.PackageDirectory ?? string.Empty);
        if (thumb is not null)
        {
            resultColor = ExtractDominantColor(thumb, package.Title);
        }
        else
        {
            var hash = Math.Abs(package.Title.GetHashCode());
            resultColor = CuratedPalette[hash % CuratedPalette.Length];
        }

        _sceneAccentColors[package] = resultColor;
        return resultColor;
    }

    private static Color ExtractDominantColor(Texture2D texture, string fallbackSeed)
    {
        try
        {
            var image = texture.GetImage();
            if (image is not null && !image.IsEmpty())
            {
                int width = image.GetWidth();
                int height = image.GetHeight();

                float bestScore = -1f;
                Color bestColor = Colors.Transparent;

                int stepX = Math.Max(1, width / 14);
                int stepY = Math.Max(1, height / 10);

                for (int y = stepY / 2; y < height; y += stepY)
                {
                    for (int x = stepX / 2; x < width; x += stepX)
                    {
                        var p = image.GetPixel(x, y);
                        if (p.A < 0.5f) continue;

                        float lum = 0.299f * p.R + 0.587f * p.G + 0.114f * p.B;
                        if (lum < 0.12f || lum > 0.90f) continue; // Skip near-black and glare

                        float max = Math.Max(p.R, Math.Max(p.G, p.B));
                        float min = Math.Min(p.R, Math.Min(p.G, p.B));
                        float sat = max > 0 ? (max - min) / max : 0;

                        float score = sat * (1.0f - Math.Abs(lum - 0.5f));
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestColor = p;
                        }
                    }
                }

                if (bestScore > 0.06f)
                {
                    float h, s, v;
                    bestColor.ToHsv(out h, out s, out v);
                    s = Math.Clamp(s * 1.25f, 0.55f, 0.95f);
                    v = Math.Clamp(v, 0.68f, 0.92f);
                    return Color.FromHsv(h, s, v);
                }
            }
        }
        catch
        {
            // Ignore
        }

        var fHash = Math.Abs(fallbackSeed.GetHashCode());
        return CuratedPalette[fHash % CuratedPalette.Length];
    }

    private Control CreateOsuBeatmapCard(ScenePackage package, int index, int total, out PanelContainer card, out Controls.MarqueeLabel marquee)
    {
        var isSelected = package == _selectedPackage;
        var accent = GetSceneAccentColor(package);
        int naturalZIndex = Math.Max(0, 50 - index);

        // 1. Outer holder handles 50px step in ScenesListContainer with descending natural ZIndex
        var holder = new Control
        {
            CustomMinimumSize = new Vector2(0, 50),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
            ZIndex = isSelected ? 80 : naturalZIndex
        };

        // 2. Outer card (Outer Rim): darker/richer cover accent tint + cover-colored drop shadow
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

        var outerMargin = new MarginContainer
        {
            Name = "OuterMargin",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        outerMargin.AddThemeConstantOverride("margin_left", 4);
        outerMargin.AddThemeConstantOverride("margin_right", 4);
        outerMargin.AddThemeConstantOverride("margin_top", 4);
        outerMargin.AddThemeConstantOverride("margin_bottom", 4);
        card.AddChild(outerMargin);

        // 3. Inner card (Inner Box): lighter luminous white/soft-tint surface
        var innerCard = new PanelContainer
        {
            Name = "InnerCard",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        outerMargin.AddChild(innerCard);

        ApplyCardStyles(card, innerCard, accent, isSelected, isHovered: false);

        var innerMargin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        innerMargin.AddThemeConstantOverride("margin_left", 10);
        innerMargin.AddThemeConstantOverride("margin_right", 12);
        innerMargin.AddThemeConstantOverride("margin_top", 6);
        innerMargin.AddThemeConstantOverride("margin_bottom", 6);
        innerCard.AddChild(innerMargin);

        var hbox = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        hbox.AddThemeConstantOverride("separation", 12);
        innerMargin.AddChild(hbox);

        // Mini 16:9 Thumbnail Framed and Clipped
        var thumbFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(86, 48),
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var thumbStyle = new StyleBoxFlat
        {
            BgColor = accent.Lerp(Colors.White, 0.85f),
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            BorderWidthLeft = 2,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 2,
            BorderColor = accent.Lerp(Colors.White, 0.45f)
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
            iconLabel.AddThemeFontSizeOverride("font_size", 20);
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
            FontSize = 15,
            FontColor = new Color(0.094f, 0.086f, 0.180f) // Deep Midnight Slate
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
        charsLabel.AddThemeFontSizeOverride("font_size", 11);
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
        durLabel.AddThemeFontSizeOverride("font_size", 11);
        rightVBox.AddChild(durLabel);

        var isCustom = package.PackageDirectory is not null && !package.PackageDirectory.Contains("OfficialScenes");
        var badgePanel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var badgeStyle = new StyleBoxFlat
        {
            BgColor = accent,
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        badgePanel.AddThemeStyleboxOverride("panel", badgeStyle);
        var badgeLabel = new Label
        {
            Text = isCustom ? "Custom" : $"{package.Document.VoiceSlots.Count} Lines",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        badgeLabel.AddThemeColorOverride("font_color", Colors.White); // Crisp white text!
        badgeLabel.AddThemeFontSizeOverride("font_size", 10);
        badgePanel.AddChild(badgeLabel);
        rightVBox.AddChild(badgePanel);

        // Interactive Accordion Hover: pops completely out of the overlap, casts glow, scrolls marquee!
        var capturedCard = card;
        var capturedInner = innerCard;
        var capturedHolder = holder;
        var capturedMarquee = marquee;

        capturedCard.MouseEntered += () =>
        {
            UiSoundManager.Instance.PlayHover();
            capturedHolder.ZIndex = 150;
            capturedCard.ZIndex = 150;
            var tween = capturedCard.CreateTween();
            tween?.SetParallel(true);
            tween?.TweenProperty(capturedCard, "offset_left", -12.0f, 0.12f)
                  .SetTrans(Tween.TransitionType.Back)
                  .SetEase(Tween.EaseType.Out);
            tween?.TweenProperty(capturedCard, "scale", new Vector2(1.03f, 1.03f), 0.12f);

            ApplyCardStyles(capturedCard, capturedInner, accent, isSelected, isHovered: true);
            capturedMarquee.OnCardHovered();
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

            ApplyCardStyles(capturedCard, capturedInner, accent, isCurSelected, isHovered: false);
            capturedMarquee.OnCardUnhovered();

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

    private static void ApplyCardStyles(PanelContainer card, PanelContainer innerCard, Color accent, bool isSelected, bool isHovered)
    {
        var skyBg = new Color(0.925f, 0.955f, 0.988f);
        var iceWhite = new Color(0.965f, 0.980f, 1.000f);

        // 1. Dış Katman (Outer Box): Darker on the outside, rich cover accent border & shadow
        var outerStyle = new StyleBoxFlat
        {
            BgColor = isSelected
                ? accent.Lerp(skyBg, 0.72f)
                : (isHovered ? accent.Lerp(skyBg, 0.76f) : accent.Lerp(skyBg, 0.84f)),
            BorderWidthLeft = isSelected ? 6 : (isHovered ? 5 : 4),
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 3,
            BorderColor = isHovered ? accent.Lightened(0.14f) : accent,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ShadowColor = isHovered
                ? new Color(accent.R, accent.G, accent.B, 0.40f)
                : new Color(accent.R * 0.35f, accent.G * 0.35f, accent.B * 0.35f, isSelected ? 0.28f : 0.16f),
            ShadowSize = isHovered ? 16 : (isSelected ? 12 : 8),
            ShadowOffset = new Vector2(0, 5)
        };
        card.AddThemeStyleboxOverride("panel", outerStyle);

        // 2. İç Katman (Inner Box): Progressively lighter towards the inside (illuminated soft ice sky surface)
        var innerStyle = new StyleBoxFlat
        {
            BgColor = isHovered ? new Color(0.985f, 0.992f, 1.000f) : (isSelected ? new Color(0.975f, 0.988f, 1.0f) : accent.Lerp(iceWhite, 0.95f)),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = isHovered ? accent.Lerp(new Color(0.780f, 0.850f, 0.950f), 0.50f) : accent.Lerp(new Color(0.780f, 0.850f, 0.950f), 0.75f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
        innerCard.AddThemeStyleboxOverride("panel", innerStyle);
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
            var accent = GetSceneAccentColor(pkg);
            int naturalZIndex = Math.Max(0, 50 - index);

            var holder = _cardHolders.TryGetValue(pkg, out var h) ? h : null;
            if (holder is not null)
            {
                holder.ZIndex = isSelected ? 80 : naturalZIndex;
            }
            card.ZIndex = isSelected ? 80 : naturalZIndex;

            var innerCard = card.GetNodeOrNull<PanelContainer>("OuterMargin/InnerCard");
            if (innerCard is not null)
            {
                ApplyCardStyles(card, innerCard, accent, isSelected, isHovered: false);
            }

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
        var accent = GetSceneAccentColor(package);
        var skyBg = new Color(0.925f, 0.955f, 0.988f);
        var iceWhite = new Color(0.965f, 0.980f, 1.000f);

        // Showcase outer panel: cover accent border & soft tinted background
        if (_showcasePanel is not null)
        {
            var showcaseStyle = new StyleBoxFlat
            {
                BgColor = accent.Lerp(skyBg, 0.85f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                BorderColor = accent,
                CornerRadiusTopLeft = 24,
                CornerRadiusTopRight = 24,
                CornerRadiusBottomLeft = 24,
                CornerRadiusBottomRight = 24,
                ShadowColor = new Color(accent.R * 0.35f, accent.G * 0.35f, accent.B * 0.35f, 0.18f),
                ShadowSize = 18,
                ShadowOffset = new Vector2(0, 6)
            };
            _showcasePanel.AddThemeStyleboxOverride("panel", showcaseStyle);
        }

        // Showcase inner card: pure radiant ice canvas
        var showcaseInnerCard = GetNodeOrNull<PanelContainer>("MainLayoutMargin/SplitHBox/ShowcasePanel/ShowcaseMargin/ShowcaseInnerCard");
        if (showcaseInnerCard is not null)
        {
            var innerStyle = new StyleBoxFlat
            {
                BgColor = iceWhite,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = accent.Lerp(new Color(0.780f, 0.850f, 0.950f), 0.75f),
                CornerRadiusTopLeft = 18,
                CornerRadiusTopRight = 18,
                CornerRadiusBottomLeft = 18,
                CornerRadiusBottomRight = 18
            };
            showcaseInnerCard.AddThemeStyleboxOverride("panel", innerStyle);
        }

        // Thumbnail Frame
        if (_thumbnailFrame is not null)
        {
            var frameBox = new StyleBoxFlat
            {
                BgColor = accent.Lerp(Colors.White, 0.90f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = accent.Lerp(Colors.White, 0.50f),
                CornerRadiusTopLeft = 14,
                CornerRadiusTopRight = 14,
                CornerRadiusBottomLeft = 14,
                CornerRadiusBottomRight = 14
            };
            _thumbnailFrame.AddThemeStyleboxOverride("panel", frameBox);
        }

        if (_showcaseTitle is not null)
        {
            _showcaseTitle.Text = package.Title;
        }

        var durSec = package.Document.DurationMilliseconds / 1000.0;
        if (_durationBadge is not null)
        {
            _durationBadge.Text = $"⏱️ {durSec:F1}s";
            var durStyle = new StyleBoxFlat
            {
                BgColor = accent.Lerp(Colors.White, 0.88f),
                CornerRadiusTopLeft = 9,
                CornerRadiusTopRight = 9,
                CornerRadiusBottomLeft = 9,
                CornerRadiusBottomRight = 9,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            };
            _durationBadge.AddThemeStyleboxOverride("panel", durStyle);
            _durationBadge.AddThemeColorOverride("font_color", new Color(0.12f, 0.14f, 0.25f));
        }

        if (_slotsBadge is not null)
        {
            _slotsBadge.Text = $"🎙️ {package.Document.VoiceSlots.Count} Lines";
            var slotsStyle = new StyleBoxFlat
            {
                BgColor = accent,
                CornerRadiusTopLeft = 9,
                CornerRadiusTopRight = 9,
                CornerRadiusBottomLeft = 9,
                CornerRadiusBottomRight = 9,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            };
            _slotsBadge.AddThemeStyleboxOverride("panel", slotsStyle);
            _slotsBadge.AddThemeColorOverride("font_color", Colors.White);
        }

        var isCustom = package.PackageDirectory is not null && !package.PackageDirectory.Contains("OfficialScenes");
        if (_categoryBadge is not null)
        {
            _categoryBadge.Text = isCustom ? "Custom Workshop" : "Official Scene";
            var catStyle = new StyleBoxFlat
            {
                BgColor = isCustom ? new Color(0.561f, 0.396f, 0.973f) : accent,
                CornerRadiusTopLeft = 9,
                CornerRadiusTopRight = 9,
                CornerRadiusBottomLeft = 9,
                CornerRadiusBottomRight = 9,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 3,
                ContentMarginBottom = 3
            };
            _categoryBadge.AddThemeStyleboxOverride("panel", catStyle);
            _categoryBadge.AddThemeColorOverride("font_color", Colors.White);
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

        if (_playSelectedButton is not null)
        {
            var normal = new StyleBoxFlat { BgColor = accent, CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23, ShadowSize = 8, ShadowColor = new Color(accent.R, accent.G, accent.B, 0.35f) };
            var hover = new StyleBoxFlat { BgColor = accent.Lightened(0.12f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23, ShadowSize = 12, ShadowColor = new Color(accent.R, accent.G, accent.B, 0.45f) };
            var pressed = new StyleBoxFlat { BgColor = accent.Darkened(0.12f), CornerRadiusTopLeft = 23, CornerRadiusTopRight = 23, CornerRadiusBottomLeft = 23, CornerRadiusBottomRight = 23 };
            _playSelectedButton.AddThemeStyleboxOverride("normal", normal);
            _playSelectedButton.AddThemeStyleboxOverride("hover", hover);
            _playSelectedButton.AddThemeStyleboxOverride("pressed", pressed);
            _playSelectedButton.AddThemeStyleboxOverride("focus", hover);
            _playSelectedButton.AddThemeColorOverride("font_color", Colors.White);
            _playSelectedButton.AddThemeColorOverride("font_hover_color", Colors.White);
            _playSelectedButton.AddThemeColorOverride("font_focus_color", Colors.White);
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
