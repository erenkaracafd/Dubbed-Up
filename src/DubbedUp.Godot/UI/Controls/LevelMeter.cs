using Godot;

namespace DubbedUp.Godot.UI.Controls;

/// <summary>
/// Rounded input-level bar: smooths the incoming level, colors it mint → peach → red as it
/// gets louder, and keeps a short-lived peak marker.
/// </summary>
public partial class LevelMeter : Control
{
    private float _shown;      // smoothed 0..100
    private float _peak;       // 0..100
    private double _peakHold;
    private float _target;

    public bool Active { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 14);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Sets the current level in the 0..100 range.</summary>
    public void SetLevel(double level)
    {
        _target = (float)Math.Clamp(level, 0.0, 100.0);
    }

    public override void _Process(double delta)
    {
        var target = Active ? _target : 0f;
        // Fast attack, slower release reads like a real meter.
        var rate = target > _shown ? 30f : 8f;
        _shown = Mathf.Lerp(_shown, target, 1f - Mathf.Exp(-(float)delta * rate));

        if (_shown >= _peak)
        {
            _peak = _shown;
            _peakHold = 0.8;
        }
        else if ((_peakHold -= delta) <= 0)
        {
            _peak = Mathf.MoveToward(_peak, _shown, (float)delta * 60f);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var radius = (int)(Size.Y / 2f);
        var track = UiKit.RoundedBox(new Color(0.88f, 0.90f, 0.96f), radius);
        DrawStyleBox(track, new Rect2(Vector2.Zero, Size));

        var fraction = _shown / 100f;
        if (fraction > 0.01f)
        {
            var color = fraction < 0.6f ? UiKit.Mint : fraction < 0.85f ? new Color(0.98f, 0.64f, 0.25f) : UiKit.Danger;
            var fill = UiKit.RoundedBox(color, radius);
            DrawStyleBox(fill, new Rect2(Vector2.Zero, new Vector2(Mathf.Max(Size.Y, Size.X * fraction), Size.Y)));
        }

        if (_peak > 2f)
        {
            var x = Mathf.Clamp(Size.X * _peak / 100f, 3f, Size.X - 3f);
            DrawLine(new Vector2(x, 2), new Vector2(x, Size.Y - 2), UiKit.Ink with { A = 0.35f }, 2f, true);
        }
    }
}
