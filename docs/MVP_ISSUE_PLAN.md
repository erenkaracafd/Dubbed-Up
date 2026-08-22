# MVP issue plan

GitHub Issues are authoritative for live ownership and status. This document records the initial dependency shape and intended boundaries.

```text
#1 Foundation
 |
 +--> #2 Core project format + scene/timeline domain --+
 |                                                  |
 +--> #3 Godot runtime shell + screen navigation ----+--> #6 Recording flow
 |                                                  |
 +--> #4 Core session/round assignments -------------+--> #8 End-to-end integration
 |                                                  |
 +--> #5 Official licensed test scene ---------------+--> #7 Playback composition
 |                                                       |
 +--> #9 Core voting/scoring -----------------------------+
                                                         |
                                                         +--> #10 Playtest hardening
```

The initial safe pair after #1 is:

- Contributor A: #2, isolated to Core scene/timeline/project-format code and tests.
- Contributor B: #3, isolated to the Godot runtime shell and UI navigation.

They share only the already-established Core project reference. #3 must not invent scene/domain contracts owned by #2.

## Planned issues

| Issue | Outcome | Depends on | Primary ownership area |
|---|---|---|---|
| #1 | Repository foundation, planning, workflow, skeleton, CI | — | shared hotspots |
| #2 | Versioned JSON scene/project format and timeline models | #1 | Core `Scenes`, `Timeline`, `ProjectFormat` |
| #3 | Main menu/local setup UI shell and phase navigation | #1 | Godot `UI`, `LocalSession` |
| #4 | Player/session/round assignment state and tests | #1 | Core `Sessions`, `Rounds`, `Characters` |
| #5 | One rights-cleared official test scene package | #1 + human rights approval | `Content/` only |
| #6 | Microphone recording adapter and take workflow | #2, #3, #4 | Godot `Microphone`; Core `VoiceTakes` contract |
| #7 | Synchronized scene/voice playback | #2, #3, #5 | Godot playback areas |
| #8 | Local round orchestration and replay/next-round loop | #4, #6, #7, #9 | integration/composition |
| #9 | Voting, tallying, scoring, and tie behavior | #1 | Core `Voting`, `Scoring` |
| #10 | End-to-end playtest hardening and feedback checklist | #8 | tests/docs/focused fixes |

Issue #5 is intentionally content-only so media work cannot accidentally alter schemas or playback code. #6 owns the initial voice-recording contract to prevent two branches from independently changing it.

