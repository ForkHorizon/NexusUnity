# Changelog - Nexus Unity

All notable public changes to Nexus Unity are documented here.

## [1.0.0] - 2026-05-18

### Added
- First public open source release for `com.forkhorizon.nexus.unity`.
- Raw HTTP JSON-RPC API with 111 Unity Editor tools discoverable through `list_tools`.
- Consolidated MCP bridge API with 14 `unity_` manager and macro tools for AI clients.
- Modular Python bridge package under `nexus_bridge/`.
- `unity_write_and_compile` macro for write, wait, and compiler-feedback workflows.
- Editor tests for path security, port behavior, manager routing, raw registry consistency, and bridge contract consistency.
- Release-readiness docs: `SECURITY.md`, `CONTRIBUTING.md`, and `RELEASE.md`.
- `GPL-3.0-only` license.

### Changed
- Public repository target is `https://github.com/ForkHorizon/NexusUnity.git`.
- Public documentation now treats Nexus Unity as a standalone open source package.
- The Unity installer deploys both `nexus_unity_bridge.py` and the required `nexus_bridge/` module to the project root.
- Package version, server version, bridge version, and public docs now use `1.0.0`.
- Package metadata now includes public repository, documentation, changelog, and license URLs.

### Removed
- Removed generated Graphify cache files from the package repo.
- Removed tracked `.jules/`, `.DS_Store`, Python bytecode, and local validation artifacts from public package contents.
- Experimental native bridge artifacts are not included in the public release.

## Pre-public History

Earlier internal builds used `2.x` and `3.x` version numbers while the package was being validated. Public semantic versioning starts at `1.0.0`.
