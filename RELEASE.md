# Release Checklist

This checklist is for publishing `com.forkhorizon.nexus.unity` as an open source Unity Package Manager package.

## Release Identity

- Package id: `com.forkhorizon.nexus.unity`
- Public repository: `https://github.com/ForkHorizon/NexusUnity.git`
- License: `GPL-3.0-only`
- Current public version: `1.0.0`
- Minimum Unity version: `6000.0`

## Pre-release Checks

1. Verify `Assets/NexusUnity/package.json`:
   - `name` is `com.forkhorizon.nexus.unity`.
   - `version` matches the release tag.
   - `license` is `GPL-3.0-only`.
   - Repository, documentation, changelog, and license URLs point to the public repository.
2. Verify docs:
   - `README.md` install command uses the public Git URL.
   - `DOCUMENTATION.MD` version and security model match the code.
   - `API_REFERENCE.MD` matches raw `list_tools` and MCP bridge schemas.
   - `CHANGELOG.md` contains the release entry.
   - `SECURITY.md` and `CONTRIBUTING.md` are present.
3. Verify package contents:
   - Include `Editor/`, `Runtime/`, `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD`, `CHANGELOG.md`, `LICENSE.md`, `SECURITY.md`, `CONTRIBUTING.md`, and required `.meta` files.
   - Exclude `.soma/`, `graphify-out/`, `.jules/`, `.DS_Store`, `__pycache__/`, `*.pyc`, Unity `Library/`, temporary validation projects, and native bridge binaries without source/build instructions.
4. Verify security posture:
   - Server binds to loopback only.
   - Origin validation rejects non-loopback origins.
   - Request payload limits are enforced for HTTP and WebSocket paths.
   - File writes remain inside the Unity project root.
5. Run validation:
   - Unity package compile.
   - Editor tests for path security, server port behavior, API contract, consolidated managers, and bridge contract consistency.
   - Optional project auditor/lint pass.

## Tagging

Use a semantic version tag matching `package.json`:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Do not push tags until the public repository contents and release notes have been reviewed.

## Post-release Smoke Test

Install into a clean Unity project with Package Manager using:

```text
https://github.com/ForkHorizon/NexusUnity.git
```

Then verify:

- `Window > Nexus Unity` opens.
- Server starts on `127.0.0.1:8081` or the configured loopback port.
- `get_server_status` returns a valid JSON-RPC response.
- MCP bridge starts with `python3 Packages/com.forkhorizon.nexus.unity/Editor/nexus_unity_bridge.py`.
