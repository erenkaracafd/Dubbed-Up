using Godot;

namespace DubbedUp.Godot.UI.Controls;

/// <summary>Pill-shaped on/off switch with an animated knob.</summary>
public partial class ToggleSwitch : Control
{
    private const float AnimationSpeed = 10f;

    private bool _isOn;
    private float _position; // 0 = off, 1 = on (animated)
    private bool _hovered;

    public event Action<bool>? Toggled;

    public Color OnColor { get; set; } = UiKit.Mint;

    public bool IsOn
    {
        get => _isOn;
        set => SetOn(value, emit: false, animate: false);
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(54, 30);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.All;
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent @event)
    {
        var activated = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
            || (@event.IsActionPressed("ui_accept") && HasFocus());
        if (activated)
        {
            AudioPlayback.UiSoundManager.Instance.PlayClick();
            SetOn(!_isOn, emit: true, animate: true);
            AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        var target = _isOn ? 1f : 0f;
        if (!Mathf.IsEqualApprox(_position, target))
        {
            _position = Mathf.MoveToward(_position, target, (float)delta * AnimationSpeed);
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = CustomMinimumSize;
        var origin = new Vector2(0, (Size.Y - size.Y) / 2f);
        var radius = (int)(size.Y / 2f);
        var eased = 1f - Mathf.Pow(1f - _position, 3f);

        var offColor = new Color(0.82f, 0.85f, 0.92f);
        var track = UiKit.RoundedBox(offColor.Lerp(OnColor, eased), radius);
        track.CornerDetail = 16;
        if (_hovered)
        {
            track.ShadowColor = OnColor with { A = 0.25f };
            track.ShadowSize = 6;
        }
        DrawStyleBox(track, new Rect2(origin, size));

        var knobRadius = size.Y / 2f - 3f;
        var knobX = Mathf.Lerp(origin.X + 3f + knobRadius, origin.X + size.X - 3f - knobRadius, eased);
        var knobCenter = new Vector2(knobX, origin.Y + size.Y / 2f);
        DrawCircle(knobCenter + new Vector2(0, 1.5f), knobRadius, new Color(0, 0, 0, 0.12f), true, -1, true);
        DrawCircle(knobCenter, knobRadius, Colors.White, true, -1, true);
    }

    private void SetOn(bool value, bool emit, bool animate)
    {
        if (_isOn == value && !animate) return;
        _isOn = value;
        if (!animate) _position = value ? 1f : 0f;
        QueueRedraw();
        if (emit) Toggled?.Invoke(value);
    }
}
