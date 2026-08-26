using DubbedUp.Godot.AudioPlayback;
using Godot;

namespace DubbedUp.Godot.UI.Controls;

public partial class TimelineWaveformEditor : Control
{
    [Signal]
    public delegate void SeekRequestedEventHandler(double targetSeconds);

    [Signal]
    public delegate void SlotSelectedEventHandler(int slotIndex);

    [Signal]
    public delegate void SlotChangedEventHandler(int slotIndex);

    [Signal]
    public delegate void SlotDeleteRequestedEventHandler(int slotIndex);

    public sealed class TimelineSlotData
    {
        public string SlotId { get; set; } = "";
        public string CharacterName { get; set; } = "Karakter";
        public string Prompt { get; set; } = "";
        public double StartSeconds { get; set; } = 0.0;
        public double EndSeconds { get; set; } = 4.0;
        public Color BoxColor { get; set; } = new Color(0.9f, 0.65f, 0.2f, 0.4f);
    }

    private readonly List<TimelineSlotData> _slots = [];
    private float[]? _waveformPoints;
    private double _totalDuration = 22.0;
    private double _currentPlayhead = 0.0;
    private int _selectedSlotIndex = -1;

    // Dragging state
    private enum DragMode { None, Playhead, MoveBox, ResizeLeft, ResizeRight }
    private DragMode _currentDragMode = DragMode.None;
    private int _draggedSlotIndex = -1;
    private double _dragStartMouseX = 0.0;
    private double _dragOrigStartSec = 0.0;
    private double _dragOrigEndSec = 0.0;

