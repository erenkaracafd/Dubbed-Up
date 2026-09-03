using System;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class MainMenuScreen : BaseScreen
{
    private PanelContainer? _emblemCard;
    private Button? _playButton;
    private Button? _onlinePlayButton;
    private Button? _studioButton;
    private Button? _settingsButton;
    private Button? _quitButton;
    private Button? _musicToggleButton;
    private MenuMusicController? _musicManager;

    public override void Initialize(IScreenNavigator navigator, LocalSessionCoordinator coordinator)
    {
        base.Initialize(navigator, coordinator);

        if (navigator is LocalNavigationController navCtrl)
        {
            _musicManager = navCtrl.MusicManager;
            _musicManager.BeatPulse += OnMusicBeatPulse;
            UpdateMusicButtonState();
        }
    }

    public override void _Ready()
    {
        _emblemCard = GetNodeOrNull<PanelContainer>("CenterContainer/ContentVBox/HeroEmblemContainer/EmblemCard");
        _playButton = GetNodeOrNull<Button>("CenterContainer/ContentVBox/ButtonsVBox/PlayButton");
        _onlinePlayButton = GetNodeOrNull<Button>("CenterContainer/ContentVBox/ButtonsVBox/OnlinePlayButton");
        _studioButton = GetNodeOrNull<Button>("CenterContainer/ContentVBox/ButtonsVBox/StudioButton");
        _settingsButton = GetNodeOrNull<Button>("CenterContainer/ContentVBox/ButtonsVBox/SettingsButton");
        _quitButton = GetNodeOrNull<Button>("CenterContainer/ContentVBox/ButtonsVBox/QuitButton");
        _musicToggleButton = GetNodeOrNull<Button>("TopBar/TopHBox/MusicToggleButton");

        ApplyStyling();

        if (_playButton is not null)
        {
            _playButton.Pressed += OnPlayButtonPressed;
            AttachHoverAnimation(_playButton);
        }

        if (_onlinePlayButton is not null)
        {
            _onlinePlayButton.Pressed += OnOnlinePlayPressed;
            AttachHoverAnimation(_onlinePlayButton);
        }

        if (_studioButton is not null)
        {
            _studioButton.Pressed += OnStudioPressed;
            AttachHoverAnimation(_studioButton);
        }

        if (_settingsButton is not null)
        {
            _settingsButton.Pressed += OnSettingsPressed;
            AttachHoverAnimation(_settingsButton);
        }

        if (_quitButton is not null)
        {
            _quitButton.Pressed += OnQuitButtonPressed;
            AttachHoverAnimation(_quitButton);
        }

        if (_musicToggleButton is not null)
        {
            _musicToggleButton.Pressed += OnMusicTogglePressed;
            AttachHoverAnimation(_musicToggleButton);
        }
    }

    public override void _ExitTree()
    {
        if (_musicManager is not null)
        {
            _musicManager.BeatPulse -= OnMusicBeatPulse;
        }
    }

    private void ApplyStyling()
    {
        // 1. Emblem Card (Center Cookie)
        if (_emblemCard is not null)
        {
            var emblemBox = new StyleBoxFlat
            {
                BgColor = new Color(1f, 1f, 1f, 0.95f),
                BorderWidthLeft = 3,
                BorderWidthTop = 3,
                BorderWidthRight = 3,
                BorderWidthBottom = 3,
                BorderColor = new Color(1.0f, 0.400f, 0.667f, 0.70f), // Soft Sakura Pink
                CornerRadiusTopLeft = 30,
                CornerRadiusTopRight = 30,
                CornerRadiusBottomLeft = 30,
                CornerRadiusBottomRight = 30,
                ShadowColor = new Color(1.0f, 0.243f, 0.514f, 0.15f),
                ShadowSize = 14,
                ShadowOffset = new Vector2(0, 6),
                ContentMarginLeft = 24,
                ContentMarginRight = 24,
                ContentMarginTop = 14,
                ContentMarginBottom = 14
            };
            _emblemCard.AddThemeStyleboxOverride("panel", emblemBox);
        }

        // 2. Play Button (Hot Pink)
        if (_playButton is not null)
        {
            StylePillButton(
                _playButton,
                normalColor: new Color(1.0f, 0.243f, 0.514f), // #FF3E83
                hoverColor: new Color(1.0f, 0.350f, 0.590f),
                pressedColor: new Color(0.880f, 0.150f, 0.420f),
                textColor: Colors.White,
                shadowColor: new Color(1.0f, 0.243f, 0.514f, 0.35f),
                radius: 26
            );
        }

        // 3. Online Play Button (Sky Blue)
        if (_onlinePlayButton is not null)
        {
            StylePillButton(
                _onlinePlayButton,
                normalColor: new Color(0.220f, 0.714f, 1.0f), // #38B6FF
                hoverColor: new Color(0.370f, 0.770f, 1.0f),
                pressedColor: new Color(0.110f, 0.640f, 0.940f),
                textColor: Colors.White,
                shadowColor: new Color(0.220f, 0.714f, 1.0f, 0.30f),
                radius: 25
            );
        }

        // 4. Studio Button (Pastel Violet)
        if (_studioButton is not null)
        {
            StylePillButton(
                _studioButton,
                normalColor: new Color(0.561f, 0.396f, 0.973f), // #8F65F8
                hoverColor: new Color(0.650f, 0.510f, 1.0f),
                pressedColor: new Color(0.470f, 0.290f, 0.920f),
                textColor: Colors.White,
                shadowColor: new Color(0.561f, 0.396f, 0.973f, 0.25f),
                radius: 24
            );
        }

        // 5. Settings Button (Porcelain White with Subtle Border)
        if (_settingsButton is not null)
        {
            StyleOutlineButton(
                _settingsButton,
                normalBg: Colors.White,
                hoverBg: new Color(0.940f, 0.955f, 0.985f),
                pressedBg: new Color(0.890f, 0.920f, 0.970f),
                borderColor: new Color(0.886f, 0.902f, 0.941f),
                textColor: new Color(0.118f, 0.106f, 0.294f),
                radius: 23
            );
        }

        // 6. Quit Button (Minimal Ghost)
        if (_quitButton is not null)
        {
            StyleGhostButton(_quitButton, new Color(0.549f, 0.576f, 0.682f), new Color(0.118f, 0.106f, 0.294f));
        }

        // 7. Music Toggle Button
        if (_musicToggleButton is not null)
        {
            StyleOutlineButton(
                _musicToggleButton,
                normalBg: Colors.White,
                hoverBg: new Color(0.940f, 0.955f, 0.985f),
                pressedBg: new Color(0.890f, 0.920f, 0.970f),
                borderColor: new Color(0.886f, 0.902f, 0.941f),
                textColor: new Color(0.294f, 0.322f, 0.439f),
                radius: 18
            );
        }
    }

    private static void StylePillButton(Button btn, Color normalColor, Color hoverColor, Color pressedColor, Color textColor, Color shadowColor, int radius)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = normalColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = shadowColor,
            ShadowSize = 8,
            ShadowOffset = new Vector2(0, 3)
        };

        var hover = new StyleBoxFlat
        {
            BgColor = hoverColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = shadowColor,
            ShadowSize = 12,
            ShadowOffset = new Vector2(0, 4)
        };

        var pressed = new StyleBoxFlat
        {
            BgColor = pressedColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowSize = 2,
            ShadowOffset = new Vector2(0, 1)
        };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", textColor);
        btn.AddThemeColorOverride("font_hover_color", textColor);
        btn.AddThemeColorOverride("font_pressed_color", textColor);
    }

    private static void StyleOutlineButton(Button btn, Color normalBg, Color hoverBg, Color pressedBg, Color borderColor, Color textColor, int radius)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = normalBg,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = borderColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.05f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(0, 2)
        };

        var hover = new StyleBoxFlat
        {
            BgColor = hoverBg,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.38f, 0.71f, 1.0f, 0.7f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowSize = 6,
            ShadowOffset = new Vector2(0, 3)
        };

        var pressed = new StyleBoxFlat
        {
            BgColor = pressedBg,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = borderColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowSize = 1
        };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", pressed);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeColorOverride("font_color", textColor);
        btn.AddThemeColorOverride("font_hover_color", textColor);
        btn.AddThemeColorOverride("font_pressed_color", textColor);
    }

    private static void StyleGhostButton(Button btn, Color textColor, Color hoverTextColor)
    {
        var empty = new StyleBoxEmpty();
        btn.AddThemeStyleboxOverride("normal", empty);
        btn.AddThemeStyleboxOverride("hover", empty);
        btn.AddThemeStyleboxOverride("pressed", empty);
        btn.AddThemeStyleboxOverride("focus", empty);
        btn.AddThemeColorOverride("font_color", textColor);
        btn.AddThemeColorOverride("font_hover_color", hoverTextColor);
    }

    private static void AttachHoverAnimation(Button button)
    {
        button.MouseEntered += () =>
        {
            var tween = button.CreateTween();
            tween?.TweenProperty(button, "scale", new Vector2(1.035f, 1.035f), 0.12f)
                  .SetTrans(Tween.TransitionType.Back)
                  .SetEase(Tween.EaseType.Out);
        };

        button.MouseExited += () =>
        {
            var tween = button.CreateTween();
            tween?.TweenProperty(button, "scale", Vector2.One, 0.10f)
                  .SetTrans(Tween.TransitionType.Cubic)
                  .SetEase(Tween.EaseType.Out);
        };

        button.ButtonDown += () =>
        {
            var tween = button.CreateTween();
            tween?.TweenProperty(button, "scale", new Vector2(0.97f, 0.97f), 0.05f);
        };

        button.ButtonUp += () =>
        {
            var tween = button.CreateTween();
            tween?.TweenProperty(button, "scale", new Vector2(1.035f, 1.035f), 0.08f);
        };
    }

    private void OnMusicBeatPulse(int beatIndex)
    {
        // osu!-inspired beat pulse on the center emblem
        if (_emblemCard is null) return;

        var tween = _emblemCard.CreateTween();
        if (tween is null) return;

        tween.TweenProperty(_emblemCard, "scale", new Vector2(1.03f, 1.03f), 0.08f)
             .SetTrans(Tween.TransitionType.Back)
             .SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_emblemCard, "scale", Vector2.One, 0.20f)
             .SetTrans(Tween.TransitionType.Cubic)
             .SetEase(Tween.EaseType.Out);
    }

    private void OnPlayButtonPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }

    private void OnOnlinePlayPressed()
    {
        Navigator?.NavigateTo(AppScreen.Lobby);
    }

    private void OnStudioPressed()
    {
        Navigator?.NavigateTo(AppScreen.ScenePicker);
    }

    private void OnSettingsPressed()
    {
        Navigator?.NavigateTo(AppScreen.Settings);
    }

    private void OnQuitButtonPressed()
    {
        GetTree().Quit();
    }

    private void OnMusicTogglePressed()
    {
        if (_musicManager is not null)
        {
            _musicManager.IsMuted = !_musicManager.IsMuted;
            UpdateMusicButtonState();
        }
    }

    private void UpdateMusicButtonState()
    {
        if (_musicToggleButton is not null && _musicManager is not null)
        {
            _musicToggleButton.Text = _musicManager.IsMuted ? "🔇 Music: OFF" : "🎵 Music: ON";
        }
    }
}
