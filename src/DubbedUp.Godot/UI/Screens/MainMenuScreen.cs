using System;
using DubbedUp.Godot.AudioPlayback;
using DubbedUp.Godot.LocalSession;
using DubbedUp.Godot.UI.Controls;
using Godot;

namespace DubbedUp.Godot.UI.Screens;

public partial class MainMenuScreen : BaseScreen
{
    private MenuBackgroundVisuals? _bgVisuals;
    private PanelContainer? _topBar;
    private PanelContainer? _bottomBar;
    private PanelContainer? _heroEmblem;
    private Button? _playButton;
    private Button? _onlinePlayButton;
    private Button? _studioButton;
    private Button? _settingsButton;
    private Button? _quitButton;
    private Button? _musicToggleButton;
    private Button? _fullscreenButton;

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
        _bgVisuals = GetNodeOrNull<MenuBackgroundVisuals>("MenuBackgroundVisuals");
        _topBar = GetNodeOrNull<PanelContainer>("TopBar");
        _bottomBar = GetNodeOrNull<PanelContainer>("BottomBar");
        _heroEmblem = GetNodeOrNull<PanelContainer>("CenterArea/MainHBox/LeftHeroContainer/HeroEmblem");

        _playButton = GetNodeOrNull<Button>("CenterArea/MainHBox/RightMenuContainer/PlayButton");
        _onlinePlayButton = GetNodeOrNull<Button>("CenterArea/MainHBox/RightMenuContainer/OnlinePlayButton");
        _studioButton = GetNodeOrNull<Button>("CenterArea/MainHBox/RightMenuContainer/StudioButton");
        _settingsButton = GetNodeOrNull<Button>("CenterArea/MainHBox/RightMenuContainer/SettingsButton");
        _quitButton = GetNodeOrNull<Button>("CenterArea/MainHBox/RightMenuContainer/QuitButton");

        _musicToggleButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/MusicToggleButton");
        _fullscreenButton = GetNodeOrNull<Button>("TopBar/TopMargin/TopHBox/FullscreenButton");

        ApplyOsuStyling();

