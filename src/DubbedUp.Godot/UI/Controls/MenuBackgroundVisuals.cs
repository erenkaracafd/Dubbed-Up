using System;
using System.Collections.Generic;
using Godot;

namespace DubbedUp.Godot.UI.Controls;

/// <summary>
/// Soft animated ambient background shared by the menu screens.
/// Draws slowly orbiting aurora glows plus rounded shapes (dots, rings, capsules, squircles)
/// that float upward with a gentle sway, mouse parallax and beat pulsing.
/// </summary>
public partial class MenuBackgroundVisuals : Control
{
    private enum ShapeKind
    {
        Dot,
        Ring,
        Capsule,
        Squircle,
    }

    private struct Particle
    {
        public Vector2 Position;
        public float Speed;
        public float Size;
        public float Rotation;
        public float RotationSpeed;
        public float SwayAmplitude;
        public float SwayFrequency;
        public float SwayPhase;
        public float ParallaxFactor;
        public Color Color;
        public ShapeKind Kind;
        public StyleBoxFlat? Box;
    }

    private struct Glow
    {
        public Vector2 Anchor;
        public Vector2 Orbit;
        public float Speed;
        public float Phase;
        public float Radius;
        public Color Color;
    }

    private static readonly Color[] Palette =
    [
        new(1.00f, 0.56f, 0.72f), // blossom pink
        new(0.36f, 0.70f, 1.00f), // sky blue
        new(0.64f, 0.52f, 0.98f), // lilac
        new(0.42f, 0.86f, 0.80f), // mint
        new(1.00f, 0.74f, 0.52f), // peach
    ];

    private const int ParticleCount = 26;

