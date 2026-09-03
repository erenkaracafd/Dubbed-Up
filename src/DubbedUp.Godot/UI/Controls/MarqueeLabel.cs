using System;
using Godot;

namespace DubbedUp.Godot.UI.Controls;

public partial class MarqueeLabel : Control
{
    private readonly Label _label = new();
    private Tween? _scrollTween;
    private bool _isHovered;
    private float _textWidth;

    public string Text
    {
        get => _label.Text;
        set
        {
            _label.Text = value;
            UpdateTextSize();
        }
    }

    public Color FontColor
    {
        get => _label.GetThemeColor("font_color");
        set => _label.AddThemeColorOverride("font_color", value);
    }

    public int FontSize
    {
        get => _label.GetThemeFontSize("font_size");
        set => _label.AddThemeFontSizeOverride("font_size", value);
    }

    public override void _Ready()
    {
        ClipContents = true;
        CustomMinimumSize = new Vector2(0, 22);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;

        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.HorizontalAlignment = HorizontalAlignment.Left;
        _label.AutowrapMode = TextServer.AutowrapMode.Off;
        _label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        AddChild(_label);

        Resized += OnResized;
        UpdateTextSize();
    }

    private void OnResized()
    {
        UpdateTextSize();
        if (_isHovered)
        {
            StartScroll();
        }
        else
        {
            ResetScroll();
        }
    }

    public void UpdateTextSize()
    {
        if (string.IsNullOrEmpty(_label.Text))
        {
            _textWidth = 0;
            return;
        }

        var font = _label.GetThemeDefaultFont();
        var fontSize = _label.GetThemeFontSize("font_size");
        if (fontSize <= 0) fontSize = 15;

        if (font is not null)
        {
            _textWidth = font.GetStringSize(_label.Text, HorizontalAlignment.Left, -1, fontSize).X;
        }
        else
        {
            _textWidth = _label.GetCombinedMinimumSize().X;
        }

        _label.Size = new Vector2(MathF.Max(_textWidth + 12, Size.X), Size.Y > 0 ? Size.Y : 22);
    }

    public void OnCardHovered()
    {
        _isHovered = true;
        StartScroll();
    }

    public void OnCardUnhovered()
    {
        _isHovered = false;
        ResetScroll();
    }

    private void StartScroll()
    {
        _scrollTween?.Kill();
        UpdateTextSize();

        var overflow = _textWidth - Size.X;
        if (overflow <= 4 || Size.X <= 10)
        {
            // Fits cleanly within view bounds, no scrolling needed
            _label.Position = Vector2.Zero;
            return;
        }

        var scrollDistance = overflow + 20f;
        var scrollDuration = MathF.Max(1.0f, scrollDistance / 36.0f); // 36 px/sec reading speed

        _scrollTween = CreateTween();
        _scrollTween.SetLoops();
        _scrollTween.TweenInterval(0.4);
        _scrollTween.TweenProperty(_label, "position:x", -scrollDistance, scrollDuration)
                    .SetTrans(Tween.TransitionType.Linear);
        _scrollTween.TweenInterval(0.8);
        _scrollTween.TweenProperty(_label, "position:x", 0.0f, 0.35)
                    .SetTrans(Tween.TransitionType.Cubic)
                    .SetEase(Tween.EaseType.Out);
        _scrollTween.TweenInterval(0.3);
    }

    private void ResetScroll()
    {
        _scrollTween?.Kill();
        var tween = CreateTween();
        tween?.TweenProperty(_label, "position:x", 0.0f, 0.15)
              .SetTrans(Tween.TransitionType.Cubic)
              .SetEase(Tween.EaseType.Out);
    }
}
