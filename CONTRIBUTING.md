# Contributing to Nexus Unity

Thank you for helping improve Nexus Unity. This package is intended to stay small, auditable, and safe for local Unity Editor automation.

## Scope

Public package code lives under `Assets/NexusUnity`.

Before changing package behavior, check the relevant docs:

- `README.md` for install and quick-start behavior.
- `DOCUMENTATION.MD` for architecture, security model, and troubleshooting.
- `API_REFERENCE.MD` for public raw JSON-RPC and MCP bridge tools.
- `SECURITY.md` for vulnerability handling and local-only assumptions.
- `RELEASE.md` for release steps.

## Branch Workflow

Contributor pull requests must target `development`.

- `development` is the integration branch for public contributions, validation, and unreleased work.
- `main` is release-only. Do not open contributor pull requests against `main`.
- Only maintainers should promote reviewed `development` changes to `main` during an intentional release.
- Use short-lived feature branches from `development` for public changes.
- Direct pushes to `main` and `development` are blocked for everyone, including maintainers.
- Trusted maintainers merge approved pull requests through GitHub; external contributors do not need direct repository write access.

## Commit Attribution

Author commits with an email address verified on your GitHub account, preferably the GitHub no-reply address from account email settings. Pull requests can merge even when commit author emails are not account-linked, but GitHub contributor statistics may not credit those commits. The repository `.mailmap` records known historical identity mappings for local Git reports; it is not a substitute for fixing your local `git config user.email` before future commits.

## Development Rules

- Keep the server local-only. Do not add non-loopback binding without an explicit security design.
- Keep file operations constrained to the Unity project root.
- Prefer consolidated MCP manager tools for new AI workflows instead of expanding schemas unnecessarily.
- Keep raw JSON-RPC tool names and MCP bridge schemas synchronized.
- Update `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, and `API_REFERENCE.MD` when public behavior changes.
- Document public and protected C# types and methods with useful XML comments that explain contract, side effects, and Unity Editor constraints. Do not add line-by-line comments or filler summaries.
- Do not commit generated caches, local agent folders, Unity `Library/`, Python bytecode, `.DS_Store`, or private validation artifacts.
- Do not commit API keys, tokens, private project source, or proprietary assets.

## Changelog And Versioning

During normal development, do not bump `package.json` for every fix. Keep the package version at the latest public release until maintainers prepare the next release.

- Record user-visible API, behavior, documentation, and validation changes under `[Unreleased]` in `CHANGELOG.md`.
- Update README, technical docs, and API reference in the same change when a public contract changes.
- Only release-preparation changes should move `[Unreleased]` entries into a dated version section and update `package.json`, badges, install examples, and visible version strings.
- Use semantic versioning for releases: patch for compatible fixes, minor for backward-compatible additions or notable behavior improvements, major for breaking public contracts.

## Validation

Run the Unity Editor tests for the package before submitting changes. At minimum, cover:

- Path security tests.
- Server port and loopback behavior.
- Raw API registry consistency.
- MCP bridge schema and routing consistency.
- Write-and-compile workflows when changing bridge or code-write behavior.

If Unity is not available in your environment, document what you could not run and include any static checks you did run.

Pull requests are checked by one GitHub check, `CI Scope`, posted by the CI Scope GitHub App; the checks run on a maintainer-owned Mac. What runs is defined by `.ci-scope.json` and the configs in this repo (`.code-linter.json`, the standard limits: 600 lines per file, 80 per function); the pinned gates live in [ForkHorizon/ci-gates](https://github.com/ForkHorizon/ci-gates) and the repository carries no gate logic of its own. `.github/workflows/ci-scope-checks.yml` is the single workflow. Unity compile and EditMode tests are not part of this check: run `scripts/prepush-validate.sh --integration` and the Unity EditMode suite locally before merging.

Contributor pull requests target `development`; `main` is release-only. Direct pushes to both are blocked and maintainers merge through GitHub. Review fork pull requests carefully before merge.

- public/protected production C# types or methods without XML documentation;
- XML summaries that are empty, too short, generic, or only repeat the symbol name;
- production C# files over 450 lines;
- production methods over 150 lines.

It warns on files over 300 lines and methods over 50 lines. Test methods are exempt from XML documentation requirements, but production code under `Editor/` and `Runtime/` is not.

The hook runs `scripts/prepush-validate.sh --quick`. This is the contributor default: static package checks always run, and `NEXUS_UNITY_HOOK_LIVE=auto` adds a short read-only live smoke check when the Nexus Unity server is available at `http://127.0.0.1:8081/`. If the server is temporarily unreachable or times out, the hook prints `NOTICE` and skips live smoke instead of blocking the push. Use `NEXUS_UNITY_HOOK_LIVE=required` to fail when the live server is unavailable, or `NEXUS_UNITY_HOOK_LIVE=off` to run only static checks.

Run full local integration validation explicitly when changing server behavior, bridge routing, or public API contracts:

```bash
bash scripts/prepush-validate.sh --integration
```

Run the required local AI documentation gate explicitly when operating the maintainer self-hosted runner:

For a faster maintainer or agent check of bridge/UI observability, run:

```bash
python3 scripts/agent-tooling-smoke.py
```

Integration tests require the Unity project to be open with the Nexus Unity server running. If this package is not checked out under a Unity project, set `NEXUS_UNITY_PROJECT_ROOT` before running integration validation.

Run the slower local Ollama review before risky API, security, or release-readiness changes:

## Pull Request Checklist

- [ ] Package compiles in Unity.
- [ ] Relevant Editor tests pass.
- [ ] Public docs updated for behavior/API changes.
- [ ] `CHANGELOG.md` updated.
- [ ] No generated caches or local-only files included.
- [ ] No secrets or personal credentials included.
- [ ] Pull request targets `development`, unless this is an intentional maintainer release PR to `main`.