    private readonly List<Particle> _particles = [];
    private readonly List<Glow> _glows = [];
    private readonly Random _rng = new();
    private GradientTexture2D? _softTexture;
    private Vector2 _mouseOffset = Vector2.Zero;
    private float _beatPulse;
    private float _time;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _softTexture = CreateSoftTexture();
        InitializeScene();
    }

    public void TriggerBeatPulse()
    {
        _beatPulse = 1.0f;
    }

    private static GradientTexture2D CreateSoftTexture()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, 1, 1, 1));
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradient.AddPoint(0.45f, new Color(1, 1, 1, 0.55f));

        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(0.5f, 0.0f),
            Width = 256,
            Height = 256,
        };
    }

    private void InitializeScene()
    {
        var size = GetViewportRect().Size;
        if (size == Vector2.Zero) size = new Vector2(1280, 720);

        _glows.Clear();
        var glowAnchors = new[] { new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.22f), new Vector2(0.78f, 0.86f), new Vector2(0.20f, 0.84f) };
        for (var i = 0; i < glowAnchors.Length; i++)
        {
            _glows.Add(new Glow
            {
                Anchor = glowAnchors[i],
                Orbit = new Vector2(RandRange(40, 90), RandRange(30, 70)),
                Speed = RandRange(0.05f, 0.11f),
                Phase = RandRange(0, Mathf.Tau),
                Radius = RandRange(300, 420),
                Color = Palette[i % Palette.Length] with { A = 0.30f },
            });
        }

        _particles.Clear();
        for (var i = 0; i < ParticleCount; i++)
        {
            _particles.Add(CreateParticle(new Vector2(RandRange(0, size.X), RandRange(0, size.Y))));
        }
    }

    private Particle CreateParticle(Vector2 position)
    {
        var kind = (ShapeKind)_rng.Next(4);
        var baseColor = Palette[_rng.Next(Palette.Length)];
        var particle = new Particle
        {
            Position = position,
            Speed = RandRange(8, 22),
            Size = kind switch
            {
                ShapeKind.Dot => RandRange(6, 16),
                ShapeKind.Ring => RandRange(18, 46),
                ShapeKind.Capsule => RandRange(26, 54),
                _ => RandRange(22, 44),
            },
            Rotation = RandRange(0, Mathf.Tau),
            RotationSpeed = RandRange(-0.25f, 0.25f),
            SwayAmplitude = RandRange(10, 34),
            SwayFrequency = RandRange(0.15f, 0.45f),
            SwayPhase = RandRange(0, Mathf.Tau),
            ParallaxFactor = RandRange(0.012f, 0.045f),
            Color = baseColor with { A = kind == ShapeKind.Dot ? 0.34f : 0.24f },
            Kind = kind,
        };

        if (kind is ShapeKind.Capsule or ShapeKind.Squircle)
        {
            var filled = kind == ShapeKind.Capsule && _rng.Next(2) == 0;
            var radius = kind == ShapeKind.Capsule ? (int)(particle.Size * 0.5f) : (int)(particle.Size * 0.38f);
            particle.Box = new StyleBoxFlat
            {
                BgColor = particle.Color with { A = 0.14f },
                DrawCenter = filled,
                BorderColor = particle.Color,
                BorderWidthLeft = filled ? 0 : 3,
                BorderWidthTop = filled ? 0 : 3,
                BorderWidthRight = filled ? 0 : 3,
                BorderWidthBottom = filled ? 0 : 3,
                CornerRadiusTopLeft = radius,
                CornerRadiusTopRight = radius,
                CornerRadiusBottomLeft = radius,
                CornerRadiusBottomRight = radius,
                CornerDetail = 12,
                AntiAliasing = true,
            };
        }

        return particle;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;

        var size = GetViewportRect().Size;
        if (size.X <= 0 || size.Y <= 0) return;

        var target = GetViewport().GetMousePosition() - size * 0.5f;
        _mouseOffset = _mouseOffset.Lerp(target, 1.0f - Mathf.Exp(-dt * 2.5f));
        _beatPulse = Mathf.Max(0, _beatPulse - dt * 2.2f);

        for (var i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            p.Position.Y -= p.Speed * dt;
            p.Rotation += p.RotationSpeed * dt;

            if (p.Position.Y < -80)
            {
                p = CreateParticle(new Vector2(RandRange(0, size.X), size.Y + 80));
            }

            _particles[i] = p;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = GetViewportRect().Size;

        // Soft vertical wash
        var top = new Color(0.925f, 0.950f, 0.992f);
        var bottom = new Color(0.965f, 0.940f, 0.990f);
        DrawPolygon(
            [Vector2.Zero, new Vector2(size.X, 0), size, new Vector2(0, size.Y)],
            [top, top, bottom, bottom]);

        if (_softTexture is null) return;

        // Pulse eases out so beats feel springy rather than snappy
        var pulse = 1.0f + 0.05f * EaseOutCubic(_beatPulse);

        foreach (var g in _glows)
        {
            var t = _time * g.Speed + g.Phase;
            var center = g.Anchor * size + new Vector2(Mathf.Sin(t) * g.Orbit.X, Mathf.Cos(t * 1.3f) * g.Orbit.Y) - _mouseOffset * 0.02f;
            var radius = g.Radius * pulse;
            DrawTextureRect(_softTexture, new Rect2(center - new Vector2(radius, radius), new Vector2(radius * 2, radius * 2)), false, g.Color);
        }

        foreach (var p in _particles)
        {
            var sway = Mathf.Sin(_time * p.SwayFrequency * Mathf.Tau + p.SwayPhase) * p.SwayAmplitude;
            var pos = new Vector2(p.Position.X + sway, p.Position.Y) - _mouseOffset * p.ParallaxFactor;
            var s = p.Size * pulse;

            switch (p.Kind)
            {
                case ShapeKind.Dot:
                    DrawTextureRect(_softTexture, new Rect2(pos - new Vector2(s * 2, s * 2), new Vector2(s * 4, s * 4)), false, p.Color with { A = p.Color.A * 0.6f });
                    DrawCircle(pos, s * 0.55f, p.Color, true, -1, true);
                    break;

                case ShapeKind.Ring:
                    DrawArc(pos, s, 0, Mathf.Tau, 48, p.Color, 3.0f, true);
                    break;

                case ShapeKind.Capsule when p.Box is not null:
                    DrawSetTransform(pos, p.Rotation, Vector2.One);
                    p.Box.Draw(GetCanvasItem(), new Rect2(-s, -s * 0.5f, s * 2, s));
                    DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                    break;

                case ShapeKind.Squircle when p.Box is not null:
                    DrawSetTransform(pos, p.Rotation, Vector2.One);
                    p.Box.Draw(GetCanvasItem(), new Rect2(-s * 0.5f, -s * 0.5f, s, s));
                    DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                    break;
            }
        }
    }

    private static float EaseOutCubic(float x) => 1 - MathF.Pow(1 - x, 3);

    private float RandRange(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
}
