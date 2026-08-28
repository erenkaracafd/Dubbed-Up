# Codex and contributor instructions

These rules apply to the entire repository and to every human or AI coding agent.

## Coordination sources of truth

Use GitHub as the live coordination layer:

- The active implementation Issue is the work lock and live work log.
- The feature branch and Pull Request contain the proposed implementation.
- `docs/PROJECT_STATUS.md` is the concise snapshot of merged project state.
- `docs/ARCHITECTURE.md` describes durable architecture and changes only when architecture actually changes.

No meaningful implementation work may be silent. If code, tests, dependencies, shared contracts, generated assets, or behavior change, the responsible Issue/PR must say what changed.

## Session startup (before modifying files)

1. Read this file, `docs/PROJECT_STATUS.md`, `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT_WORKFLOW.md`, and the relevant feature documents under `docs/`.
2. Run `git status --short --branch` and inspect recent history.
3. Inspect all open GitHub Issues and Pull Requests relevant to the intended area, including ownership, branches, dependencies, claimed files/directories, shared contracts, and likely overlap.
4. Synchronize local `main` with `git pull --ff-only`.
5. Select one unclaimed, dependency-ready issue; assign it to yourself and apply `status: in-progress`.
6. Before editing, make the Issue ownership block explicit and current:
   - Owner
   - Agent/tool in use
   - Branch
   - Status
   - Owned scope
   - Main files/directories expected to change
   - Avoid/overlap areas
   - Dependencies and shared contracts
7. Create a fresh `issue-<number>-<slug>` branch from current `main`.

Never implement directly on `main`. An assigned active issue is a work lock. Do not modify files, directories, or a significant subsystem claimed by another active issue without explicit coordination recorded on both affected issues or the relevant PRs.

If a task unexpectedly needs a shared hotspot or another workstream's owned area, stop that part of the implementation, record the proposed overlap, and resolve ownership before editing it.

## While working

- Keep `DubbedUp.Core` free of Godot, platform APIs, Steamworks, and FFmpeg.
- Preserve the separation between source media, dub project data, session state, and player voice data.
- Keep persisted formats engine-independent and explicitly versioned.
- Use only official/commercially licensed scene media with provenance metadata.
- Treat a shared contract used by another active branch as stable; coordinate material changes before editing it.
- Do not introduce dependencies with prohibited or unclear licensing. Update `THIRD_PARTY_NOTICES.md` for every dependency.
- **Preserve Audio Separation & Playback Invariants:**
  - Inside speech boxes: original dialogue must be muted (-80 dB), background ambient music/effects must continue playing at full volume (0 dB), and player voice takes mixed cleanly over the music.
  - Outside speech boxes: original video audio with actor dialogue must play at 100% volume (0 dB).
  - Video aspect ratio must always be preserved dynamically using `AspectRatioContainer` without squeezing.
  - Playback master clock must remain continuous and delta-based; never seek `VideoStreamPlayer.StreamPosition` frame-by-frame in `_Process` as it causes keyframe decoder freezes and stutters.
- Make small coherent commits and push recoverable work regularly. Never force-push shared or reviewed branches.

After every meaningful push or coherent implementation checkpoint, add an append-only work-log update to the active Issue. Include:

```text
Commit/PR: <sha or link>
Changed: <files/directories or contracts>
Summary: <what behavior/implementation changed>
Next: <next intended step>
Blockers/overlap: <none, or explicit risk/coordination needed>
```

A "meaningful checkpoint" includes completed behavior, a changed contract/schema, dependency changes, a newly discovered overlap/blocker, or a push another contributor may need to understand before continuing parallel work. Trivial formatting-only commits may be grouped into the next meaningful update.

Before editing a file that was not declared in the Issue ownership block, update the Issue first. Before changing a shared hotspot, re-check active Issues/PRs immediately before the edit.

## Before merge or handoff

1. Fetch current `main`, inspect upstream changes and active PRs, and deliberately integrate them if the branch is stale.
2. Run relevant build, tests, and repository verification.
3. Self-review the diff for scope, architecture, licensing, secrets, generated files, and overlap with active work.
4. Check documentation impact explicitly:
   - Update `docs/PROJECT_STATUS.md` when the PR changes the concise merged-state snapshot: completed foundations/features, implementation frontier, verification, blockers, repository protection, or next ready work.
   - Update `docs/ARCHITECTURE.md` only when the PR changes durable module boundaries, data boundaries, shared contracts, dependency direction, runtime areas, persistence format strategy, or architectural hotspots.
   - Do not edit either file merely to record temporary branch progress; active progress belongs in the Issue work log.
5. Push the branch and create/update a focused PR that closes its issue.
6. The PR must explicitly list changed areas, verification, architecture/status documentation impact, dependencies/licenses, limitations, and active-work overlap.
7. Add a final Issue work-log entry summarizing the PR/handoff state.
8. Apply `status: review`; merge only when CI passes and no active work conflicts.
9. After merge, synchronize `main` before selecting another issue and re-check whether the other active workstream must sync.

GitHub Issues are authoritative for active individual work state. Keep `docs/PROJECT_STATUS.md` concise and limited to merged project state; do not duplicate the full issue board or per-commit history there.