        SetupButton(_playButton, OnPlayButtonPressed);
        SetupButton(_onlinePlayButton, OnOnlinePlayPressed);
        SetupButton(_studioButton, OnStudioPressed);
        SetupButton(_settingsButton, OnSettingsPressed);
        SetupButton(_quitButton, OnQuitButtonPressed);
        SetupButton(_musicToggleButton, OnMusicTogglePressed);
        SetupButton(_fullscreenButton, OnFullscreenPressed);
    }

    public override void _ExitTree()
    {
        if (_musicManager is not null)
        {
            _musicManager.BeatPulse -= OnMusicBeatPulse;
        }
    }

    private void SetupButton(Button? btn, Action action)
    {
        if (btn is null) return;
        btn.Pressed += action;
        UiSoundManager.Attach(btn);
        AttachOsuWedgeHover(btn);
    }

    private void ApplyOsuStyling()
    {
        // 1. Top Bar & Bottom Bar (Frosted translucent porcelain)
        var barStyle = new StyleBoxFlat
        {
            BgColor = new Color(1.0f, 1.0f, 1.0f, 0.88f),
            BorderWidthBottom = 1,
            BorderColor = new Color(0.886f, 0.902f, 0.941f, 0.8f),
            ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
            ShadowSize = 6
        };
        _topBar?.AddThemeStyleboxOverride("panel", barStyle);

        var bottomBarStyle = new StyleBoxFlat
        {
            BgColor = new Color(1.0f, 1.0f, 1.0f, 0.88f),
            BorderWidthTop = 1,
            BorderColor = new Color(0.886f, 0.902f, 0.941f, 0.8f),
            ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
            ShadowSize = 4
        };
        _bottomBar?.AddThemeStyleboxOverride("panel", bottomBarStyle);

        // 2. Hero Cookie (The central circular emblem)
        if (_heroEmblem is not null)
        {
            var cookieStyle = new StyleBoxFlat
            {
                BgColor = new Color(1.0f, 1.0f, 1.0f, 0.98f),
                BorderWidthLeft = 4,
                BorderWidthTop = 4,
                BorderWidthRight = 4,
                BorderWidthBottom = 4,
                BorderColor = new Color(1.0f, 0.400f, 0.667f, 0.90f), // Hot Sakura Pink border
                CornerRadiusTopLeft = 140,
                CornerRadiusTopRight = 140,
                CornerRadiusBottomLeft = 140,
                CornerRadiusBottomRight = 140,
                ShadowColor = new Color(1.0f, 0.243f, 0.514f, 0.22f), // Pink ambient glow
                ShadowSize = 22,
                ShadowOffset = new Vector2(0, 8),
                ContentMarginLeft = 20,
                ContentMarginRight = 20,
                ContentMarginTop = 20,
                ContentMarginBottom = 20
            };
            _heroEmblem.AddThemeStyleboxOverride("panel", cookieStyle);
        }

        // 3. Play Party Button (Hot Pink #FF3E83)
        if (_playButton is not null)
        {
            StyleActionPill(
                _playButton,
                normalColor: new Color(1.0f, 0.243f, 0.514f),
                hoverColor: new Color(1.0f, 0.360f, 0.600f),
                pressedColor: new Color(0.870f, 0.140f, 0.410f),
                textColor: Colors.White,
                glowColor: new Color(1.0f, 0.243f, 0.514f, 0.35f),
                radius: 29
            );
        }

        // 4. Online Multiplayer Button (Sky Blue #38B6FF)
        if (_onlinePlayButton is not null)
        {
            StyleActionPill(
                _onlinePlayButton,
                normalColor: new Color(0.220f, 0.714f, 1.000f),
                hoverColor: new Color(0.380f, 0.780f, 1.000f),
                pressedColor: new Color(0.120f, 0.630f, 0.940f),
                textColor: Colors.White,
                glowColor: new Color(0.220f, 0.714f, 1.000f, 0.30f),
                radius: 27
            );
        }

        // 5. Scene Studio Button (Pastel Violet #8F65F8)
        if (_studioButton is not null)
        {
            StyleActionPill(
                _studioButton,
                normalColor: new Color(0.561f, 0.396f, 0.973f),
                hoverColor: new Color(0.660f, 0.520f, 1.000f),
                pressedColor: new Color(0.460f, 0.280f, 0.910f),
                textColor: Colors.White,
                glowColor: new Color(0.561f, 0.396f, 0.973f, 0.25f),
                radius: 25
            );
        }

        // 6. Settings Button (Crisp Porcelain White with Slate Border)
        if (_settingsButton is not null)
        {
            StyleOutlinePill(
                _settingsButton,
                normalBg: Colors.White,
                hoverBg: new Color(0.940f, 0.955f, 0.985f),
                pressedBg: new Color(0.890f, 0.920f, 0.970f),
                borderColor: new Color(0.886f, 0.902f, 0.941f),
                textColor: new Color(0.118f, 0.106f, 0.294f),
                radius: 24
            );
        }

        // 7. Quit Button (Minimal Ghost)
        if (_quitButton is not null)
        {
            StyleGhostButton(_quitButton, new Color(0.549f, 0.576f, 0.682f), new Color(0.95f, 0.25f, 0.35f));
        }

        // 8. Top Controls
        if (_musicToggleButton is not null)
        {
            StyleOutlinePill(_musicToggleButton, Colors.White, new Color(0.94f, 0.96f, 0.99f), new Color(0.9f, 0.93f, 0.98f), new Color(0.88f, 0.90f, 0.94f), new Color(0.294f, 0.322f, 0.439f), 16);
        }
        if (_fullscreenButton is not null)
        {
            StyleOutlinePill(_fullscreenButton, Colors.White, new Color(0.94f, 0.96f, 0.99f), new Color(0.9f, 0.93f, 0.98f), new Color(0.88f, 0.90f, 0.94f), new Color(0.294f, 0.322f, 0.439f), 16);
        }
    }

    private static void StyleActionPill(Button btn, Color normalColor, Color hoverColor, Color pressedColor, Color textColor, Color glowColor, int radius)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = normalColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = glowColor,
            ShadowSize = 10,
            ShadowOffset = new Vector2(0, 4)
        };

        var hover = new StyleBoxFlat
        {
            BgColor = hoverColor,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = glowColor,
            ShadowSize = 16,
            ShadowOffset = new Vector2(0, 6)
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

    private static void StyleOutlinePill(Button btn, Color normalBg, Color hoverBg, Color pressedBg, Color borderColor, Color textColor, int radius)
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
            ShadowColor = new Color(0.1f, 0.1f, 0.2f, 0.04f),
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
            BorderColor = new Color(0.38f, 0.71f, 1.0f, 0.8f),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowSize = 8,
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

    private static void StyleGhostButton(Button btn, Color normalColor, Color hoverColor)
    {
        var empty = new StyleBoxEmpty();
        btn.AddThemeStyleboxOverride("normal", empty);
        btn.AddThemeStyleboxOverride("hover", empty);
        btn.AddThemeStyleboxOverride("pressed", empty);
        btn.AddThemeStyleboxOverride("focus", empty);
        btn.AddThemeColorOverride("font_color", normalColor);
        btn.AddThemeColorOverride("font_hover_color", hoverColor);
    }

    private static void AttachOsuWedgeHover(Button button)
    {
        button.MouseEntered += () =>
        {
            var tween = button.CreateTween();
            tween?.SetParallel(true);
            tween?.TweenProperty(button, "scale", new Vector2(1.035f, 1.035f), 0.12f)
                  .SetTrans(Tween.TransitionType.Back)
                  .SetEase(Tween.EaseType.Out);
        };

        button.MouseExited += () =>
        {
            var tween = button.CreateTween();
            tween?.SetParallel(true);
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
        // 1. Rhythmic bounce on the hero cookie
        if (_heroEmblem is not null)
        {
            var tween = _heroEmblem.CreateTween();
            if (tween is not null)
            {
                tween.TweenProperty(_heroEmblem, "scale", new Vector2(1.032f, 1.032f), 0.07f)
                     .SetTrans(Tween.TransitionType.Back)
                     .SetEase(Tween.EaseType.Out);

                tween.TweenProperty(_heroEmblem, "scale", Vector2.One, 0.18f)
                     .SetTrans(Tween.TransitionType.Cubic)
                     .SetEase(Tween.EaseType.Out);
            }
        }

        // 2. Ripple floating background particles
        _bgVisuals?.TriggerBeatPulse();
    }

    private void TriggerButtonPunch(Button? btn, Action action)
    {
        if (btn is null)
        {
            action();
            return;
        }

        var tween = btn.CreateTween();
        tween?.TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.07f)
              .SetTrans(Tween.TransitionType.Back)
              .SetEase(Tween.EaseType.Out);

        if (tween is not null)
        {
            tween.Finished += () => action();
        }
        else
        {
            action();
        }
    }

    private void OnPlayButtonPressed()
    {
        TriggerButtonPunch(_playButton, () => Navigator?.NavigateTo(AppScreen.ScenePicker));
    }

    private void OnOnlinePlayPressed()
    {
        TriggerButtonPunch(_onlinePlayButton, () => Navigator?.NavigateTo(AppScreen.Lobby));
    }

    private void OnStudioPressed()
    {
        TriggerButtonPunch(_studioButton, () => Navigator?.NavigateTo(AppScreen.ScenePicker));
    }

    private void OnSettingsPressed()
    {
        TriggerButtonPunch(_settingsButton, () => Navigator?.NavigateTo(AppScreen.Settings));
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

    private void OnFullscreenPressed()
    {
        var isFullscreen = DisplayServer.WindowGetMode(0) is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        LocalNavigationController.SetFullscreen(!isFullscreen, this);
    }

    private void UpdateMusicButtonState()
    {
        if (_musicToggleButton is not null && _musicManager is not null)
        {
            _musicToggleButton.Text = _musicManager.IsMuted ? "🔇 Music: OFF" : "🔊 Music: ON";
        }
    }
}
