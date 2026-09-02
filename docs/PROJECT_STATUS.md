# Project Status

- **Milestone:** MVP local loop and Online Multiplayer foundation are feature-complete on `main`; human fun-validation playtest (Issue #10) is the active priority.
- **Active Branch:** `main`

## Completed Foundations & Subsystems

- **Core Architecture & Engine Separation**
  - `DubbedUp.Core` remains engine-agnostic and platform-independent (.NET 8 / .NET 10).
  - `DubbedUp.Core.Tests` has 87 unit/integration tests passing (%100).
- **Godot Runtime & Local Session Loop**
  - Main menu and local-session navigation are implemented.
  - Solo Dubbing, Co-op Dubbing, and Competitive Voting flows are implemented.
  - Recording, playback, voting/results, replay/next-round integration is present and verified.
- **Steam Lifecycle & Online Multiplayer Foundation**
  - Steamworks.NET client lifecycle, lobby creation, joining, member discovery, and friend invite overlays with clean ENet local fallback (Issue #20 / PR #24).
  - Versioned zero-media multiplayer message protocol with strict size bounds and SHA-256 scene integrity checking (Issue #21 / PR #25).
  - Reliable, size-bounded, chunked (32 KB) voice-take transport with SHA-256 payload integrity verification (Issue #22 / PR #26).
  - Host-authoritative ready barrier, NTP-style clock offset estimation with median outlier filtering, and scheduled playback start synchronization (Issue #23 / PR #27).
- **Microphone Recording & Playback**
  - Real microphone capture, level metering, re-recording, take preview, and synchronized playback are implemented.
  - Microphone latency compensation is configurable from 0-400 ms and persisted in `user://audio_settings.cfg`.
- **Interactive Waveform & Scene Editing**
  - PCM waveform visualization, draggable/resizable speech boxes, subtitle editing, deletion controls, dynamic duration scaling, and scene persistence are implemented.
  - Streamlined scene creation workflow: input simplified to video picker and title, immediately transitioning to the visual scene editor.
  - Sleek 1.0px speech box borders with non-obstructive corner resize handles; moving constrained strictly to center grip dot to prevent accidental drags.
  - Speech box cut/split tool (toolbar button and `C` / `X` shortcut) allowing quick splitting at the current playhead position.
  - Automatic speech-to-text subtitle generation via local Whisper AI with silent-gap rejection (no ghost boxes in silence or pure music) and full Turkish UTF-8 character support (`ç, ğ, ı, ö, ş, ü, İ`).
- **Custom Scene Import Pipeline & Transcoding**
  - Windows media prerequisites are reproducibly installed by `scripts/setup-media-tools.ps1`; runtime discovery prefers the ignored project-local Whisper environment/config and falls back to explicit environment variables or system tools.
  - Imported `.mp4`, `.webm`, `.mov`, and `.mkv` media are automatically transcoded to Godot-native `.ogv` (Theora/Vorbis) and 16-bit PCM `.wav` on-the-fly via `MediaTranscoder.cs`.
  - In-game video playback uses a smooth 720p proxy for stutter-free 60fps performance across all clip lengths, while final exports remain pristine 1080p H.264 CRF 18.
  - Dynamic duration-based transcoding timeouts eliminate unexpected ffmpeg kills on longer high-resolution clips.
  - Per-scene unique thumbnail extraction with SHA-256 path collision prevention and lively frame selection at 2.0s.
  - Safe ASCII normalization and deadlock-free non-blocking process execution guarantee fast, unhindered transcoding.
  - Custom scene media scanning, quick handoff to scene selection, and in-game scene deletion with a safety confirmation modal are implemented.
- **Selective Audio Composition**
  - Original movie audio is preserved outside speech boxes.
  - During speech boxes, original dialogue is ducked while background audio and the player's dub take are mixed together.
- **Localization**
  - Current in-game UI text is 100% English.
- **Playtest Groundwork**
  - `docs/PLAYTEST_CHECKLIST.md` documents local/online playtest checks and feedback prompts.

## Third-Party Media & Intellectual Property Policy

> [!NOTE]
> **Test Scenes Disclaimer:**
> Any third-party video or audio clips (such as internet memes, film excerpts, or YouTube clips) used during local development and testing are strictly temporary test media (`user://workshop_scenes`) intended solely to validate the UGC importing, stem separation, and playback pipeline.
> - No unlicensed or copyrighted media is bundled in the official game repository or shipping binaries.
> - All official launch scenes will be 100% rights-cleared, original, or commercially licensed with documented provenance.

## Verification Reported on `main`

- `dotnet build DubbedUp.sln --configuration Debug`: 0 errors, 0 warnings.
- `dotnet test tests/DubbedUp.Core.Tests --configuration Release`: 87/87 tests passing.
- Standalone game launch verified via `.\run-game.ps1`.
- **Manual Full-Round Microphone & Playback Smoke Test (Verified & Passed):**
  - Tested on Windows 11 standalone build with real microphone hardware.
  - Completed end-to-end loop: `MainMenu` -> `ScenePicker` (*Museum Mix-up*) -> `Setup` (Player names configured) -> `Recording` (Voice takes captured on real mic with live metering, previewed without drift) -> `Playback` (Video, background music, and user voice take played simultaneously with continuous delta clock and zero freezing).
  - All 3 official launch scenes (`museum_mixup`, `cooking_disaster`, `space_drift`) have valid `provenance.json` linked to `docs/OFFICIAL_CONTENT_PROVENANCE.md`.

## Active Priority Gate

### #10 - Fun-validation playtest

Primary remaining MVP task.

Required outcome:
- run at least one representative two-or-more-player session;
- complete the full local loop without editor intervention;
- record blocking usability/reliability defects;
- add regression coverage where practical;
- explicitly answer whether recording, watching, and voting is fun enough to continue.

## Post-MVP Backlog

- Steam Workshop UGC upload/download binding.
- Additional rights-cleared official scene packages.
- Online multiplayer lobby/session UI polish and real-world network playtesting.
- Packaging/release hardening and broader device compatibility.
