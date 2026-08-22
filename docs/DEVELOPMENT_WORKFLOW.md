# Two-contributor development workflow

GitHub Issues and Pull Requests are the coordination layer. An assigned issue with `status: in-progress` is the work lock. The protected `main` branch requires a pull request and passing `core` and `repository-consistency` CI checks, including for administrators.

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
git checkout -b issue-12-short-description
```

Each implementation issue has one owner and one branch. Its body records owner, branch, status, expected scope, likely files/directories, and dependencies. Use `parallel-safe` only when the paired workstreams have genuinely separate contracts/files.

## Commits, pushes, and PRs

Make small coherent commits. Push active work regularly. Do not reuse branches, share a feature branch without explicit agreement, or force-push shared/reviewed history.

Every implementation PR closes one issue and states:

- change and motivation;
- testing performed;
- architecture or shared-contract effects;
- dependencies and license implications;
- limitations and file-overlap risks.

Apply `status: review` when the PR is ready. Before merge, fetch `main`, inspect divergence, integrate deliberately, rerun affected tests, and push. Do not merge failing, conflicted, stale, incomplete, or licensing-blocked work.

## Conflict prevention

- Select work by dependency readiness and file ownership, not convenience.
- Split by modules/directories and stabilize the smallest cross-stream contract first.
- Assign hotspot edits to one issue at a time.
- Avoid broad refactors while a dependent branch is active.
- If both branches changed the same intent, understand and preserve both valid behaviors; never blindly choose ours/theirs.
- Escalate architectural disagreements exposed by conflicts.

## Merge and next task

Routine merges are allowed only after CI passes, the diff is self-reviewed, current `main` is integrated, and no active PR conflicts. Product scope expansion, licensing risk, paid services, and hard-to-reverse architecture choices require human approval.

After merge, delete the branch, synchronize `main`, and only then claim the next ready issue. Reassess whether the other active branch must sync.
