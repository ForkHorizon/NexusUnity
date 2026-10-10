# Baseline at the start of the compatibility sprint

Recorded 2026-10-05 from the package git state, then updated with what this sprint changed on disk.

| Item | Value |
| --- | --- |
| Branch | `rework2/S-02` |
| Tip at start | `bfdd0f7e71c851fb4615956f3c420940a9623220` (`feat: lock canonical CLI parameters to the descriptor`) |
| `development` ancestor | not rebased. Stack still begins at the previously shipped commits `6fff2e8`, `060cc13`, `aa923f6`, `23005b4`, `bfdd0f7`. |
| Push / merge | none |
| Live harness | PID 4465, Unity `6000.3.16f1`, Pipeline `0.7.0-exp.1`, port 7800. Still that process at the end of the sprint. |
| Installed CLI | `~/.unity/bin/unity` is still `1.0.0-beta.10`. It was not self-updated. |
| Isolated CLI | `/tmp/unity-cli-beta12/unity` is `1.0.0-beta.12`, sha256 `7f21746fe7b2cf1010a7f293763ad9590715166147dcc0401a7b52de0d95f107`. |

Uncommitted product files at the end of the sprint, not yet committed because the seam review was still open when the evidence was written:

- `Editor/Commands/CaptureGameViewCommand.cs`
- `Editor/Commands/GroupCompileErrorsCommand.cs`
- `Tests/Editor/NexusCommandCliDriftTests.cs`
- `Editor/Pipeline/UnityMCP.Editor.Pipeline.asmdef` (adds `Unity.Pipeline.Attributes` beside `Unity.Pipeline`)

Research under `NexusFeature~/v2_analysis/I_08_compat_validation/` stays untracked. `MASTER_PLAN_V2.md` was not edited.

Scratch worktree `/tmp/nexus-p08-wt` is detached at `bfdd0f7` plus the same uncommitted product files. Its git directory is the submodule worktree `.../.git/modules/Assets/NexusUnity/worktrees/nexus-p08-wt`. Status on that tree must use `--git-dir` and `--work-tree`. A plain `git -C` status walks the wrong tree.
