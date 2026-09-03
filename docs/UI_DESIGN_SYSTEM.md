# Dubbed Up — UI Design System Guide

> **Visual Identity**: *osu!-Inspired Pop Arcade*  
> **Theme Mode**: Light Theme (Porcelain Canvas, Vibrant Hot Pink & Sky Blue Accents)  
> **Mood**: Joyful, energetic, rhythmic, tactile, and party-ready.

This document defines the visual design language, color tokens, typography, UI components, motion principles, and audio UX for the entire Dubbed Up user interface. All new screens, controls, and visual updates must conform to these rules to ensure cohesive aesthetic quality across the game.

---

## 1. Color Palette & Design Tokens

The interface uses a clean, bright, and cheerful light aesthetic. Surfaces are warm white/porcelain, high-contrast dark text ensures effortless readability, and accents pop with iconic hot pinks and fresh sky blues.

### Core Canvas & Surfaces
| Token Name | Hex Code | Godot Color | Purpose / Usage |
| :--- | :--- | :--- | :--- |
| `CanvasBackground` | `#F8F9FD` | `Color(0.973, 0.976, 0.992, 1.0)` | Base application background for all screens. |
| `SurfaceCard` | `#FFFFFF` | `Color(1.0, 1.0, 1.0, 1.0)` | Elevated containers, cards, modals, and content boxes. |
| `SurfaceMuted` | `#EEF1F8` | `Color(0.933, 0.945, 0.973, 1.0)` | Secondary panels, recessed areas, timeline backgrounds. |
| `BorderSubtle` | `#E2E6F0` | `Color(0.886, 0.902, 0.941, 1.0)` | Card outlines, dividers, inactive slot boundaries. |

### Brand & Action Accents
| Token Name | Hex Code | Godot Color | Purpose / Usage |
| :--- | :--- | :--- | :--- |
| **Hot Pink (Primary)** | `#FF3E83` | `Color(1.0, 0.243, 0.514, 1.0)` | Primary call-to-action ("Play Party", record button, hero pulse). |
| **Sakura Pink (Light)** | `#FF66AA` | `Color(1.0, 0.400, 0.667, 1.0)` | Hover states, glowing rings, playful badge tags. |
| **Sky Blue (Secondary)** | `#38B6FF` | `Color(0.220, 0.714, 1.0, 1.0)` | Online Multiplayer buttons, network connectivity, guest badges. |
| **Pastel Cyan (Light)** | `#70D6FF` | `Color(0.439, 0.839, 1.0, 1.0)` | Secondary button hover, active slider tracks, playhead indicators. |
| **Pastel Violet (Bridge)**| `#8F65F8` | `Color(0.561, 0.396, 0.973, 1.0)` | Scene Studio/Editor button, custom scene tags, category badges. |
| **Mint Ice (Success)** | `#00C48C` | `Color(0.0, 0.769, 0.549, 1.0)` | Ready state ("Ready!"), mic detection active, audio export done. |
| **Coral Sunset (Warning)**| `#FF6B6B` | `Color(1.0, 0.420, 0.420, 1.0)` | Delete confirmation, disconnect alerts, mic clipping warning. |

### Typography Colors
| Token Name | Hex Code | Godot Color | Purpose / Usage |
| :--- | :--- | :--- | :--- |
| `TextPrimary` | `#1E1B4B` | `Color(0.118, 0.106, 0.294, 1.0)` | High-contrast midnight indigo for all titles, headings, and button labels. |
| `TextSecondary` | `#4B5270` | `Color(0.294, 0.322, 0.439, 1.0)` | Subtitles, instructions, metadata, slot timestamps. |
| `TextMuted` | `#8C93AE` | `Color(0.549, 0.576, 0.682, 1.0)` | Disabled hints, placeholder text, shortcut labels (e.g. "(C)"). |
| `TextLight` | `#FFFFFF` | `Color(1.0, 1.0, 1.0, 1.0)` | Text placed on colored accent buttons (Hot Pink, Sky Blue, Violet). |

