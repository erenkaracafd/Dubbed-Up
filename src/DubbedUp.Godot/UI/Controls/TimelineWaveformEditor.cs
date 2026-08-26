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
        new Color(1.0f, 0.65f, 0.2f, 0.38f),  // Amber
        new Color(0.3f, 0.8f, 1.0f, 0.38f),   // Cyan
        new Color(0.6f, 1.0f, 0.4f, 0.38f),   // Green
        new Color(1.0f, 0.4f, 0.7f, 0.38f),   // Pink
        new Color(0.8f, 0.5f, 1.0f, 0.38f),   // Purple
    ];

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(820, 110);
        MouseFilter = MouseFilterEnum.Stop;
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

        _waveformPoints = waveform;
        QueueRedraw();
    }

    public void SetWaveform(float[]? waveform)
    {
        _waveformPoints = waveform;
        QueueRedraw();
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

    public override void _Draw()
    {
        var size = Size;
        if (size.X <= 0 || size.Y <= 0 || _totalDuration <= 0) return;

        // 1. Background
        var bgRect = new Rect2(Vector2.Zero, size);
        DrawRect(bgRect, new Color(0.08f, 0.10f, 0.14f, 1.0f));
        DrawRect(bgRect, new Color(0.25f, 0.30f, 0.40f, 0.8f), false, 1.5f);

        // 2. Time Grid & Ruler Marks (every 1s / 5s)
        var stepSec = _totalDuration > 60.0 ? 5.0 : (_totalDuration > 20.0 ? 2.0 : 1.0);
        var font = ThemeDB.FallbackFont;
        var fontSize = 11;

        for (double t = 0; t <= _totalDuration; t += stepSec)
        {
            var x = (float)((t / _totalDuration) * size.X);
            var isMajor = (int)t % 5 == 0;
            var lineColor = isMajor ? new Color(0.4f, 0.45f, 0.55f, 0.6f) : new Color(0.2f, 0.25f, 0.35f, 0.3f);
            var lineH = isMajor ? size.Y : size.Y * 0.4f;

            DrawLine(new Vector2(x, 0), new Vector2(x, lineH), lineColor, 1.0f);

            if (isMajor && x < size.X - 30)
            {
                DrawString(font, new Vector2(x + 3, 14), $"{t:F0}s", HorizontalAlignment.Left, -1, fontSize, new Color(0.7f, 0.75f, 0.85f, 0.8f));
            }
        }

        // 3. Audio Waveform Center Line & Amplitude Bars
        var centerY = size.Y * 0.58f;
        var maxAmpH = size.Y * 0.32f;

        DrawLine(new Vector2(0, centerY), new Vector2(size.X, centerY), new Color(0.25f, 0.35f, 0.5f, 0.4f), 1.0f);

        if (_waveformPoints is not null && _waveformPoints.Length > 0)
        {
            var count = _waveformPoints.Length;
            var barWidth = Math.Max(2.0f, size.X / count);

            for (int i = 0; i < count; i++)
            {
                var x = (float)i / count * size.X;
                var amp = Math.Clamp(_waveformPoints[i], 0.0f, 1.0f) * maxAmpH;
                if (amp > 1.0f)
                {
                    DrawLine(new Vector2(x, centerY - amp), new Vector2(x, centerY + amp), new Color(0.2f, 0.75f, 1.0f, 0.65f), barWidth * 0.8f);
                }
            }
        }

        // 4. Transparent Speech Selection Boxes
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var xStart = (float)(Math.Clamp(slot.StartSeconds / _totalDuration, 0.0, 1.0) * size.X);
            var xEnd = (float)(Math.Clamp(slot.EndSeconds / _totalDuration, 0.0, 1.0) * size.X);
            var width = Math.Max(12.0f, xEnd - xStart);

            var isSelected = (i == _selectedSlotIndex);
            var boxColor = isSelected ? new Color(0.2f, 0.9f, 0.4f, 0.48f) : slot.BoxColor;
            var borderColor = isSelected ? new Color(0.4f, 1.0f, 0.6f, 1.0f) : new Color(slot.BoxColor.R, slot.BoxColor.G, slot.BoxColor.B, 0.95f);

            var slotRect = new Rect2(xStart, 18, width, size.Y - 22);

            // Fill transparent box
            DrawRect(slotRect, boxColor);
            // Border
            DrawRect(slotRect, borderColor, false, isSelected ? 2.5f : 1.5f);

            // Edge resize handle indicators
            DrawRect(new Rect2(xStart, 18, 4, size.Y - 22), borderColor);
            DrawRect(new Rect2(xStart + width - 4, 18, 4, size.Y - 22), borderColor);

            // Header Tag (Number & Character Name)
            var tagText = $"#{i + 1} {slot.CharacterName}";
            var tagBgRect = new Rect2(xStart, 18, Math.Min(width, 140), 16);
            DrawRect(tagBgRect, new Color(0.0f, 0.0f, 0.0f, 0.75f));
            DrawString(font, new Vector2(xStart + 3, 30), tagText, HorizontalAlignment.Left, (int)width - 6, 10, new Color(1.0f, 1.0f, 1.0f, 0.95f));
        }

        // 5. Playhead (Zaman Çubuğu)
        var playheadX = (float)((_currentPlayhead / _totalDuration) * size.X);
        var playheadColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);

        // Vertical Line
        DrawLine(new Vector2(playheadX, 0), new Vector2(playheadX, size.Y), playheadColor, 2.0f);

        // Top triangle head
        var points = new Vector2[]
        {
            new(playheadX - 6, 0),
            new(playheadX + 6, 0),
            new(playheadX, 10)
        };
        DrawColoredPolygon(points, playheadColor);
    }

    public override void _GuiInput(InputEvent @event)
    {
        var size = Size;
        if (size.X <= 0 || _totalDuration <= 0) return;

        if (@event is InputEventMouseButton mb)
        {
            var mouseX = mb.Position.X;
            var clickedSec = (mouseX / size.X) * _totalDuration;

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    // Check if clicked on slot box or resize edges
                    var hitSlot = -1;
                    var dragMode = DragMode.Playhead;

                    for (int i = _slots.Count - 1; i >= 0; i--)
                    {
                        var s = _slots[i];
                        var x1 = (s.StartSeconds / _totalDuration) * size.X;
                        var x2 = (s.EndSeconds / _totalDuration) * size.X;

                        if (mouseX >= x1 - 5 && mouseX <= x2 + 5)
                        {
                            hitSlot = i;
                            if (Math.Abs(mouseX - x1) <= 7) dragMode = DragMode.ResizeLeft;
                            else if (Math.Abs(mouseX - x2) <= 7) dragMode = DragMode.ResizeRight;
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
                        // Clicked timeline background -> Seek playhead
                        _currentDragMode = DragMode.Playhead;
                        _currentPlayhead = Math.Clamp(clickedSec, 0.0, _totalDuration);
                        EmitSignal(SignalName.SeekRequested, _currentPlayhead);
                        QueueRedraw();
                    }
                }
                else
                {
                    // Released
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
