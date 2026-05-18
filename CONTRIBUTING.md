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

## Development Rules

- Keep the server local-only. Do not add non-loopback binding without an explicit security design.
- Keep file operations constrained to the Unity project root.
- Prefer consolidated MCP manager tools for new AI workflows instead of expanding schemas unnecessarily.
- Keep raw JSON-RPC tool names and MCP bridge schemas synchronized.
- Update `CHANGELOG.md`, `README.md`, `DOCUMENTATION.MD`, and `API_REFERENCE.MD` when public behavior changes.
- Do not commit generated caches, local agent folders, Unity `Library/`, Python bytecode, `.DS_Store`, or private validation artifacts.
- Do not commit API keys, tokens, private project source, or proprietary assets.

## Validation

Run the Unity Editor tests for the package before submitting changes. At minimum, cover:

- Path security tests.
- Server port and loopback behavior.
- Raw API registry consistency.
- MCP bridge schema and routing consistency.
- Write-and-compile workflows when changing bridge or code-write behavior.

If Unity is not available in your environment, document what you could not run and include any static checks you did run.

## Pull Request Checklist

- [ ] Package compiles in Unity.
- [ ] Relevant Editor tests pass.
- [ ] Public docs updated for behavior/API changes.
- [ ] `CHANGELOG.md` updated.
- [ ] No generated caches or local-only files included.
- [ ] No secrets or personal credentials included.