    private static readonly Color[] PresetColors =
    [
        new Color(1.0f, 0.70f, 0.25f, 0.42f),  // Amber
        new Color(0.25f, 0.85f, 1.0f, 0.42f),  // Cyan
        new Color(0.50f, 1.0f, 0.45f, 0.42f),  // Green
        new Color(1.0f, 0.45f, 0.75f, 0.42f),  // Pink
        new Color(0.85f, 0.55f, 1.0f, 0.42f),  // Purple
    ];

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(820, 115);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
    }

    public void SetData(double totalDuration, List<TimelineSlotData> slots, float[]? waveform = null)
    {
        _totalDuration = Math.Max(1.0, totalDuration);
        _slots.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            s.BoxColor = PresetColors[i % PresetColors.Length];
            _slots.Add(s);
        }

        if (waveform is not null && waveform.Length > 0)
        {
            _waveformPoints = waveform;
        }

        QueueRedraw();
    }

    public void SetWaveform(float[]? waveform)
    {
        if (waveform is not null && waveform.Length > 0)
        {
            _waveformPoints = waveform;
            QueueRedraw();
        }
    }

    public void SetPlayhead(double currentSeconds)
    {
        _currentPlayhead = Math.Clamp(currentSeconds, 0.0, _totalDuration);
        QueueRedraw();
    }

    public void SelectSlot(int index)
    {
        _selectedSlotIndex = Math.Clamp(index, -1, _slots.Count - 1);
        QueueRedraw();
    }

    public int GetSelectedSlotIndex() => _selectedSlotIndex;

    public override void _Draw()
    {
        var size = Size;
        if (size.X <= 0 || size.Y <= 0 || _totalDuration <= 0) return;

        // 1. Background Frame
        var bgRect = new Rect2(Vector2.Zero, size);
        DrawRect(bgRect, new Color(0.06f, 0.08f, 0.12f, 1.0f));
        DrawRect(bgRect, new Color(0.20f, 0.35f, 0.55f, 0.9f), false, 2.0f);

        // 2. Time Grid & Ruler Marks
        var font = ThemeDB.FallbackFont;
        var fontSize = 11;
        var stepSec = _totalDuration > 60.0 ? 5.0 : (_totalDuration > 20.0 ? 2.0 : 1.0);

        for (double t = 0; t <= _totalDuration; t += stepSec)
        {
            var x = (float)((t / _totalDuration) * size.X);
            var isMajor = ((int)Math.Round(t)) % 5 == 0;
            var lineColor = isMajor ? new Color(0.45f, 0.55f, 0.70f, 0.7f) : new Color(0.20f, 0.25f, 0.35f, 0.4f);
            var lineH = isMajor ? size.Y : 18.0f;

            DrawLine(new Vector2(x, 0), new Vector2(x, lineH), lineColor, 1.0f);

            if (isMajor && x < size.X - 35)
            {
                DrawString(font, new Vector2(x + 4, 13), $"{t:F0}s", HorizontalAlignment.Left, -1, fontSize, new Color(0.8f, 0.88f, 1.0f, 0.9f));
            }
        }

        // 3. Audio Waveform Track
        var centerY = size.Y * 0.56f;
        var maxAmpH = size.Y * 0.36f;

        DrawLine(new Vector2(0, centerY), new Vector2(size.X, centerY), new Color(0.25f, 0.40f, 0.60f, 0.5f), 1.0f);

        if (_waveformPoints is not null && _waveformPoints.Length > 0)
        {
            var count = _waveformPoints.Length;
            var stepPx = size.X / count;
            var barWidth = Math.Max(2.0f, stepPx * 0.85f);

            for (int i = 0; i < count; i++)
            {
                var x = (float)i / count * size.X;
                var rawAmp = _waveformPoints[i];
                var amp = Math.Max(1.5f, Math.Clamp(rawAmp, 0.0f, 1.0f) * maxAmpH);

                var barColor = rawAmp > 0.05f
                    ? new Color(0.20f, 0.85f, 1.0f, 0.85f)
                    : new Color(0.15f, 0.45f, 0.65f, 0.40f);

                DrawLine(new Vector2(x, centerY - amp), new Vector2(x, centerY + amp), barColor, barWidth);
            }
        }
        else
        {
            for (float x = 0; x < size.X; x += 4)
            {
                var idleAmp = (float)(Math.Sin(x * 0.05) * 3.0);
                DrawLine(new Vector2(x, centerY - idleAmp), new Vector2(x, centerY + idleAmp), new Color(0.2f, 0.5f, 0.7f, 0.4f), 2.0f);
            }
        }

        // 4. Transparent Speech Selection Boxes (Overlaid on Waveform)
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var xStart = (float)(Math.Clamp(slot.StartSeconds / _totalDuration, 0.0, 1.0) * size.X);
            var xEnd = (float)(Math.Clamp(slot.EndSeconds / _totalDuration, 0.0, 1.0) * size.X);
            var width = Math.Max(14.0f, xEnd - xStart);

            var isSelected = (i == _selectedSlotIndex);
            var baseColor = slot.BoxColor;
            var boxColor = isSelected ? new Color(0.2f, 0.95f, 0.45f, 0.55f) : baseColor;
            var borderColor = isSelected ? new Color(0.4f, 1.0f, 0.6f, 1.0f) : new Color(baseColor.R, baseColor.G, baseColor.B, 0.95f);

            var slotRect = new Rect2(xStart, 16, width, size.Y - 18);

            // Fill transparent colored box
            DrawRect(slotRect, boxColor);
            // Border
            DrawRect(slotRect, borderColor, false, isSelected ? 2.5f : 1.5f);

            // Left and Right resize handle bars
            DrawRect(new Rect2(xStart, 16, 5, size.Y - 18), borderColor);
            DrawRect(new Rect2(xStart + width - 5, 16, 5, size.Y - 18), borderColor);

            // Header Tag (Number & Character Name)
            var tagText = $"#{i + 1} {slot.CharacterName}";
            var tagWidth = Math.Min(width, 160);
            var tagBgRect = new Rect2(xStart, 16, tagWidth, 16);
            DrawRect(tagBgRect, new Color(0.0f, 0.0f, 0.0f, 0.80f));
            DrawString(font, new Vector2(xStart + 4, 28), tagText, HorizontalAlignment.Left, (int)tagWidth - 20, 11, new Color(1.0f, 1.0f, 1.0f, 1.0f));

            // Delete 'x' Icon button at top right of the box if width allows
            if (width >= 35)
            {
                var deleteIconRect = new Rect2(xStart + width - 16, 16, 16, 16);
                DrawRect(deleteIconRect, isSelected ? new Color(0.9f, 0.2f, 0.2f, 0.9f) : new Color(0.0f, 0.0f, 0.0f, 0.7f));
                DrawString(font, new Vector2(xStart + width - 13, 28), "✕", HorizontalAlignment.Left, -1, 10, new Color(1.0f, 1.0f, 1.0f, 1.0f));
            }
        }

        // 5. Red Moving Playhead
        var playheadX = (float)((_currentPlayhead / _totalDuration) * size.X);
        var playheadColor = new Color(1.0f, 0.20f, 0.20f, 1.0f);

        DrawLine(new Vector2(playheadX, 0), new Vector2(playheadX, size.Y), playheadColor, 2.5f);

        var pointerPoints = new Vector2[]
        {
            new(playheadX - 7, 0),
            new(playheadX + 7, 0),
            new(playheadX, 11)
        };
        DrawColoredPolygon(pointerPoints, playheadColor);
    }

    public override void _GuiInput(InputEvent @event)
    {
        var size = Size;
        if (size.X <= 0 || _totalDuration <= 0) return;

        if (@event is InputEventKey ek && ek.Pressed)
        {
            if (ek.Keycode == Key.Delete || ek.Keycode == Key.Backspace)
            {
                if (_selectedSlotIndex >= 0 && _selectedSlotIndex < _slots.Count)
                {
                    EmitSignal(SignalName.SlotDeleteRequested, _selectedSlotIndex);
                    return;
                }
            }
        }

        if (@event is InputEventMouseButton mb)
        {
            var mouseX = mb.Position.X;
            var mouseY = mb.Position.Y;
            var clickedSec = (mouseX / size.X) * _totalDuration;

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    GrabFocus();

                    // Check if clicked on slot box, delete icon, or resize edges
                    var hitSlot = -1;
                    var dragMode = DragMode.Playhead;

                    for (int i = _slots.Count - 1; i >= 0; i--)
                    {
                        var s = _slots[i];
                        var x1 = (s.StartSeconds / _totalDuration) * size.X;
                        var x2 = (s.EndSeconds / _totalDuration) * size.X;

                        if (mouseX >= x1 - 6 && mouseX <= x2 + 6)
                        {
                            // Check delete icon hit (top-right 16x16 area)
                            if (mouseY >= 16 && mouseY <= 32 && mouseX >= x2 - 18 && mouseX <= x2 + 2)
                            {
                                EmitSignal(SignalName.SlotDeleteRequested, i);
                                return;
                            }

                            hitSlot = i;
                            if (Math.Abs(mouseX - x1) <= 8) dragMode = DragMode.ResizeLeft;
                            else if (Math.Abs(mouseX - x2) <= 8) dragMode = DragMode.ResizeRight;
                            else dragMode = DragMode.MoveBox;
                            break;
                        }
                    }

                    if (hitSlot != -1)
                    {
                        _draggedSlotIndex = hitSlot;
                        _currentDragMode = dragMode;
                        _dragStartMouseX = mouseX;
                        _dragOrigStartSec = _slots[hitSlot].StartSeconds;
                        _dragOrigEndSec = _slots[hitSlot].EndSeconds;
                        SelectSlot(hitSlot);
                        EmitSignal(SignalName.SlotSelected, hitSlot);
                    }
                    else
                    {
                        _currentDragMode = DragMode.Playhead;
                        _currentPlayhead = Math.Clamp(clickedSec, 0.0, _totalDuration);
                        EmitSignal(SignalName.SeekRequested, _currentPlayhead);
                        QueueRedraw();
                    }
                }
                else
                {
                    if (_currentDragMode != DragMode.None && _draggedSlotIndex != -1)
                    {
                        EmitSignal(SignalName.SlotChanged, _draggedSlotIndex);
                    }
                    _currentDragMode = DragMode.None;
                    _draggedSlotIndex = -1;
                }
            }
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (_currentDragMode == DragMode.Playhead)
            {
                _currentPlayhead = Math.Clamp((mm.Position.X / size.X) * _totalDuration, 0.0, _totalDuration);
                EmitSignal(SignalName.SeekRequested, _currentPlayhead);
                QueueRedraw();
            }
            else if (_draggedSlotIndex >= 0 && _draggedSlotIndex < _slots.Count)
            {
                var slot = _slots[_draggedSlotIndex];
                var deltaSec = ((mm.Position.X - _dragStartMouseX) / size.X) * _totalDuration;

                if (_currentDragMode == DragMode.MoveBox)
                {
                    var dur = _dragOrigEndSec - _dragOrigStartSec;
                    var newStart = Math.Clamp(_dragOrigStartSec + deltaSec, 0.0, _totalDuration - dur);
                    slot.StartSeconds = Math.Round(newStart, 1);
                    slot.EndSeconds = Math.Round(newStart + dur, 1);
                }
                else if (_currentDragMode == DragMode.ResizeLeft)
                {
                    var newStart = Math.Clamp(_dragOrigStartSec + deltaSec, 0.0, slot.EndSeconds - 0.4);
                    slot.StartSeconds = Math.Round(newStart, 1);
                }
                else if (_currentDragMode == DragMode.ResizeRight)
                {
                    var newEnd = Math.Clamp(_dragOrigEndSec + deltaSec, slot.StartSeconds + 0.4, _totalDuration);
                    slot.EndSeconds = Math.Round(newEnd, 1);
                }

                QueueRedraw();
            }
        }
    }
}
