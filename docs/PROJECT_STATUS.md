# Project status

- Milestone: MVP & Steam/Multiplayer Expansion Complete (with Local AI & Real Audio)
- Completed foundations:
  - #1 architecture/CI, #2 project format, #3 Godot UI shell, #4 local session/round, #6 microphone recording workflow, #7 synchronized playback composition, #8 local round orchestration & end-to-end local loop, #9 voting/scoring foundations, #10 playtest hardening & integration tests.
  - Workstream A (ScenePackageLoader & GameMode: Solo, Co-op, Competitive).
  - Workstream B (ScenePicker, Setup character preview, Co-op direct playback flow, auto-advance).
  - Workstream C (Steam Workshop UGC Provider & Local Custom Scenes Folder, user://workshop_scenes).
  - Workstream D (Multiplayer Lobby & Remote Audio Take Sync via Godot High-Level Multiplayer).
  - Audio & Hardware Subsystem: Dynamic input device selector (`SettingsScreen`), programmatic speaker test beep, real RIFF/WAVE parsing (`VoiceTakeAudioPlayer`), early mic bus initialization (`LocalNavigationController`).
  - Content & Video: IShowSpeed 16s scene package (`speed_mama_homeless`) with synchronized 20.9MB MP4 video playback.
  - Local AI Pipeline: `DubbedUp.Core.Ai` (`AiSceneBuilder`, `DetectedSpeechSegment`) and `LocalAiSceneExtractor` (SRT/VAD parser) for offline, local automatic scene generation inside `SceneCreatorScreen`.
- Implementation frontier: Full playable end-to-end local (Solo, Co-op, Competitive) and multiplayer loop; Steam-ready local AI scene generation; live playtest checklist ready (`docs/PLAYTEST_CHECKLIST.md`).
- Verification: .NET restored and built the full solution with zero warnings/errors; 69 Core unit and integration tests pass; Godot solution builds cleanly; git working tree clean.
- Known blockers: none.
- Repository protection: public repository; `main` requires pull requests and passing Core/consistency CI.
- Coordination: cross-contributor roadmap documented in `docs/STEAM_MULTIPLAYER_PLAN.md`; playtest guide in `docs/PLAYTEST_CHECKLIST.md`.
- Next available work: In-person playtesting, Steamworks AppID provisioning, and packaging release builds.
