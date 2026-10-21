using System;
using System.Collections.Generic;
using System.Linq;
using DubbedUp.Core.Scenes;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.Workshop;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

/// <summary>
/// Scene selection screen: a responsive grid of large thumbnail cards with a floating
/// action bar for the selected scene. The layout is built in code so cards can animate freely.
/// </summary>
public partial class ScenePickerScreen : BaseScreen
{
    private const float MinCardWidth = 340f;
    private const int MaxColumns = 4;
    private const int CardGap = 22;
    private const int CardRadius = 22;
    private const int ThumbRadius = 16;

    private sealed class SceneCard
    {
        public required ScenePackage Package { get; init; }
        public required PanelContainer Root { get; init; }
        public required Control HoverBadge { get; init; }
        public required Color Accent { get; init; }
        public bool Hovered { get; set; }
    }

    private readonly SteamWorkshopService _workshopService = new();
    private readonly List<ScenePackage> _availableScenes = [];
    private readonly List<SceneCard> _cards = [];
    private readonly Dictionary<string, Color> _accentColors = [];
    private ScenePackage? _selectedPackage;
    private ScenePackage? _pendingDeletePackage;

    private Label? _countLabel;
    private LineEdit? _searchInput;
    private ScrollContainer? _scroll;
    private GridContainer? _grid;
    private Label? _emptyLabel;

    private PanelContainer? _actionBar;
    private Panel? _barThumb;
    private Label? _barTitle;
    private Label? _barMeta;
    private Button? _dubButton;
    private Button? _deleteButton;
    private bool _actionBarShown;

    private Control? _modal;
    private PanelContainer? _modalCard;
    private Label? _modalMessage;

