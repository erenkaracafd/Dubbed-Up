using System.Collections.Generic;
using Godot;

namespace DubbedUp.Godot.UI;

/// <summary>
/// Loads the bundled Lucide SVG icons (Content/Icons) and attaches them to buttons.
/// The SVGs are white so they can be tinted to match each button's text color.
/// </summary>
public static class UiIcons
{
    private const string IconDirectory = "res://Content/Icons/";
    private const float IconSidePadding = 20f;
    private static readonly string[] StyleStates = ["normal", "hover", "pressed", "focus", "disabled"];
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    public static Texture2D? Get(string iconName)
    {
        if (!Cache.TryGetValue(iconName, out var texture))
        {
            texture = ResourceLoader.Load<Texture2D>($"{IconDirectory}{iconName}.svg");
            Cache[iconName] = texture;
        }

        return texture;
    }

    /// <summary>
    /// Sets the icon and tints it with the button's current font colors.
    /// Call after the button's styling overrides have been applied.
    /// </summary>
    public static void Apply(Button? button, string iconName)
    {
        if (button is null)
        {
            return;
        }

        button.Icon = Get(iconName);

        // Godot can't center an icon+text group, so keep them together on the left
        // and give pill styles (which have no content margin) room to breathe.
        button.Alignment = HorizontalAlignment.Left;
        foreach (var state in StyleStates)
        {
            var box = button.GetThemeStylebox(state);
            if (box is not null && box.ContentMarginLeft < IconSidePadding * 0.5f)
            {
                box.ContentMarginLeft = IconSidePadding;
                box.ContentMarginRight = Mathf.Max(box.ContentMarginRight, IconSidePadding);
            }
        }

        button.AddThemeColorOverride("icon_normal_color", button.GetThemeColor("font_color"));
        button.AddThemeColorOverride("icon_hover_color", button.GetThemeColor("font_hover_color"));
        button.AddThemeColorOverride("icon_pressed_color", button.GetThemeColor("font_pressed_color"));
        button.AddThemeColorOverride("icon_focus_color", button.GetThemeColor("font_focus_color"));
        button.AddThemeColorOverride("icon_hover_pressed_color", button.GetThemeColor("font_hover_pressed_color"));
    }

    /// <summary>Swaps the icon texture only, keeping the tint set by <see cref="Apply"/>.</summary>
    public static void Swap(Button? button, string iconName)
    {
        if (button is not null)
        {
            button.Icon = Get(iconName);
        }
    }
}
