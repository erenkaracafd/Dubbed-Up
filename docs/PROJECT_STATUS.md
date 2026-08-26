# Project Status

- **Milestone:** MVP implementation is feature-complete on the active integration branch; repository reconciliation and human fun-validation are still pending.
- **Active Branch:** `issue-8-local-round-integration`
- **Integration PR:** #19 (`issue-8-local-round-integration` -> `main`)

## Completed Foundations & Subsystems

- **Core Architecture & Engine Separation**
  - `DubbedUp.Core` remains engine-agnostic and platform-independent.
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
- **Custom Scene Import Pipeline**
  - Imported `.mp4`, `.webm`, `.mov`, and `.mkv` media can be transcoded to Godot-native `.ogv` through FFmpeg.
  - Demucs stem separation produces dialogue/vocal and background stems for selective playback composition.
  - Custom scene media scanning and save-to-scene-selection workflow are implemented.
- **Selective Audio Composition**
  - Original movie audio is preserved outside speech boxes.
  - During speech boxes, original dialogue is ducked while background audio and the player's dub take are mixed together.
- **Localization**
  - Current in-game UI text is English.
- **Playtest Groundwork**
  - `docs/PLAYTEST_CHECKLIST.md` documents local/online playtest checks and feedback prompts.

## Verification Reported on the Active Branch

- `dotnet build DubbedUp.sln --configuration Debug`: 0 errors, 0 warnings.
- `dotnet test tests/DubbedUp.Core.Tests`: 69/69 tests passing.
- Standalone game launch verified via `.\run-game.ps1`.

## Current Merge Gate

PR #19 is the repository reconciliation gate. Do not start another broad feature branch from stale `main` until #19 is resolved.

Before merging #19, verify:

1. CI passes from the pull-request workflow.
2. A clean checkout can build and launch the game.
3. One complete local round can reach results and replay/next-round without stale state.
4. Microphone recording, take preview, and synchronized playback remain usable on a second machine/device setup where practical.
5. No third-party copyrighted test media is accidentally being treated as distributable official content.
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

### #5 - Rights-cleared official scene

The project still needs at least one purpose-made or commercially licensed official MVP scene with durable rights/provenance evidence. Development/test clips must not be treated as distributable content unless their rights are documented.

## Post-MVP Backlog

These are intentionally behind the MVP validation gate and should be tracked in separate issues/branches rather than expanding PR #19:

- Steamworks SDK integration and real Steam Workshop UGC upload/binding.
- Additional rights-cleared official scene packages.
- Online multiplayer lobby/session polish and real-world network playtesting.
- Packaging/release hardening and broader device compatibility.

## Contributor Rule

Until #19 is merged, treat `issue-8-local-round-integration` as the source of truth for current implementation. After merge, branch new work from updated `main` and keep issue status/acceptance criteria synchronized with code changes.