    public override void _Ready()
    {
        BuildLayout();
        BuildActionBar();
        BuildDeleteModal();

        LoadAvailableScenes();
        PopulateGrid(animate: true);
    }

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);

        // _Ready runs before Initialize, so restore the coordinator's selection now.
        var previous = coordinator.SelectedScenePackage;
        var match = previous is null ? null : _availableScenes.FirstOrDefault(p => p.PackageDirectory == previous.PackageDirectory);
        if (match is not null && match != _selectedPackage)
        {
            SelectPackage(match, playSound: false);
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
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        margin.AddChild(column);

        // Header
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        column.AddChild(header);

        var backButton = UiKit.CreateButton("Back", "arrow-left", outline: true, UiKit.Soft, UiKit.Ink, OnBackPressed);
        backButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(backButton);

        var titleBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titleBox.AddThemeConstantOverride("separation", 0);
        header.AddChild(titleBox);
        titleBox.AddChild(UiKit.Heading("Pick a scene", 32));
        _countLabel = UiKit.Body(string.Empty, 13);
        titleBox.AddChild(_countLabel);

        _searchInput = new LineEdit
        {
            PlaceholderText = "Search scenes or characters",
            CustomMinimumSize = new Vector2(300, 46),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            RightIcon = UiIcons.Get("search"),
            ClearButtonEnabled = true,
        };
        UiKit.StyleInput(_searchInput);
        _searchInput.TextChanged += _ => PopulateGrid(animate: true);
        header.AddChild(_searchInput);

        var importButton = UiKit.CreateButton("Import", "upload", outline: false, UiKit.Lilac, Colors.White, OnCreateScenePressed);
        importButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        importButton.CustomMinimumSize = new Vector2(0, 46);
        header.AddChild(importButton);

        // Card grid
        _scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _scroll.Resized += UpdateGridColumns;
        UiKit.StyleScrollBar(_scroll.GetVScrollBar());
        column.AddChild(_scroll);

        var gridPadding = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        gridPadding.AddThemeConstantOverride("margin_left", 10);
        gridPadding.AddThemeConstantOverride("margin_right", 10);
        gridPadding.AddThemeConstantOverride("margin_top", 10);
        gridPadding.AddThemeConstantOverride("margin_bottom", 140); // room under the floating action bar
        _scroll.AddChild(gridPadding);

        var gridStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        gridPadding.AddChild(gridStack);

        _grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", CardGap);
        _grid.AddThemeConstantOverride("v_separation", CardGap);
        gridStack.AddChild(_grid);

        _emptyLabel = UiKit.Body("No scenes match your search.", 16);
        _emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _emptyLabel.VerticalAlignment = VerticalAlignment.Center;
        _emptyLabel.CustomMinimumSize = new Vector2(0, 120);
        _emptyLabel.Visible = false;
        gridStack.AddChild(_emptyLabel);
    }

    private void BuildActionBar()
    {
        _actionBar = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -460,
            OffsetRight = 460,
            OffsetTop = 40,
            OffsetBottom = 128,
            Modulate = new Color(1, 1, 1, 0),
            ZIndex = 50, // stays above hovered cards, which are raised to z 10
        };
        var barBox = UiKit.CardBox(padding: 12);
        barBox.BgColor = new Color(1, 1, 1, 0.96f);
        barBox.ShadowColor = new Color(0.12f, 0.10f, 0.32f, 0.16f);
        barBox.ShadowSize = 26;
        _actionBar.AddThemeStyleboxOverride("panel", barBox);
        AddChild(_actionBar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        _actionBar.AddChild(row);

        _barThumb = new Panel { CustomMinimumSize = new Vector2(112, 63), ClipChildren = ClipChildrenMode.Only };
        _barThumb.AddThemeStyleboxOverride("panel", UiKit.RoundedBox(Colors.White, 14));
        row.AddChild(_barThumb);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        info.AddThemeConstantOverride("separation", 2);
        row.AddChild(info);

        _barTitle = UiKit.Heading(string.Empty, 20);
        _barTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        info.AddChild(_barTitle);

        _barMeta = UiKit.Body(string.Empty, 13);
        _barMeta.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        info.AddChild(_barMeta);

        _deleteButton = UiKit.CreateButton("Delete", "trash-2", outline: true, new Color(1.0f, 0.95f, 0.96f), UiKit.Danger, OnDeleteScenePressed);
        _deleteButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(_deleteButton);

        var editButton = UiKit.CreateButton("Edit", "pencil", outline: true, UiKit.Soft, UiKit.Ink, OnEditTimelinePressed);
        editButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(editButton);

        _dubButton = UiKit.CreateButton("DUB IT", "mic", outline: false, UiKit.Pink, Colors.White, OnPlaySelectedPressed);
        _dubButton.CustomMinimumSize = new Vector2(170, 56);
        _dubButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _dubButton.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(_dubButton);
    }

    private void BuildDeleteModal()
    {
        _modal = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop, ZIndex = 100 };
        _modal.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_modal);

        var dim = new ColorRect { Color = new Color(0.07f, 0.06f, 0.18f, 0.38f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OnCancelDeletePressed();
        };
        _modal.AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        _modal.AddChild(center);

        _modalCard = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        var modalBox = UiKit.CardBox(padding: 26);
        modalBox.BgColor = Colors.White;
        modalBox.ShadowColor = new Color(0.08f, 0.06f, 0.25f, 0.28f);
        modalBox.ShadowSize = 30;
        _modalCard.AddThemeStyleboxOverride("panel", modalBox);
        center.AddChild(_modalCard);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        _modalCard.AddChild(stack);

        stack.AddChild(UiKit.Heading("Delete scene?", 24));

        _modalMessage = UiKit.Body(string.Empty);
        _modalMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _modalMessage.CustomMinimumSize = new Vector2(364, 0);
        stack.AddChild(_modalMessage);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        stack.AddChild(buttons);

        buttons.AddChild(UiKit.CreateButton("Cancel", null, outline: true, UiKit.Soft, UiKit.Ink, OnCancelDeletePressed));
        buttons.AddChild(UiKit.CreateButton("Delete", "trash-2", outline: false, UiKit.Danger, Colors.White, OnConfirmDeletePressed));
    }

    private void UpdateGridColumns()
    {
        if (_scroll is null || _grid is null) return;
        var usable = _scroll.Size.X - 20;
        var columns = (int)Math.Floor((usable + CardGap) / (MinCardWidth + CardGap));
        _grid.Columns = Math.Clamp(columns, 1, MaxColumns);
    }

    // ───────────────────────── Data ─────────────────────────

    private void LoadAvailableScenes()
    {
        _availableScenes.Clear();
        _workshopService.Refresh();
        _availableScenes.AddRange(_workshopService.GetAvailableScenes());

        if (_availableScenes.Count == 0)
        {
            var defaultDoc = LocalSessionCoordinator.CreateDefaultScene();
            _availableScenes.Add(new ScenePackage(defaultDoc, "builtin://museum-mixup", null, null));
        }

        if (_selectedPackage is null || !_availableScenes.Contains(_selectedPackage))
        {
            _selectedPackage = _availableScenes[0];
        }
    }

    private void PopulateGrid(bool animate)
    {
        if (_grid is null) return;

        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _cards.Clear();

        var filter = _searchInput?.Text.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(filter)
            ? _availableScenes
            : _availableScenes.Where(s => s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                       || s.Document.Characters.Any(c => c.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                              .ToList();

        if (_countLabel is not null)
        {
            _countLabel.Text = filtered.Count == _availableScenes.Count
                ? $"{_availableScenes.Count} scenes"
                : $"{filtered.Count} of {_availableScenes.Count} scenes";
        }

        if (_emptyLabel is not null) _emptyLabel.Visible = filtered.Count == 0;

        for (var i = 0; i < filtered.Count; i++)
        {
            var card = CreateCard(filtered[i]);
            _cards.Add(card);
            _grid.AddChild(card.Root);
            ApplyCardStyle(card);

            if (animate) PlayEntrance(card.Root, i);
        }

        Callable.From(UpdateGridColumns).CallDeferred();
        UpdateActionBar(bump: false);
    }

    // ───────────────────────── Cards ─────────────────────────

    private SceneCard CreateCard(ScenePackage package)
    {
        var accent = GetSceneAccentColor(package);
        var root = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };

        var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 0);
        root.AddChild(stack);

        // Thumbnail with rounded mask
        var thumb = new Panel { ClipChildren = ClipChildrenMode.Only, MouseFilter = MouseFilterEnum.Ignore };
        thumb.AddThemeStyleboxOverride("panel", UiKit.RoundedBox(Colors.White, ThumbRadius));
        stack.AddChild(thumb);
        UiKit.FillSceneArt(thumb, package, accent, initialSize: 56);
        root.Resized += () =>
        {
            var width = root.Size.X - 6;
            thumb.CustomMinimumSize = new Vector2(0, Mathf.Round(width * 9f / 16f));
            root.PivotOffset = root.Size * 0.5f;
        };

        // Chips on top of the thumbnail
        var chipRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        chipRow.SetAnchorsPreset(LayoutPreset.TopWide);
        chipRow.OffsetLeft = 10;
        chipRow.OffsetRight = -10;
        chipRow.OffsetTop = 10;
        thumb.AddChild(chipRow);

        if (IsCustom(package)) chipRow.AddChild(UiKit.CreateChip("Custom", UiKit.Lilac with { A = 0.92f }));
        chipRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        chipRow.AddChild(UiKit.CreateChip(FormatDuration(package), new Color(0.07f, 0.06f, 0.18f, 0.62f)));

        // Hover badge: round mic button that fades in
        var badge = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        var badgeBox = UiKit.RoundedBox(accent, 30, padding: 16);
        badgeBox.CornerDetail = 24;
        badgeBox.ShadowColor = accent with { A = 0.45f };
        badgeBox.ShadowSize = 14;
        badge.AddThemeStyleboxOverride("panel", badgeBox);
        badge.AddChild(new TextureRect
        {
            Texture = UiIcons.Get("mic"),
            CustomMinimumSize = new Vector2(26, 26),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var badgeCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        badgeCenter.SetAnchorsPreset(LayoutPreset.FullRect);
        badgeCenter.AddChild(badge);
        thumb.AddChild(badgeCenter);

        // Title + meta
        var info = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        info.AddThemeConstantOverride("margin_left", 10);
        info.AddThemeConstantOverride("margin_right", 10);
        info.AddThemeConstantOverride("margin_top", 10);
        info.AddThemeConstantOverride("margin_bottom", 6);
        stack.AddChild(info);

        var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 1);
        info.AddChild(text);

        var title = UiKit.Heading(package.Title, 19);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        title.MouseFilter = MouseFilterEnum.Ignore;
        text.AddChild(title);

        var meta = UiKit.Body(FormatMeta(package), 13);
        meta.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        meta.MouseFilter = MouseFilterEnum.Ignore;
        text.AddChild(meta);

        var card = new SceneCard { Package = package, Root = root, HoverBadge = badge, Accent = accent };

        root.MouseEntered += () => SetHovered(card, true);
        root.MouseExited += () => SetHovered(card, false);
        root.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
            if (mb.DoubleClick && package == _selectedPackage)
            {
                OnPlaySelectedPressed();
                return;
            }
            SelectPackage(package, playSound: true);
        };

        return card;
    }

    private void ApplyCardStyle(SceneCard card)
    {
        var selected = card.Package == _selectedPackage;
        var accent = card.Accent;
        var box = UiKit.RoundedBox(selected ? accent.Lerp(Colors.White, 0.9f) : new Color(1, 1, 1, 0.94f), CardRadius, padding: 3);
        box.BorderWidthLeft = 3;
        box.BorderWidthTop = 3;
        box.BorderWidthRight = 3;
        box.BorderWidthBottom = 3;
        box.BorderColor = selected ? accent : (card.Hovered ? accent.Lerp(Colors.White, 0.55f) : new Color(1, 1, 1, 0));
        box.ShadowColor = card.Hovered
            ? accent with { A = 0.28f }
            : selected ? accent with { A = 0.18f } : new Color(0.12f, 0.10f, 0.32f, 0.10f);
        box.ShadowSize = card.Hovered ? 24 : (selected ? 16 : 12);
        box.ShadowOffset = new Vector2(0, card.Hovered ? 10 : 6);
        card.Root.AddThemeStyleboxOverride("panel", box);
    }

    private void SetHovered(SceneCard card, bool hovered)
    {
        card.Hovered = hovered;
        ApplyCardStyle(card);
        card.Root.ZIndex = hovered ? 10 : 0;
        if (hovered) UiSoundManager.Instance.PlayHover();

        card.HoverBadge.PivotOffset = card.HoverBadge.Size * 0.5f;
        var tween = card.Root.CreateTween().SetParallel(true);
        tween.TweenProperty(card.Root, "scale", hovered ? new Vector2(1.035f, 1.035f) : Vector2.One, hovered ? 0.26f : 0.2f)
             .SetTrans(hovered ? Tween.TransitionType.Back : Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card.HoverBadge, "modulate:a", hovered ? 1.0f : 0.0f, 0.18f);
        tween.TweenProperty(card.HoverBadge, "scale", hovered ? Vector2.One : new Vector2(0.8f, 0.8f), 0.24f)
             .SetTrans(Tween.TransitionType.Back)
             .SetEase(Tween.EaseType.Out);
    }

    private static void PlayEntrance(Control card, int index)
    {
        card.Modulate = new Color(1, 1, 1, 0);
        card.Scale = new Vector2(0.94f, 0.94f);
        var delay = Math.Min(index, 12) * 0.045;
        var tween = card.CreateTween().SetParallel(true);
        tween.TweenProperty(card, "modulate:a", 1.0f, 0.32f).SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "scale", Vector2.One, 0.46f).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    // ───────────────────────── Selection & action bar ─────────────────────────

    private void SelectPackage(ScenePackage package, bool playSound)
    {
        if (playSound) UiSoundManager.Instance.PlayClick();

        _selectedPackage = package;
        if (Coordinator is not null)
        {
            Coordinator.SelectedScenePackage = package;
        }

        foreach (var card in _cards) ApplyCardStyle(card);
        UpdateActionBar(bump: true);
    }

    private void UpdateActionBar(bool bump)
    {
        if (_actionBar is null) return;
        var package = _selectedPackage;
        if (package is null) return;

        var accent = GetSceneAccentColor(package);
        if (_barTitle is not null) _barTitle.Text = package.Title;
        if (_barMeta is not null) _barMeta.Text = $"{FormatDuration(package)}  ·  {FormatMeta(package)}";
        if (_deleteButton is not null) _deleteButton.Visible = IsCustom(package);
        if (_dubButton is not null) UiKit.StylePill(_dubButton, accent, Colors.White, outline: false);

        if (_barThumb is not null)
        {
            foreach (var child in _barThumb.GetChildren())
            {
                _barThumb.RemoveChild(child);
                child.QueueFree();
            }
            UiKit.FillSceneArt(_barThumb, package, accent, initialSize: 28);
        }

        if (!_actionBarShown)
        {
            _actionBarShown = true;
            var tween = _actionBar.CreateTween().SetParallel(true);
            tween.TweenProperty(_actionBar, "offset_top", -112.0f, 0.5f).SetDelay(0.15).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_actionBar, "offset_bottom", -24.0f, 0.5f).SetDelay(0.15).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_actionBar, "modulate:a", 1.0f, 0.3f).SetDelay(0.15);
        }
        else if (bump)
        {
            _actionBar.PivotOffset = _actionBar.Size * 0.5f;
            _actionBar.Scale = new Vector2(0.97f, 0.97f);
            _actionBar.CreateTween()
                      .TweenProperty(_actionBar, "scale", Vector2.One, 0.32f)
                      .SetTrans(Tween.TransitionType.Back)
                      .SetEase(Tween.EaseType.Out);
        }
    }

    // ───────────────────────── Actions ─────────────────────────

    private void OnPlaySelectedPressed()
    {
        if (_selectedPackage is null) return;
        if (Coordinator is not null) Coordinator.SelectedScenePackage = _selectedPackage;
        Navigator?.NavigateTo(AppScreen.Setup);
    }

    private void OnEditTimelinePressed()
    {
        if (_selectedPackage is null) return;
        if (Coordinator is not null) Coordinator.SelectedScenePackage = _selectedPackage;
        Navigator?.NavigateTo(AppScreen.SceneEditor);
    }

    private void OnDeleteScenePressed()
    {
        if (_selectedPackage is null || _modal is null || _modalCard is null) return;
        _pendingDeletePackage = _selectedPackage;
        if (_modalMessage is not null)
        {
            _modalMessage.Text = $"\"{_selectedPackage.Title}\" will be removed from your scenes. This can't be undone.";
        }

        _modal.Visible = true;
        _modal.Modulate = new Color(1, 1, 1, 0);
        _modalCard.PivotOffset = _modalCard.Size * 0.5f;
        _modalCard.Scale = new Vector2(0.9f, 0.9f);
        var tween = _modal.CreateTween().SetParallel(true);
        tween.TweenProperty(_modal, "modulate:a", 1.0f, 0.18f);
        tween.TweenProperty(_modalCard, "scale", Vector2.One, 0.32f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void CloseModal()
    {
        if (_modal is null) return;
        var modal = _modal;
        var tween = modal.CreateTween();
        tween.TweenProperty(modal, "modulate:a", 0.0f, 0.14f);
        tween.Finished += () => modal.Visible = false;
    }

    private void OnConfirmDeletePressed()
    {
        var package = _pendingDeletePackage;
        _pendingDeletePackage = null;
        CloseModal();
        if (package is null) return;

        var dir = package.PackageDirectory;
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

        if (_selectedPackage == package) _selectedPackage = null;
        LoadAvailableScenes();
        PopulateGrid(animate: true);
    }

    private void OnCancelDeletePressed()
    {
        _pendingDeletePackage = null;
        CloseModal();
    }

    private void OnCreateScenePressed() => Navigator?.NavigateTo(AppScreen.SceneCreator);

    private void OnBackPressed() => Navigator?.NavigateTo(AppScreen.MainMenu);

    // ───────────────────────── Helpers ─────────────────────────

    private static bool IsCustom(ScenePackage package) =>
        package.PackageDirectory is not null
        && !package.PackageDirectory.Contains("OfficialScenes")
        && !package.PackageDirectory.StartsWith("builtin://", StringComparison.Ordinal);

    private static string FormatDuration(ScenePackage package)
    {
        var seconds = (int)Math.Round(package.Document.DurationMilliseconds / 1000.0);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static string FormatMeta(ScenePackage package)
    {
        var lines = package.Document.VoiceSlots.Count;
        var characters = string.Join(", ", package.Document.Characters.Select(c => c.DisplayName));
        var lineText = lines == 1 ? "1 line" : $"{lines} lines";
        return string.IsNullOrEmpty(characters) ? lineText : $"{lineText}  ·  {characters}";
    }

    private Color GetSceneAccentColor(ScenePackage package)
    {
        var key = package.PackageDirectory ?? package.Title;
        if (_accentColors.TryGetValue(key, out var cached)) return cached;

        var fallback = UiKit.PaletteColor(Math.Max(0, _availableScenes.IndexOf(package)));
        var thumb = UiKit.GetSceneThumbnail(package);
        var color = thumb is not null ? UiKit.ExtractDominantColor(thumb, fallback) : fallback;

        _accentColors[key] = color;
        return color;
    }
}
