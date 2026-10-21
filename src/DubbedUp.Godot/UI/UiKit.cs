using System;
using System.Collections.Generic;
using DubbedUp.Core.Scenes;
using DubbedUp.Godot.AudioPlayback;
using Godot;

namespace DubbedUp.Godot.UI;

/// <summary>
/// Shared building blocks for screens whose layout is built in code
/// (pill buttons, inputs, cards, chips, avatars and scene artwork).
/// </summary>
public static class UiKit
{
    public static readonly Color Ink = new(0.118f, 0.106f, 0.294f);
    public static readonly Color Muted = new(0.42f, 0.45f, 0.58f);
    public static readonly Color Line = new(0.84f, 0.87f, 0.94f);
    public static readonly Color Soft = new(0.96f, 0.97f, 1.0f);
    public static readonly Color Lilac = new(0.52f, 0.42f, 0.95f);
    public static readonly Color Pink = new(1.0f, 0.540f, 0.680f);
    public static readonly Color Sky = new(0.280f, 0.650f, 0.950f);
    public static readonly Color Mint = new(0.180f, 0.750f, 0.620f);
    public static readonly Color Danger = new(0.94f, 0.36f, 0.46f);

    public static readonly Color[] Palette =
    [
        new(1.0f, 0.540f, 0.680f),
        new(0.280f, 0.650f, 0.950f),
        new(0.620f, 0.480f, 0.960f),
        new(0.180f, 0.750f, 0.620f),
        new(0.980f, 0.640f, 0.250f),
        new(0.960f, 0.450f, 0.650f),
        new(0.420f, 0.500f, 0.950f),
        new(0.180f, 0.720f, 0.900f),
    ];

    private static readonly Dictionary<string, Texture2D?> ThumbnailCache = [];

    /// <summary>Spreads palette colors so neighbouring items never share a hue.</summary>
    public static Color PaletteColor(int index) => Palette[Math.Abs(index) * 3 % Palette.Length];

