# Two-contributor development workflow

GitHub Issues and Pull Requests are the coordination layer. An assigned issue with `status: in-progress` is the work lock. The protected `main` branch requires a pull request and passing `core` and `repository-consistency` CI checks, including for administrators.

The workflow is designed for two contributors or AI agents working in parallel without silent overlap.

## Sources of truth

Use each artifact for one purpose:

1. **Active Issue** — live ownership, branch, declared files/scope, dependencies, overlap risks, blockers, and append-only work-log updates.
2. **Feature branch / PR** — proposed code and the complete reviewable change.
3. **`docs/PROJECT_STATUS.md`** — concise snapshot of what is already merged into `main`, current blockers, verification, and implementation frontier.
4. **`docs/ARCHITECTURE.md`** — durable technical architecture; update only for real architectural changes.

Do not use `PROJECT_STATUS.md` as a live per-commit log. Doing so makes parallel branches compete for the same file and creates avoidable merge conflicts.

## Task lifecycle

```text
Backlog -> Ready -> In Progress -> Review -> Done
                          |
                          -> Blocked
```

Before work, confirm dependencies are complete, inspect active issue/PR file scopes for overlap, claim the issue, record the branch, and branch from updated `main`:

```powershell
git checkout main
git pull --ff-only
git checkout -b issue-14-short-description
```

Each implementation issue has one owner and one branch. Before any code edit, its ownership block must explicitly record:

- Owner
- Agent/tool in use
- Branch
- Status
- Owned scope
- Main files/directories expected to change
- Avoid/overlap areas
- Dependencies and shared contracts

An active assigned issue is a lock on its declared workstream. A second contributor must not edit that area without explicit coordination recorded on the affected issues or PRs.

Use `parallel-safe` only when the paired workstreams genuinely have separate files/contracts or a stable shared contract has already been agreed.

## Live work log

After every meaningful push or coherent implementation checkpoint, append a short update to the active Issue. Use issue comments for the running log so the ownership block stays stable.

```text
Commit/PR: abc1234
Changed:
- src/example/
- tests/example/
Summary:
Implemented the declared behavior or contract change.
Next:
Next intended implementation step.
Blockers/overlap:
None.  # or state the exact coordination risk
```

Log an update whenever another contributor could make a wrong coordination decision without knowing what just changed. This includes:

- completed behavior or tests;
- new/changed shared contracts or schemas;
- dependency or license changes;
- newly touched files/directories outside the original declaration;
- new blockers or overlap risks;
- a meaningful push that the other contributor may need to integrate or avoid.

Formatting-only or similarly trivial commits may be grouped into the next meaningful checkpoint.

If scope expands to a file not declared in the Issue, update the Issue before editing that file.

## Shared hotspots and overlap

Hotspots include solution/project files, CI, `project.godot`, shared schemas, shared state-machine contracts, composition roots, and other files both branches are likely to need.

Before touching a hotspot:

1. Re-check open Issues and PRs immediately.
2. Record the intended hotspot change and why it is needed.
3. Confirm which issue owns that edit.
4. If another active workstream depends on or edits it, record the coordination plan before making the change.

Do not silently make a cross-workstream change and leave the conflict for merge time.

## Commits, pushes, and PRs

Make small coherent commits. Push active work regularly. Do not reuse branches, share a feature branch without explicit agreement, or force-push shared/reviewed history.

Every implementation PR closes one issue and states:

- change and motivation;
- changed files/directories or shared contracts;
- testing performed;
- architecture or shared-contract effects;
- `PROJECT_STATUS.md` impact;
- `ARCHITECTURE.md` impact;
- dependencies and license implications;
- limitations and active issue/PR overlap risks.

Apply `status: review` when the PR is ready. Before merge, fetch `main`, inspect divergence and active work, integrate deliberately, rerun affected tests, and push. Do not merge failing, conflicted, stale, incomplete, or licensing-blocked work.

## Documentation rules

Before a PR is ready for review, explicitly decide whether these files need changes:

### `docs/PROJECT_STATUS.md`

Update it when the PR changes the concise post-merge state, such as:

- completed foundations/features;
- implementation frontier or next ready work;
- current blockers;
- repository protection/workflow state;
- meaningful verification baseline.

Do not put branch-local progress, per-commit history, or temporary ownership here.

### `docs/ARCHITECTURE.md`

Update it only when durable architecture changes, including:

- module/runtime boundaries;
- data boundaries;
- dependency direction;
- shared interfaces/contracts;
- persistence/versioning strategy;
- architectural hotspots or composition responsibilities.

If architecture did not change, the PR should say `Architecture documentation impact: None` instead of touching the file.

## Conflict prevention

- Select work by dependency readiness and file ownership, not convenience.
- Split by modules/directories and stabilize the smallest cross-stream contract first.
- Assign hotspot edits to one issue at a time.
- Avoid broad refactors while a dependent branch is active.
- Re-check active work before expanding scope.
- If both branches changed the same intent, understand and preserve both valid behaviors; never blindly choose ours/theirs.
- Escalate architectural disagreements exposed by conflicts.

## Merge and next task

Routine merges are allowed only after CI passes, the diff is self-reviewed, current `main` is integrated, documentation impact is checked, and no active PR conflicts. Product scope expansion, licensing risk, paid services, and hard-to-reverse architecture choices require human approval.

Before handoff or merge, add a final Issue work-log entry with the PR, final changed areas, verification, and any remaining overlap/blocker.

After merge, delete the branch, synchronize `main`, and only then claim the next ready issue. Reassess whether the other active branch must sync before it continues.
