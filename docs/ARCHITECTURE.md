# Architecture

## Shape

Dubbed Up is a modular monolith split at the engine boundary:

```text
Godot presentation and adapters
            |
            v
Plain C# application/domain modules
```

`DubbedUp.Core` owns game rules and durable engine-independent models. `DubbedUp.Godot` owns UI, microphone capture, synchronized media playback, local filesystem integration, and composition. Core never references Godot or platform APIs.

## Core modules

- `Game`: top-level phase transitions and round loop.
- `Sessions`: local players and session configuration.
- `Rounds`: per-round assignments and progress.
- `Scenes`: official scene metadata and character definitions.
- `Characters`: character identity and voice slots.
- `VoiceTakes`: recorded-take metadata, never audio capture APIs.
- `Timeline`: placement of dialogue/takes against scene time.
- `Voting`: vote eligibility and tallying.
- `Scoring`: score rules and results.
- `ProjectFormat`: versioned JSON contracts and validation.

Modules should expose small domain-focused types rather than a central `GameManager` or global registry.

## Runtime areas

- `UI`: menus, setup, recording, playback, voting, and results screens.
- `Input`: player and navigation input.
- `Microphone`: Godot audio-bus capture implementation.
- `VideoPlayback` and `AudioPlayback`: synchronized official video and voice takes.
- `LocalStorage`: official content/project/voice file access.
- `LocalSession`: composition and local flow orchestration.

## Data boundaries

Four concepts remain separate even when referenced by ID:

1. Source media: licensed immutable official assets plus provenance.
2. Dub project data: scene selection, assignments, timeline, takes, and schema version.
3. Session state: active players, phase, votes, and scores.
4. Player voice data: local audio files and capture metadata.

Canonical project data is versioned JSON, not Godot resources. Media paths are relative logical references; session/player recordings are not embedded in official content metadata.

## Contracts

Add interfaces only at realistic change boundaries. Expected MVP seams are `IVoiceRecorder`, `IMediaPlayer`, and `IProjectStore`. Define the smallest contract in Core or an appropriate runtime area before two branches implement against it. Do not add export or transport interfaces until their features exist.

## Hotspots

`DubbedUp.sln`, `Directory.Build.props`, `project.godot`, CI workflows, the JSON schema/version, shared state-machine types, and the Godot composition root are merge-conflict hotspots. Give changes to these files explicit issue ownership and avoid broad refactors during parallel feature work.

