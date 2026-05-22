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

## Development Rules

- Keep the server local-only. Do not add non-loopback binding without an explicit security design.
- Keep file operations constrained to the Unity project root.
- Prefer consolidated MCP manager tools for new AI workflows instead of expanding schemas unnecessarily.
- Keep raw JSON-RPC tool names and MCP bridge schemas synchronized.
- Update `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, and `API_REFERENCE.MD` when public behavior changes.
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

### Optional Local Pre-Push Hook

The repository includes a tracked pre-push hook for local feedback. Git does not enable tracked hooks automatically after clone, so install it once per checkout:

```bash
bash scripts/install-git-hooks.sh
```

The hook runs `scripts/prepush-validate.sh --quick`. This is the contributor default: static package checks always run, and a short read-only live smoke check runs when the Nexus Unity server is already available at `http://127.0.0.1:8081/`. If the server is not running, the hook skips the live smoke instead of blocking the push.

Run full local integration validation explicitly when changing server behavior, bridge routing, or public API contracts:

```bash
bash scripts/prepush-validate.sh --integration
```

Integration tests require the Unity project to be open with the Nexus Unity server running. If this package is not checked out under a Unity project, set `NEXUS_UNITY_PROJECT_ROOT` before running integration validation.

The local hook is convenience only and can be bypassed with `git push --no-verify`; GitHub branch protection should require the `Validate package` workflow for merge enforcement.

## Pull Request Checklist

- [ ] Package compiles in Unity.
- [ ] Relevant Editor tests pass.
- [ ] Public docs updated for behavior/API changes.
- [ ] `CHANGELOG.md` updated.
- [ ] No generated caches or local-only files included.
- [ ] No secrets or personal credentials included.