---

## 2. Typography & Text Hierarchy

All UI typography must feel friendly, modern, and punchy.

- **Display Title (Screens & Menus)**: `font_size = 42` to `48`, Bold, Midnight Indigo `#1E1B4B`.
- **Section Heading**: `font_size = 22` to `26`, SemiBold / Bold, Midnight Indigo `#1E1B4B`.
- **Card Title / Major Button Label**: `font_size = 18` to `20`, Bold, `TextLight` or `TextPrimary`.
- **Body & Prompts**: `font_size = 15` to `16`, Regular / Medium, `TextSecondary` `#4B5270`.
- **Subtitles & Badges**: `font_size = 12` to `14`, SemiBold, all-caps or title case for tags.

---

## 3. Component Design Patterns

### A. Action Buttons (Pill & Wedge Shapes)
- **Shape**: Rounded corners (`corner_radius = 24` or full pill).
- **Styling**:
  - Primary button: Solid Hot Pink background (`#FF3E83`) with white text and a soft pink drop shadow (`rgba(255, 62, 131, 0.30)`).
  - Secondary button: Solid Sky Blue background (`#38B6FF`) with white text and soft cyan shadow.
  - Studio button: Solid Pastel Violet (`#8F65F8`) with white text.
  - Tertiary / Ghost button: White surface (`#FFFFFF`) with subtle border (`#E2E6F0`) and `TextPrimary` label.
- **Minimum Size**: `custom_minimum_size = Vector2(280, 52)` for main menu buttons.

### B. Cards (Scene Cards, Player Cards)
- **Shape**: `corner_radius = 16`.
- **Surface**: Pure White (`#FFFFFF`).
- **Shadow & Border**: Thin subtle border (`#E2E6F0`) with 4px soft ambient shadow (`rgba(30, 27, 75, 0.06)`).
- **Aspect Ratio**: 16:9 thumbnail previews on the left, descriptive metadata and duration badges on the right.

### C. The Hero Emblem (The osu! "Cookie" Pattern)
- A central circular crest representing the game's energy.
- Inner disc: Crisp white or gradient with the "Dubbed Up" clapper/mic iconography.
- Outer ring: Hot Pink with subtle Sky Blue glow.
- **Rhythmic Pulse**: Scales smoothly (`1.00x` $\to$ `1.04x` $\to$ `1.00x`) synchronized with the background music beat pulse (~128 BPM).

---

## 4. Motion & Animation Principles

Nothing in Dubbed Up should appear statically or snap without feedback. "Juice" and tactility are essential:

1. **Hover Micro-Interaction**:
   - On `mouse_entered`: Scale up to `1.04x` using `Tween.TransitionType.Back` with `Tween.EaseType.Out` (duration: `0.15s`).
   - On `mouse_exited`: Reset scale to `1.00x` (duration: `0.12s`).
2. **Click / Press Punch**:
   - On `button_down`: Squash to `0.96x` (duration: `0.06s`).
   - On `button_up`: Spring back to `1.04x`.
3. **Screen Transitions**:
   - When navigating, screens slide or fade gracefully (duration: `0.20s` to `0.25s`) rather than instantly popping.

---

## 5. Audio UX & Invariant Rules

1. **Menu Music Loop**:
   - `Dubbed Up.mp3` loops continuously across all menu screens (`MainMenu`, `ScenePicker`, `Lobby`, `Setup`, `Results`, `Settings`).
   - Controlled globally by `MenuMusicController`.
2. **Audio Separation Invariant (CRITICAL)**:
   - When entering `RecordingScreen` or `PlaybackScreen`, `MenuMusicController` **must smoothly fade out to -80 dB / silence** in `0.3s`.
   - Outside speech boxes during playback, original scene audio plays at `0 dB`. Inside speech boxes, original dialogue is muted (`-80 dB`) and background music plays cleanly.
   - When returning from recording or playback to any menu screen, `MenuMusicController` smoothly fades back in to normal volume.

