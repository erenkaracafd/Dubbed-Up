# Project Status

- **Milestone:** MVP implementation is feature-complete on the active integration branch; repository reconciliation and human fun-validation are still pending.
- **Active Branch:** `issue-8-local-round-integration`
- **Integration PR:** #19 (`issue-8-local-round-integration` -> `main`)

## Completed Foundations & Subsystems

- **Core Architecture & Engine Separation**
  - `DubbedUp.Core` remains engine-agnostic and platform-independent (.NET 8 / .NET 10).
  - `DubbedUp.Core.Tests` has 69 unit/integration tests passing on the active branch.
- **Godot Runtime & Local Session Loop**
  - Main menu and local-session navigation are implemented.
  - Solo Dubbing, Co-op Dubbing, and Competitive Voting flows are implemented.
  - Recording, playback, voting/results, replay/next-round integration is present on the active branch.
- **Microphone Recording & Playback**
  - Real microphone capture, level metering, re-recording, take preview, and synchronized playback are implemented.
  - Microphone latency compensation is configurable from 0-400 ms and persisted in `user://audio_settings.cfg`.
- **Interactive Waveform & Scene Editing**
  - PCM waveform visualization, draggable/resizable speech boxes, subtitle editing, deletion controls, dynamic duration scaling, and scene persistence are implemented.
- **Custom Scene Import Pipeline & Transcoding**
  - Imported `.mp4`, `.webm`, `.mov`, and `.mkv` media are automatically transcoded to Godot-native `.ogv` (Theora/Vorbis) and 16-bit PCM `.wav` on-the-fly via `MediaTranscoder.cs`.
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
> - All official launch scenes will be 100% rights-cleared, original, or commercially licensed with documented provenance (tracked under Issue #5).

## Verification Reported on the Active Branch

- `dotnet build DubbedUp.sln --configuration Debug`: 0 errors, 0 warnings.
- `dotnet test tests/DubbedUp.Core.Tests`: 69/69 tests passing.
- Standalone game launch verified via `.\run-game.ps1`.
- **Manual Full-Round Microphone & Playback Smoke Test (Verified & Passed):**
  - Tested on Windows 11 standalone build with real microphone hardware.
  - Completed end-to-end loop: `MainMenu` -> `ScenePicker` (*Museum Mix-up*) -> `Setup` (Player names configured) -> `Recording` (Voice takes captured on real mic with live metering, previewed without drift) -> `Playback` (Video, background music, and user voice take played simultaneously with continuous delta clock and zero freezing).
  - All 3 official launch scenes (`museum_mixup`, `cooking_disaster`, `space_drift`) have valid `provenance.json` linked to `docs/OFFICIAL_CONTENT_PROVENANCE.md`.

## Current Merge Gate

PR #19 is the repository reconciliation gate. Do not start another broad feature branch from stale `main` until #19 is resolved.

Before merging #19, verify:

1. CI passes from the pull-request workflow.
2. A clean checkout can build and launch the game.
3. One complete local round can reach results and replay/next-round without stale state. (VERIFIED)
4. Microphone recording, take preview, and synchronized playback remain usable on a second machine/device setup where practical. (VERIFIED)
5. All official scenes have verified `provenance.json` pointing to `docs/OFFICIAL_CONTENT_PROVENANCE.md`. (VERIFIED)
6. Issue #3, #6, #7, and #8 acceptance criteria are reconciled; PR #19 is configured to close them on merge.

## Remaining MVP Gates

### #10 - Fun-validation playtest

After #19 merges, #10 becomes the primary MVP task.

Required outcome:
- run at least one representative two-or-more-player session;
- complete the full local loop without editor intervention;
- record blocking usability/reliability defects;
- add regression coverage where practical;
- explicitly answer whether recording, watching, and voting is fun enough to continue.

## Completed Content Gate on the Active Branch

### #5 - Rights-cleared official scenes

Three purpose-made official scenes are included with durable rights/provenance evidence in `docs/OFFICIAL_CONTENT_PROVENANCE.md` and per-scene `provenance.json` files. Issue #5 is configured to close with PR #19.

## Post-MVP Backlog

These are intentionally behind the MVP validation gate and should be tracked in separate issues/branches rather than expanding PR #19:

- Steamworks SDK integration and real Steam Workshop UGC upload/binding.
- Additional rights-cleared official scene packages.
- Online multiplayer lobby/session polish and real-world network playtesting.
- Packaging/release hardening and broader device compatibility.

## Contributor Rule

Until #19 is merged, treat `issue-8-local-round-integration` as the source of truth for current implementation. After merge, branch new work from updated `main` and keep issue status/acceptance criteria synchronized with code changes.
