using Godot;

namespace DubbedUp.Godot.UI.Controls;

/// <summary>
/// Choicer-Voicer style visualizer that renders reference dialogue waveforms,
/// real-time user voice energy, and moving playhead with sync scoring.
/// </summary>
public partial class WaveformVisualizer : Control
{
    private const int SampleResolution = 150; // Number of horizontal energy bins

    private float[] _referenceSamples = new float[SampleResolution];
    private float[] _liveSamples = new float[SampleResolution];
    private double _durationSeconds = 4.0;
    private double _playheadSeconds = 0.0;
    private bool _isRecording = false;

    [Export]
    public Color ReferenceColor { get; set; } = new Color(0.2f, 0.6f, 0.9f, 0.45f);

    [Export]
    public Color LiveVoiceColor { get; set; } = new Color(0.2f, 1.0f, 0.5f, 0.95f);

    [Export]
    public Color PlayheadColor { get; set; } = new Color(1.0f, 0.9f, 0.2f, 1.0f);

    [Export]
    public Color BackgroundColor { get; set; } = new Color(0.08f, 0.10f, 0.14f, 0.9f);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(600, 100);
        GenerateSyntheticReferenceWave();
    }

    public void Reset(double durationSeconds, float[]? customReference = null)
    {
        _durationSeconds = Math.Max(0.5, durationSeconds);
        _playheadSeconds = 0.0;
        _isRecording = false;
        Array.Clear(_liveSamples, 0, _liveSamples.Length);

        if (customReference is not null && customReference.Length > 0)
        {
            _referenceSamples = (float[])customReference.Clone();
        }
        else
        {
            GenerateSyntheticReferenceWave();
        }

        QueueRedraw();
    }

    public void SetPlayhead(double currentSeconds, bool isRecording)
    {
        _playheadSeconds = Math.Clamp(currentSeconds, 0.0, _durationSeconds);
        _isRecording = isRecording;
        QueueRedraw();
    }

    public void AddLiveVoiceSample(double currentSeconds, float peakLevel0to100)
    {
        var normalizedTime = Math.Clamp(currentSeconds / _durationSeconds, 0.0, 1.0);
        var index = (int)(normalizedTime * (SampleResolution - 1));
        var normalizedAmp = Math.Clamp(peakLevel0to100 / 100.0f, 0.0f, 1.0f);

        if (index >= 0 && index < SampleResolution)
        {
            _liveSamples[index] = Math.Max(_liveSamples[index], normalizedAmp);
        }

        _playheadSeconds = currentSeconds;
        QueueRedraw();
    }

    public float CalculateSyncMatchPercentage()
    {
        float totalDiff = 0.0f;
        int activeBins = 0;

        for (int i = 0; i < SampleResolution; i++)
        {
            var refVal = _referenceSamples[i];
            var liveVal = _liveSamples[i];

            if (refVal > 0.1f || liveVal > 0.1f)
            {
                totalDiff += Math.Abs(refVal - liveVal);
                activeBins++;
            }
        }

        if (activeBins == 0) return 100.0f;

        var avgError = totalDiff / activeBins;
        var score = Math.Clamp((1.0f - (avgError * 0.7f)) * 100.0f, 20.0f, 98.0f);
        return (float)Math.Round(score);
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var height = rect.Size.Y;
        var width = rect.Size.X;
        var midY = height / 2.0f;

        // 1. Draw rounded background box
        DrawRect(rect, BackgroundColor, true, -1.0f);
        DrawRect(rect, new Color(0.25f, 0.35f, 0.5f, 0.6f), false, 1.5f);

        // 2. Draw subtle horizontal center line and time ticks
        DrawLine(new Vector2(0, midY), new Vector2(width, midY), new Color(0.3f, 0.4f, 0.5f, 0.3f), 1.0f);

        int secondCount = (int)Math.Ceiling(_durationSeconds);
        for (int s = 1; s <= secondCount; s++)
        {
            var x = (float)(s / _durationSeconds * width);
            if (x < width)
            {
                DrawLine(new Vector2(x, 0), new Vector2(x, height), new Color(0.3f, 0.4f, 0.5f, 0.25f), 1.0f);
                DrawString(ThemeDB.FallbackFont, new Vector2(x + 4, height - 6), $"{s}s", HorizontalAlignment.Left, -1, 10, new Color(0.6f, 0.7f, 0.8f, 0.6f));
            }
        }

        // 3. Draw Reference Waveform (Target Voice in Cyan/Blue)
        var binWidth = width / SampleResolution;
        for (int i = 0; i < SampleResolution; i++)
        {
            var amp = _referenceSamples[i];
            if (amp > 0.02f)
            {
                var barHeight = amp * (height * 0.42f);
                var x = i * binWidth;
                DrawRect(new Rect2(x, midY - barHeight, Math.Max(1.5f, binWidth - 0.5f), barHeight * 2), ReferenceColor);
            }
        }

        // 4. Draw Live Player Voice Waveform (Neon Green/Yellow)
        for (int i = 0; i < SampleResolution; i++)
        {
            var amp = _liveSamples[i];
            if (amp > 0.02f)
            {
                var barHeight = amp * (height * 0.45f);
                var x = i * binWidth;
                DrawRect(new Rect2(x, midY - barHeight, Math.Max(2.0f, binWidth - 0.2f), barHeight * 2), LiveVoiceColor);
            }
        }

        // 5. Draw Playhead Vertical Indicator
        var playheadX = (float)(_playheadSeconds / _durationSeconds * width);
        playheadX = Math.Clamp(playheadX, 0.0f, width);

        DrawLine(new Vector2(playheadX, 0), new Vector2(playheadX, height), PlayheadColor, 2.5f);

        // Playhead triangle cursor at top and bottom
        var topTriangle = new Vector2[]
        {
            new(playheadX - 6, 0),
            new(playheadX + 6, 0),
            new(playheadX, 8)
        };
        DrawColoredPolygon(topTriangle, PlayheadColor);

        // 6. Draw Match / Target Label
        DrawString(ThemeDB.FallbackFont, new Vector2(8, 16), "🎯 Orijinal Ses Eğrisi", HorizontalAlignment.Left, -1, 11, new Color(0.4f, 0.8f, 1.0f, 0.8f));
        if (_isRecording || _playheadSeconds > 0)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(8, 30), "🎙️ Senin Ses Dalgası", HorizontalAlignment.Left, -1, 11, new Color(0.3f, 1.0f, 0.5f, 0.9f));
        }
    }

    private void GenerateSyntheticReferenceWave()
    {
        var rand = new Random(42);
        for (int i = 0; i < SampleResolution; i++)
        {
            var t = (double)i / SampleResolution;
            // Realistic speech envelope with 2-3 spoken word bursts and pauses
            var burst1 = Math.Exp(-Math.Pow((t - 0.25) / 0.12, 2));
            var burst2 = Math.Exp(-Math.Pow((t - 0.65) / 0.15, 2));
            var noise = (rand.NextDouble() * 0.2) + 0.1;

            var amp = (burst1 * 0.85 + burst2 * 0.75) * noise;
            _referenceSamples[i] = (float)Math.Clamp(amp, 0.0, 1.0);
        }
    }
}

