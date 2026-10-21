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

    [Signal]
    public delegate void SlotSplitRequestedEventHandler(int slotIndex, double splitSeconds);

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
    private enum DragMode { None, Playhead, MoveBox, ResizeLeft, ResizeRight, SelectOnly }
    private DragMode _currentDragMode = DragMode.None;
    private int _draggedSlotIndex = -1;
    private double _dragStartMouseX = 0.0;
    private double _dragOrigStartSec = 0.0;
    private double _dragOrigEndSec = 0.0;

    public static readonly Color[] PresetColors =
    [
        new Color(0.20f, 0.60f, 0.95f),  // Sky Blue
        new Color(0.95f, 0.40f, 0.30f),  // Coral Red
        new Color(0.15f, 0.75f, 0.45f),  // Emerald Green
        new Color(0.95f, 0.65f, 0.15f),  // Amber Orange
        new Color(0.65f, 0.35f, 0.95f),  // Violet Purple
        new Color(0.95f, 0.30f, 0.60f),  // Pink Rose
        new Color(0.05f, 0.75f, 0.80f),  // Teal Cyan
        new Color(0.85f, 0.45f, 0.10f),  // Rust Orange
        new Color(0.40f, 0.50f, 0.95f),  // Indigo
        new Color(0.50f, 0.78f, 0.20f),  // Apple Green
        new Color(0.85f, 0.20f, 0.40f),  // Ruby
        new Color(0.15f, 0.70f, 0.65f),  // Deep Mint
    ];

    public static Color GetPresetColor(int index) => PresetColors[Math.Abs(index) % PresetColors.Length];

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(400, 115);
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
            s.BoxColor = GetPresetColor(i);
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
        _selectedSlotIndex = (index >= 0 && index < _slots.Count) ? index : -1;
        QueueRedraw();
    }

    public int GetSelectedSlotIndex() => _selectedSlotIndex;

    public TimelineSlotData? GetSlot(int index)
    {
        if (index >= 0 && index < _slots.Count) return _slots[index];
        return null;
    }

    public IReadOnlyList<TimelineSlotData> GetSlots() => _slots;

    public override void _Draw()
    {
        var size = Size;
        if (size.X <= 0 || size.Y <= 0 || _totalDuration <= 0) return;

        // 1. Rounded light track
        var track = new StyleBoxFlat
        {
            BgColor = TrackColor,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            CornerDetail = 8,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = TrackBorderColor,
            AntiAliasing = true,
        };
        DrawStyleBox(track, new Rect2(Vector2.Zero, size));

        // 2. Time Grid & Ruler Marks
        var font = GetThemeDefaultFont() ?? ThemeDB.FallbackFont;
        var fontSize = 11;
        var stepSec = _totalDuration > 60.0 ? 5.0 : (_totalDuration > 20.0 ? 2.0 : 1.0);

        for (double t = stepSec; t <= _totalDuration; t += stepSec)
        {
            var x = (float)((t / _totalDuration) * size.X);
            var isMajor = ((int)Math.Round(t)) % 5 == 0;
            var lineH = isMajor ? size.Y - 4 : 12.0f;

            DrawLine(new Vector2(x, 4), new Vector2(x, lineH), isMajor ? GridMajorColor : GridMinorColor, 1.0f);

            if (isMajor && x < size.X - 35)
            {
                DrawString(font, new Vector2(x + 4, 13), $"{t:F0}s", HorizontalAlignment.Left, -1, fontSize, RulerTextColor);
            }
        }

        // 3. Audio Waveform Track
        var centerY = size.Y * 0.56f;
        var maxAmpH = size.Y * 0.36f;

        DrawLine(new Vector2(8, centerY), new Vector2(size.X - 8, centerY), GridMinorColor, 1.0f);

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
                    ? WaveColor
                    : WaveColor with { A = 0.18f };

                DrawLine(new Vector2(x, centerY - amp), new Vector2(x, centerY + amp), barColor, barWidth);
            }
        }
        else
        {
            for (float x = 0; x < size.X; x += 4)
            {
                var idleAmp = (float)(Math.Sin(x * 0.05) * 3.0);
                DrawLine(new Vector2(x, centerY - idleAmp), new Vector2(x, centerY + idleAmp), WaveColor with { A = 0.18f }, 2.0f);
            }
        }

        // 4. Vibrant Speech Selection Boxes (Overlaid on Waveform)
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var xStart = (float)(Math.Clamp(slot.StartSeconds / _totalDuration, 0.0, 1.0) * size.X);
            var xEnd = (float)(Math.Clamp(slot.EndSeconds / _totalDuration, 0.0, 1.0) * size.X);
            var width = Math.Max(16.0f, xEnd - xStart);
            var yTop = 16.0f;
            var boxHeight = size.Y - 18.0f;
            var yBottom = yTop + boxHeight;

            var isSelected = (i == _selectedSlotIndex);
            var baseColor = slot.BoxColor;
            var slotRect = new Rect2(xStart, yTop, width, boxHeight);

            // Rounded translucent box; the selected one is filled stronger with a thicker outline
            var box = new StyleBoxFlat
            {
                BgColor = baseColor with { A = isSelected ? 0.30f : 0.16f },
                BorderColor = baseColor with { A = isSelected ? 1.0f : 0.7f },
                BorderWidthLeft = isSelected ? 3 : 2,
                BorderWidthTop = isSelected ? 3 : 2,
                BorderWidthRight = isSelected ? 3 : 2,
                BorderWidthBottom = isSelected ? 3 : 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                AntiAliasing = true,
            };
            DrawStyleBox(box, slotRect);

            // Resize handles: short rounded bars centred on both edges
            var handleColor = baseColor.Darkened(isSelected ? 0.15f : 0.0f);
            var handleTop = yTop + boxHeight * 0.32f;
            var handleBottom = yTop + boxHeight * 0.68f;
            DrawLine(new Vector2(xStart + 4, handleTop), new Vector2(xStart + 4, handleBottom), handleColor, 3.0f, true);
            DrawLine(new Vector2(xEnd - 4, handleTop), new Vector2(xEnd - 4, handleBottom), handleColor, 3.0f, true);

            // Center Grip Dot for moving (Only moving happens by dragging this dot!)
            var midX = xStart + (width / 2.0f);
            var midY = yTop + (boxHeight / 2.0f);
            var dotRadius = isSelected ? 6.5f : 5.0f;
            DrawCircle(new Vector2(midX, midY), dotRadius + 2.0f, Colors.White, true, -1, true);
            DrawCircle(new Vector2(midX, midY), dotRadius, baseColor, true, -1, true);

            // Header Tag (Number & Character Name) as a rounded pill
            var tagText = $"#{i + 1} {slot.CharacterName}";
            var tagWidth = Math.Min(width, 160);
            var tagBgRect = new Rect2(xStart, yTop, tagWidth, 17);
            var tag = new StyleBoxFlat
            {
                BgColor = baseColor with { A = 0.95f },
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 0,
                CornerRadiusBottomRight = 6,
                AntiAliasing = true,
            };
            DrawStyleBox(tag, tagBgRect);
            DrawString(font, new Vector2(xStart + 6, yTop + 13), tagText, HorizontalAlignment.Left, (int)tagWidth - 24, 11, Colors.White);

            // Delete button (round, drawn X) at top right of the box if width allows
            if (width >= 35)
            {
                var center = new Vector2(xStart + width - 8, yTop + 8.5f);
                DrawCircle(center, 7.0f, DeleteColor, true, -1, true);
                DrawLine(center + new Vector2(-3, -3), center + new Vector2(3, 3), Colors.White, 1.6f, true);
                DrawLine(center + new Vector2(3, -3), center + new Vector2(-3, 3), Colors.White, 1.6f, true);
            }
        }

        // 5. Playhead with a round knob
        var playheadX = (float)((_currentPlayhead / _totalDuration) * size.X);
        DrawLine(new Vector2(playheadX, 4), new Vector2(playheadX, size.Y - 4), PlayheadColor, 2.0f, true);
        DrawCircle(new Vector2(playheadX, 6), 6.5f, Colors.White, true, -1, true);
        DrawCircle(new Vector2(playheadX, 6), 5.0f, PlayheadColor, true, -1, true);
    }

    private static readonly Color TrackColor = new(0.975f, 0.98f, 1.0f);
    private static readonly Color TrackBorderColor = new(0.84f, 0.87f, 0.94f);
    private static readonly Color GridMinorColor = new(0.118f, 0.106f, 0.294f, 0.07f);
    private static readonly Color GridMajorColor = new(0.118f, 0.106f, 0.294f, 0.15f);
    private static readonly Color RulerTextColor = new(0.42f, 0.45f, 0.58f);
    private static readonly Color WaveColor = new(0.52f, 0.42f, 0.95f, 0.55f);
    private static readonly Color PlayheadColor = new(1.0f, 0.36f, 0.56f);
    private static readonly Color DeleteColor = new(0.94f, 0.36f, 0.46f);

    public bool RequestSplitAtPlayhead()
    {
        if (_selectedSlotIndex >= 0 && _selectedSlotIndex < _slots.Count)
        {
            var slot = _slots[_selectedSlotIndex];
            if (_currentPlayhead > slot.StartSeconds + 0.3 && _currentPlayhead < slot.EndSeconds - 0.3)
            {
                EmitSignal(SignalName.SlotSplitRequested, _selectedSlotIndex, _currentPlayhead);
                return true;
            }
        }
        return false;
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
            else if (ek.Keycode == Key.C || ek.Keycode == Key.X)
            {
                RequestSplitAtPlayhead();
                return;
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

                    // Check if clicked on slot box, delete icon, center dot, or resize edges
                    var hitSlot = -1;
                    var dragMode = DragMode.Playhead;

                    for (int i = _slots.Count - 1; i >= 0; i--)
                    {
                        var s = _slots[i];
                        var x1 = (s.StartSeconds / _totalDuration) * size.X;
                        var x2 = (s.EndSeconds / _totalDuration) * size.X;
                        var boxW = x2 - x1;
                        var midX = x1 + (boxW / 2.0f);
                        var midY = 16.0f + (size.Y - 18.0f) / 2.0f;

                        if (mouseX >= x1 - 6 && mouseX <= x2 + 6)
                        {
                            // Check delete icon hit (top-right 16x16 area)
                            if (mouseY >= 16 && mouseY <= 32 && mouseX >= x2 - 18 && mouseX <= x2 + 2)
                            {
                                EmitSignal(SignalName.SlotDeleteRequested, i);
                                return;
                            }

                            hitSlot = i;
                            // Check resize handles (within 8px of edges)
                            if (Math.Abs(mouseX - x1) <= 8)
                            {
                                dragMode = DragMode.ResizeLeft;
                            }
                            else if (Math.Abs(mouseX - x2) <= 8)
                            {
                                dragMode = DragMode.ResizeRight;
                            }
                            else
                            {
                                // Check distance to center dot (within 14px radius allows moving!)
                                var distToCenter = Math.Sqrt(Math.Pow(mouseX - midX, 2) + Math.Pow(mouseY - midY, 2));
                                if (distToCenter <= 14.0)
                                {
                                    dragMode = DragMode.MoveBox;
                                }
                                else
                                {
                                    // Clicking inside body selects slot and seeks without moving!
                                    dragMode = DragMode.SelectOnly;
                                }
                            }
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

                        if (dragMode == DragMode.SelectOnly)
                        {
                            _currentPlayhead = Math.Clamp(clickedSec, 0.0, _totalDuration);
                            EmitSignal(SignalName.SeekRequested, _currentPlayhead);
                        }
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
                    if (_currentDragMode is DragMode.MoveBox or DragMode.ResizeLeft or DragMode.ResizeRight && _draggedSlotIndex != -1)
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
