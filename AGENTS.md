# Codex and contributor instructions

These rules apply to the entire repository.

## Session startup (before modifying files)

1. Read this file and the relevant documents under `docs/`.
2. Run `git status --short --branch` and inspect recent history.
3. Inspect open GitHub Issues and Pull Requests, including ownership, branches, dependencies, and likely file overlap.
4. Synchronize local `main` with `git pull --ff-only`.
5. Select one unclaimed, dependency-ready issue; assign it to yourself and apply `status: in-progress`.
6. Record owner, branch, scope, likely files, and dependencies on the issue.
7. Create a fresh `issue-<number>-<slug>` branch from current `main`.

Never implement directly on `main`. An assigned active issue is a work lock. Do not modify a significant subsystem owned by another active issue without explicit coordination.

## While working

- Keep `DubbedUp.Core` free of Godot, platform APIs, Steamworks, and FFmpeg.
- Preserve the separation between source media, dub project data, session state, and player voice data.
- Keep persisted formats engine-independent and explicitly versioned.
- Use only official/commercially licensed scene media with provenance metadata.
- Prefer new focused files over changes to hotspots such as `DubbedUp.sln`, `project.godot`, CI, shared schemas, and composition roots.
- Treat a shared contract used by another active branch as stable; coordinate material changes.
- Do not introduce dependencies with prohibited or unclear licensing. Update `THIRD_PARTY_NOTICES.md` for every dependency.
- Make small coherent commits and push recoverable work regularly. Never force-push shared or reviewed branches.

## Before merge or handoff

1. Fetch current `main`, inspect upstream changes, and deliberately integrate them if the branch is stale.
2. Run relevant build, tests, and repository verification.
3. Self-review the diff for scope, architecture, licensing, secrets, and generated files.
4. Push the branch and create/update a focused PR that closes its issue.
5. Record testing, architecture/dependency changes, limitations, and blockers.
6. Apply `status: review`; merge only when CI passes and no active work conflicts.
7. After merge, synchronize `main` before selecting another issue.

GitHub is authoritative for individual work state. Keep `docs/PROJECT_STATUS.md` concise; do not duplicate the full issue board there.