    public static StyleBoxFlat RoundedBox(Color color, int radius, int padding = 0) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        CornerDetail = 12,
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = padding,
        ContentMarginBottom = padding,
        AntiAliasing = true,
    };

    /// <summary>Frosted white card used for the main content blocks.</summary>
    public static StyleBoxFlat CardBox(int padding = 22, Color? border = null) => new()
    {
        BgColor = new Color(1, 1, 1, 0.92f),
        CornerRadiusTopLeft = 26,
        CornerRadiusTopRight = 26,
        CornerRadiusBottomLeft = 26,
        CornerRadiusBottomRight = 26,
        CornerDetail = 12,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        BorderColor = border ?? Line,
        ShadowColor = new Color(0.12f, 0.10f, 0.32f, 0.09f),
        ShadowSize = 22,
        ShadowOffset = new Vector2(0, 8),
        ContentMarginLeft = padding,
        ContentMarginRight = padding,
        ContentMarginTop = padding,
        ContentMarginBottom = padding,
        AntiAliasing = true,
    };

    public static Label Heading(string text, int size, Color? color = null)
    {
        var label = new Label { Text = text, ThemeTypeVariation = "HeadingLabel" };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        return label;
    }

    public static Label Body(string text, int size = 14, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Muted);
        return label;
    }

    public static Button CreateButton(string text, string? icon, bool outline, Color background, Color foreground, Action? onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 44),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        StylePill(button, background, foreground, outline);
        if (icon is not null) UiIcons.Apply(button, icon);
        if (onPressed is not null) button.Pressed += onPressed;
        UiSoundManager.Attach(button);
        AttachHoverBounce(button);
        return button;
    }

    /// <summary>Round icon-only button (e.g. preview / delete on list rows).</summary>
    public static Button CreateIconButton(string icon, Color background, Color foreground, Action? onPressed, int size = 36)
    {
        var button = new Button
        {
            Icon = UiIcons.Get(icon),
            IconAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };

        StyleBoxFlat Make(Color bg) => new()
        {
            BgColor = bg,
            CornerRadiusTopLeft = size / 2,
            CornerRadiusTopRight = size / 2,
            CornerRadiusBottomLeft = size / 2,
            CornerRadiusBottomRight = size / 2,
            CornerDetail = 16,
            AntiAliasing = true,
        };

        button.AddThemeStyleboxOverride("normal", Make(background));
        button.AddThemeStyleboxOverride("hover", Make(background.Darkened(0.06f)));
        button.AddThemeStyleboxOverride("pressed", Make(background.Darkened(0.12f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeConstantOverride("icon_max_width", (int)(size * 0.45f));
        foreach (var name in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_focus_color", "icon_hover_pressed_color" })
        {
            button.AddThemeColorOverride(name, foreground);
        }

        if (onPressed is not null) button.Pressed += onPressed;
        UiSoundManager.Attach(button);
        AttachHoverBounce(button);
        return button;
    }

    public static void StylePill(Button button, Color background, Color foreground, bool outline)
    {
        StyleBoxFlat Make(Color bg, int shadow, float borderAlpha) => new()
        {
            BgColor = bg,
            CornerRadiusTopLeft = 22,
            CornerRadiusTopRight = 22,
            CornerRadiusBottomLeft = 22,
            CornerRadiusBottomRight = 22,
            CornerDetail = 12,
            BorderWidthLeft = outline ? 2 : 0,
            BorderWidthTop = outline ? 2 : 0,
            BorderWidthRight = outline ? 2 : 0,
            BorderWidthBottom = outline ? 2 : 0,
            BorderColor = foreground with { A = borderAlpha },
            ShadowColor = outline ? new Color(0.12f, 0.10f, 0.32f, 0.06f) : background with { A = 0.35f },
            ShadowSize = shadow,
            ShadowOffset = new Vector2(0, shadow > 0 ? 3 : 0),
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            AntiAliasing = true,
        };

        button.AddThemeStyleboxOverride("normal", Make(background, outline ? 4 : 8, 0.18f));
        button.AddThemeStyleboxOverride("hover", Make(outline ? background.Darkened(0.03f) : background.Lightened(0.1f), outline ? 8 : 14, 0.4f));
        button.AddThemeStyleboxOverride("pressed", Make(background.Darkened(0.08f), 2, 0.4f));
        button.AddThemeStyleboxOverride("focus", Make(background, outline ? 4 : 8, 0.4f));
        button.AddThemeStyleboxOverride("disabled", Make(new Color(0.90f, 0.91f, 0.95f), 0, 0.1f));
        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
        {
            button.AddThemeColorOverride(name, foreground);
        }
        button.AddThemeColorOverride("font_disabled_color", Muted with { A = 0.7f });
        button.AddThemeColorOverride("icon_disabled_color", Muted with { A = 0.7f });
    }

    public static void AttachHoverBounce(Control control)
    {
        control.Resized += () => control.PivotOffset = control.Size * 0.5f;
        control.MouseEntered += () =>
        {
            if (control is BaseButton { Disabled: true }) return;
            control.CreateTween().TweenProperty(control, "scale", new Vector2(1.05f, 1.05f), 0.22f)
                   .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        };
        control.MouseExited += () =>
        {
            control.CreateTween().TweenProperty(control, "scale", Vector2.One, 0.18f)
                   .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        };
    }

    public static void StyleInput(LineEdit input, int height = 46)
    {
        StyleBoxFlat Make(Color border) => new()
        {
            BgColor = new Color(1, 1, 1, 0.95f),
            CornerRadiusTopLeft = height / 2,
            CornerRadiusTopRight = height / 2,
            CornerRadiusBottomLeft = height / 2,
            CornerRadiusBottomRight = height / 2,
            CornerDetail = 12,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = border,
            ContentMarginLeft = 20,
            ContentMarginRight = 16,
            AntiAliasing = true,
        };

        input.CustomMinimumSize = new Vector2(input.CustomMinimumSize.X, height);
        input.AddThemeStyleboxOverride("normal", Make(Line));
        input.AddThemeStyleboxOverride("focus", Make(new Color(0.38f, 0.66f, 1.0f)));
        input.AddThemeColorOverride("font_color", Ink);
        input.AddThemeColorOverride("font_placeholder_color", Muted with { A = 0.8f });
        input.AddThemeColorOverride("clear_button_color", Muted);
        input.AddThemeFontSizeOverride("font_size", 15);
    }

    public static void StyleScrollBar(ScrollBar bar)
    {
        StyleBoxFlat Grabber(float alpha) => new()
        {
            BgColor = Ink with { A = alpha },
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
        };

        bar.AddThemeStyleboxOverride("scroll", new StyleBoxEmpty { ContentMarginLeft = 4, ContentMarginRight = 4 });
        bar.AddThemeStyleboxOverride("grabber", Grabber(0.14f));
        bar.AddThemeStyleboxOverride("grabber_highlight", Grabber(0.26f));
        bar.AddThemeStyleboxOverride("grabber_pressed", Grabber(0.34f));
    }

    public static PanelContainer CreateChip(string text, Color background, Color? foreground = null, int fontSize = 12)
    {
        var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = RoundedBox(background, 11);
        box.ContentMarginLeft = 9;
        box.ContentMarginRight = 9;
        box.ContentMarginTop = 2;
        box.ContentMarginBottom = 3;
        chip.AddThemeStyleboxOverride("panel", box);
        var label = new Label { Text = text, ThemeTypeVariation = "HeadingLabel", MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", foreground ?? Colors.White);
        chip.AddChild(label);
        return chip;
    }

    /// <summary>Round badge with a person's (or character's) initial.</summary>
    public static PanelContainer CreateAvatar(string name, Color color, int size)
    {
        var avatar = new PanelContainer
        {
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var box = RoundedBox(color, size / 2);
        box.CornerDetail = 24;
        avatar.AddThemeStyleboxOverride("panel", box);

        var initial = Heading(string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant(), (int)(size * 0.46f), Colors.White);
        initial.HorizontalAlignment = HorizontalAlignment.Center;
        initial.VerticalAlignment = VerticalAlignment.Center;
        initial.MouseFilter = Control.MouseFilterEnum.Ignore;
        avatar.AddChild(initial);
        return avatar;
    }

    /// <summary>
    /// Returns the scene thumbnail, or null when it is missing or flat placeholder art
    /// (official scenes ship single-color thumbnails that look dull as artwork).
    /// </summary>
    public static Texture2D? GetSceneThumbnail(ScenePackage package)
    {
        var key = package.PackageDirectory ?? package.Title;
        if (ThumbnailCache.TryGetValue(key, out var cached)) return cached;

        var texture = VideoPlayback.VideoThumbnailHelper.GetOrExtractThumbnail(package.PackageDirectory ?? string.Empty);
        if (texture is not null && IsFlatImage(texture)) texture = null;

        ThumbnailCache[key] = texture;
        return texture;
    }

    /// <summary>Fills a (clipping) holder with the scene thumbnail or a gradient with its initial.</summary>
    public static void FillSceneArt(Control holder, ScenePackage package, Color accent, int initialSize)
    {
        var texture = GetSceneThumbnail(package);
        if (texture is not null)
        {
            var image = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            holder.AddChild(image);
            return;
        }

        accent.ToHsv(out var hue, out var sat, out var val);
        var gradient = new Gradient();
        gradient.SetColor(0, Color.FromHsv(hue - 0.04f, sat * 0.75f, Math.Min(1f, val + 0.08f)));
        gradient.SetColor(1, Color.FromHsv(hue + 0.07f, Math.Min(1f, sat * 1.1f), val * 0.92f));
        var fill = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = gradient, FillFrom = Vector2.Zero, FillTo = Vector2.One, Width = 64, Height = 64 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(fill);

        var initial = Heading(package.Title.Length > 0 ? package.Title[..1].ToUpperInvariant() : "?", initialSize, new Color(1, 1, 1, 0.85f));
        initial.HorizontalAlignment = HorizontalAlignment.Center;
        initial.VerticalAlignment = VerticalAlignment.Center;
        initial.MouseFilter = Control.MouseFilterEnum.Ignore;
        initial.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(initial);
    }

    public static Color ExtractDominantColor(Texture2D texture, Color fallback)
    {
        try
        {
            var image = texture.GetImage();
            if (image is not null && !image.IsEmpty())
            {
                var width = image.GetWidth();
                var height = image.GetHeight();
                var bestScore = -1f;
                var bestColor = Colors.Transparent;
                var stepX = Math.Max(1, width / 14);
                var stepY = Math.Max(1, height / 10);

                for (var y = stepY / 2; y < height; y += stepY)
                {
                    for (var x = stepX / 2; x < width; x += stepX)
                    {
                        var p = image.GetPixel(x, y);
                        if (p.A < 0.5f) continue;

                        var lum = 0.299f * p.R + 0.587f * p.G + 0.114f * p.B;
                        if (lum < 0.12f || lum > 0.90f) continue; // skip near-black and glare

                        var max = Math.Max(p.R, Math.Max(p.G, p.B));
                        var min = Math.Min(p.R, Math.Min(p.G, p.B));
                        var sat = max > 0 ? (max - min) / max : 0;
                        var score = sat * (1.0f - Math.Abs(lum - 0.5f));
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestColor = p;
                        }
                    }
                }

                if (bestScore > 0.06f)
                {
                    bestColor.ToHsv(out var h, out var s, out var v);
                    return Color.FromHsv(h, Math.Clamp(s * 1.1f, 0.45f, 0.85f), Math.Clamp(v, 0.72f, 0.92f));
                }
            }
        }
        catch
        {
            // Fall back to the provided color below.
        }

        return fallback;
    }

    private static bool IsFlatImage(Texture2D texture)
    {
        try
        {
            var image = texture.GetImage();
            if (image is null || image.IsEmpty()) return true;

            var first = image.GetPixel(0, 0);
            var stepX = Math.Max(1, image.GetWidth() / 12);
            var stepY = Math.Max(1, image.GetHeight() / 8);
            for (var y = 0; y < image.GetHeight(); y += stepY)
            {
                for (var x = 0; x < image.GetWidth(); x += stepX)
                {
                    var p = image.GetPixel(x, y);
                    if (Math.Abs(p.R - first.R) + Math.Abs(p.G - first.G) + Math.Abs(p.B - first.B) > 0.08f) return false;
                }
            }
        }
        catch
        {
            return false;
        }

        return true;
    }
}
