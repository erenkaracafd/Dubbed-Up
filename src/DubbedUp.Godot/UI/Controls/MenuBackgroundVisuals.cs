using System;
using System.Collections.Generic;
using Godot;

namespace DubbedUp.Godot.UI.Controls;

/// <summary>
/// osu!-inspired dynamic floating ambient background layer.
/// Renders smooth pastel geometric shapes that drift upward with subtle mouse parallax and beat pulsing.
/// </summary>
public partial class MenuBackgroundVisuals : Control
{
    private struct Particle
    {
        public Vector2 Position;
        public float Speed;
        public float Radius;
        public float Rotation;
        public float RotationSpeed;
        public Color Color;
        public int ShapeType; // 0 = Filled Circle, 1 = Hollow Ring, 2 = Rounded Square
        public float ParallaxFactor;
    }

    private readonly List<Particle> _particles = [];
    private Vector2 _targetMouseOffset = Vector2.Zero;
    private Vector2 _currentMouseOffset = Vector2.Zero;
    private float _beatPulseScale = 1.0f;
    private const int ParticleCount = 28;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        InitializeParticles();
    }

    private void InitializeParticles()
    {
        _particles.Clear();
        var rng = new Random();
        var viewportSize = GetViewportRect().Size;
        if (viewportSize == Vector2.Zero) viewportSize = new Vector2(1280, 720);

        var colors = new[]
        {
            new Color(1.0f, 0.580f, 0.720f, 0.16f), // Soft Blossom Pink
            new Color(0.350f, 0.720f, 1.000f, 0.16f), // Soft Ice Sky Blue
            new Color(0.650f, 0.520f, 0.980f, 0.14f), // Gentle Lilac
            new Color(1.0f, 0.680f, 0.800f, 0.12f), // Cotton Candy Pink
            new Color(0.480f, 0.840f, 0.980f, 0.15f)  // Soft Aqua
        };

        for (var i = 0; i < ParticleCount; i++)
        {
            _particles.Add(new Particle
            {
                Position = new Vector2(
                    (float)rng.NextDouble() * viewportSize.X,
                    (float)rng.NextDouble() * viewportSize.Y
                ),
                Speed = (float)(14.0 + rng.NextDouble() * 26.0),
                Radius = (float)(18.0 + rng.NextDouble() * 55.0),
                Rotation = (float)(rng.NextDouble() * Math.PI * 2),
                RotationSpeed = (float)((rng.NextDouble() - 0.5) * 0.4),
                Color = colors[rng.Next(colors.Length)],
                ShapeType = rng.Next(3),
                ParallaxFactor = (float)(0.015 + rng.NextDouble() * 0.035)
            });
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var viewportSize = GetViewportRect().Size;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;

        // Smooth mouse parallax
        var mousePos = GetViewport().GetMousePosition();
        var center = viewportSize * 0.5f;
        _targetMouseOffset = (mousePos - center);
        _currentMouseOffset = _currentMouseOffset.Lerp(_targetMouseOffset, dt * 3.0f);

        // Decay beat pulse
        if (_beatPulseScale > 1.0f)
        {
            _beatPulseScale = Mathf.MoveToward(_beatPulseScale, 1.0f, dt * 1.5f);
        }

        for (var i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            p.Position.Y -= p.Speed * dt;
            p.Rotation += p.RotationSpeed * dt;

            // Wrap around bottom
            if (p.Position.Y < -p.Radius * 2)
            {
                p.Position.Y = viewportSize.Y + p.Radius * 2;
                p.Position.X = (float)GD.RandRange(0, viewportSize.X);
            }

            _particles[i] = p;
        }

        QueueRedraw();
    }

    public void TriggerBeatPulse()
    {
        _beatPulseScale = 1.08f;
    }

    public override void _Draw()
    {
        var viewportSize = GetViewportRect().Size;

        // Soft Ice Sky Tint Background (cooler, soothing light sky tone)
        DrawRect(new Rect2(Vector2.Zero, viewportSize), new Color(0.925f, 0.955f, 0.988f, 1.0f));

        foreach (var p in _particles)
        {
            var drawPos = p.Position - (_currentMouseOffset * p.ParallaxFactor);
            var currentRadius = p.Radius * _beatPulseScale;

            switch (p.ShapeType)
            {
                case 0: // Filled Circle
                    DrawCircle(drawPos, currentRadius, p.Color);
                    break;

                case 1: // Hollow Ring
                    DrawArc(drawPos, currentRadius, 0, Mathf.Tau, 32, p.Color, 3.0f, true);
                    break;

                case 2: // Diamond / Rounded square
                    var rect = new Rect2(drawPos - new Vector2(currentRadius * 0.7f, currentRadius * 0.7f), new Vector2(currentRadius * 1.4f, currentRadius * 1.4f));
                    DrawSetTransform(drawPos, p.Rotation, Vector2.One);
                    DrawRect(new Rect2(-currentRadius * 0.7f, -currentRadius * 0.7f, currentRadius * 1.4f, currentRadius * 1.4f), p.Color, false, 2.5f);
                    DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                    break;
            }
        }
    }
}

