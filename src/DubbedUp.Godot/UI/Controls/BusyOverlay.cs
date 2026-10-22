using Godot;

namespace DubbedUp.Godot.UI.Controls;

/// <summary>
/// Modal "working on it" overlay for long background tasks (speech detection, import, export).
/// Dims the screen, blocks input and shows an animated equalizer, a title, a detail line and elapsed time.
/// Call <see cref="Open"/> on the main thread and <see cref="Close"/> when the task finishes.
/// </summary>
public partial class BusyOverlay : Control
{
    private Label? _detailLabel;
    private Label? _elapsedLabel;
    private PanelContainer? _card;
    private double _elapsed;
    private bool _closing;

    public static BusyOverlay Open(Control parent, string title, string detail)
    {
        var overlay = new BusyOverlay();
        overlay.Build(title, detail);
        parent.AddChild(overlay);
        overlay.PlayOpen();
        return overlay;
    }

    /// <summary>Updates the detail line; safe to call from any thread.</summary>
    public void SetDetail(string text)
    {
        Callable.From(() =>
        {
            if (_detailLabel is not null && GodotObject.IsInstanceValid(_detailLabel)) _detailLabel.Text = text;
        }).CallDeferred();
    }

    public void Close()
    {
        if (_closing || !IsInstanceValid(this)) return;
        _closing = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0.0f, 0.18f);
        tween.Finished += QueueFree;
    }

    private void Build(string title, string detail)
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 200;

        var dim = new ColorRect { Color = new Color(0.07f, 0.06f, 0.18f, 0.34f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _card = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        var box = UiKit.CardBox(padding: 28);
        box.BgColor = Colors.White;
        box.ShadowColor = new Color(0.08f, 0.06f, 0.25f, 0.25f);
        box.ShadowSize = 30;
        _card.AddThemeStyleboxOverride("panel", box);
        center.AddChild(_card);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);
        _card.AddChild(stack);

        stack.AddChild(new EqualizerAnimation { CustomMinimumSize = new Vector2(0, 64) });

        var titleLabel = UiKit.Heading(title, 22);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(titleLabel);

        _detailLabel = UiKit.Body(detail, 14);
        _detailLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detailLabel.CustomMinimumSize = new Vector2(324, 0);
        stack.AddChild(_detailLabel);

        _elapsedLabel = UiKit.Body("0s", 12, UiKit.Muted with { A = 0.8f });
        _elapsedLabel.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(_elapsedLabel);
    }

    private void PlayOpen()
    {
        Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(this, "modulate:a", 1.0f, 0.2f);
        if (_card is not null)
        {
            _card.Scale = new Vector2(0.9f, 0.9f);
            _card.Resized += () => _card.PivotOffset = _card.Size * 0.5f;
            tween.TweenProperty(_card, "scale", Vector2.One, 0.36f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsedLabel is not null)
        {
            var seconds = (int)_elapsed;
            _elapsedLabel.Text = seconds < 60 ? $"{seconds}s" : $"{seconds / 60}m {seconds % 60:00}s";
        }
    }

    /// <summary>Five rounded bars bouncing out of phase, in the menu palette.</summary>
    private sealed partial class EqualizerAnimation : Control
    {
        private const int BarCount = 5;
        private const float BarWidth = 12f;
        private const float Gap = 9f;
        private double _time;

        public override void _Process(double delta)
        {
            _time += delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var total = BarCount * BarWidth + (BarCount - 1) * Gap;
            var startX = (Size.X - total) * 0.5f;
            var baseline = Size.Y;

            for (var i = 0; i < BarCount; i++)
            {
                // Two blended sines give an organic, non-repeating bounce per bar
                var t = (float)_time;
                var wave = 0.5f + 0.32f * Mathf.Sin(t * 5.2f + i * 1.1f) + 0.18f * Mathf.Sin(t * 8.3f + i * 2.3f);
                var height = Mathf.Lerp(14f, Size.Y, Mathf.Clamp(wave, 0f, 1f));

                var rect = new Rect2(startX + i * (BarWidth + Gap), baseline - height, BarWidth, height);
                var bar = UiKit.RoundedBox(UiKit.Palette[i * 3 % UiKit.Palette.Length], (int)(BarWidth / 2));
                DrawStyleBox(bar, rect);
            }
        }
    }
}
