# Release Checklist

This checklist is for publishing `com.forkhorizon.nexus.unity` as an open source Unity Package Manager package.

## Release Identity

- Package id: `com.forkhorizon.nexus.unity`
- Public repository: `https://github.com/ForkHorizon/NexusUnity.git`
- License: `MIT`
- Current public version: `1.6.0`
- Minimum Unity version: `6000.0`

## Development Versioning

Between public releases, keep `package.json` at the latest shipped version unless maintainers intentionally publish a prerelease tag. Do not bump the Unity package version for every fix merged to development.

Public contribution flow:

- Feature branches and contributor pull requests target `development`.
- `main` is release-only and should be protected from direct contributor pushes.
- Direct pushes to `main` and `development` are blocked for everyone, including maintainers.
- Maintainers promote reviewed `development` changes to `main` only through a release pull request.

Use `CHANGELOG.md` as the source of truth during development:

- Add user-visible behavior, API, documentation, and validation changes under `[Unreleased]`.
- Keep compatibility notes and migration guidance in the docs while the work is unreleased.
- Prepare the next semantic version only when cutting a release branch or release commit.

Unity Package Manager requires `MAJOR.MINOR.PATCH` in `package.json`, for example `1.6.0`. GitHub release tags, titles, and announcements use the same semantic version: `v1.6.0` for tags and `1.6.0` for release titles.

When preparing the release, choose the version by semantic versioning:

- Patch: compatible bug fixes and documentation corrections only.
- Minor: backward-compatible public API additions, new tool options, or notable workflow improvements.
- Major: breaking public contracts or required migration work.

## Pre-release Checks

1. Verify `Assets/NexusUnity/package.json`:
   - `name` is `com.forkhorizon.nexus.unity`.
   - `version` matches the Unity package version, such as `1.6.0`.
   - `license` is `MIT`.
   - Repository, documentation, changelog, and license URLs point to the public repository.
2. Verify docs:
   - `README.md` install command uses the public Git URL.
   - `DOCUMENTATION.MD` version and security model match the code.
   - `API_REFERENCE.MD` matches raw `list_tools` and MCP bridge schemas.
   - `CHANGELOG.md` contains the release entry.
   - `SECURITY.md` and `CONTRIBUTING.md` are present.
3. Verify package contents:
   - Include `Editor/`, `Runtime/`, `README.md`, `DOCUMENTATION.MD`, `API_REFERENCE.MD`, `CHANGELOG.md`, `LICENSE.md`, `SECURITY.md`, `CONTRIBUTING.md`, and required `.meta` files.
   - Exclude `.soma/`, `graphify-out/`, `.jules/`, `.DS_Store`, `__pycache__/`, `*.pyc`, Unity `Library/`, temporary validation projects, native bridge binaries without source/build instructions, and `.meta` files under Unity-ignored folders such as `tools~/` and `Tests~/`.
4. Verify security posture:
   - Server binds to loopback only.
   - Origin validation rejects non-loopback origins.
   - Request payload limits are enforced for HTTP and WebSocket paths.
   - File writes remain inside the Unity project root.
5. Run validation:
   - Confirm the self-hosted runner is online with `self-hosted`, `macOS`, `ARM64`, `nexus-unity-ci`, and `nexus-doc-ai` labels.
   - Confirm the runner has `python3`, `dotnet`, Unity `6000.4.3f1`, and Ollama available locally.
   - Unity package compile.
   - Editor tests for path security, server port behavior, API contract, consolidated managers, and bridge contract consistency.
   - `python3 -m unittest discover -s Editor/tests -v` for the Python bridge unit test suite.
   - `bash scripts/prepush-validate.sh --quick` for the contributor gate.
   - `bash scripts/prepush-validate.sh --integration` when a local Unity project is available.
   - Public API stress audit comparing raw `list_tools` with the MCP bridge catalog and exercising mutating tools in a disposable namespace.
   - Optional project auditor/lint pass.

## Tagging

Use a semantic GitHub release tag matching the package version:

```bash
git tag vX.Y.Z
git push origin vX.Y.Z
```

The patch component is always included in public tags. Reserve patch bumps for urgent compatible fixes, and do not push tags until the public repository contents and release notes have been reviewed.

## Release Pull Request

Release changes flow through a final pull request into `main`.

1. Create a release PR from `development` or `release/*` to `main`.
2. Confirm the `PR target policy`, `Static validation`, `Python unit tests`, `Documentation quality AI`, and `Unity package smoke` checks pass on the self-hosted Mac runner.
3. Resolve all review conversations.
4. Merge the release PR manually.
5. Create and push the matching GitHub release tag.

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
