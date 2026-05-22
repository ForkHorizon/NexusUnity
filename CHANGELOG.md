# Changelog - Nexus Unity

All notable public changes to Nexus Unity are documented here.

## [Unreleased]

### Added
- GitHub issue templates, pull request template, static validation workflow, and code of conduct for public community maintenance.
- README badges and reproducible install URL pinned to `v1.0.0`.
- A single `Window > Nexus Unity` menu entry that opens the main Nexus Unity window.
- Tracked optional fast pre-push hook and installer for contributor local validation.
- Development versioning policy: keep unreleased development on the latest public package version and record user-visible work under `[Unreleased]` until release preparation.
- Public API stress-audit documentation covering raw `list_tools` validation, MCP bridge catalog validation, disposable mutation namespaces, and cleanup expectations.
- Contributor branch policy documentation: public pull requests target `development`; `main` is release-only.
- Pull request target policy workflow for enforcing contributor PRs to `development` and release PRs to `main`.
- UI Toolkit regression tests for the Nexus Unity editor windows.
- MCP integration config generator for Codex TOML, JSON-style MCP clients, VS Code-compatible `servers` config, Cursor, Windsurf, Claude Desktop, and generic copy/paste setup.
- Editor tests for integration config generation and responsive integration hub controls.

### Changed
- Consolidated API verification, project audit, test window, and Codex link test actions into the main Nexus Unity window instead of exposing separate Unity submenu entries.
- The package validation workflow now reuses the local static validator and includes an optional Unity EditMode test job when a Unity license secret is configured.
- The local pre-push hook now runs a sub-minute quick gate by default; full Python integration validation is opt-in with `scripts/prepush-validate.sh --integration`.
- Validation now runs on both `main` and `development` and documents that direct pushes to protected branches are blocked for everyone.
- Rebuilt Nexus Unity editor windows with compact UI Toolkit layouts, responsive wrapping, minimum usable bounds, and named automation elements.
- Reworked the main Nexus Unity window around user-facing `Server`, `Integrations`, and `Resources` tabs; test/internal actions now live under collapsed `Advanced / Diagnostics`.
- Integration setup is now card-based with status, auto setup, copy config, and config-location actions instead of a row of ambiguous CLI buttons.
- Wide editor layouts now split server and bridge controls into responsive cards and keep the header compact when stretched horizontally.
- Raw API schemas now document `update_component`'s preferred `properties` payload, keep its legacy `json_data` payload compatible, and expose `invoke_method.arguments` as a valid array schema.
- `create_material` now accepts an optional explicit asset `path`, allowing stress tests and automation to keep generated materials isolated.
- Public docs now identify `unity_write_and_compile` as the supported code-edit macro and treat old `unity_apply_code_change` wording as stale.

### Fixed
- `click_object_in_game` now honors `instance_id` as documented instead of only accepting hierarchy paths.
- Existing Codex and Claude Desktop config files are backed up before Nexus Unity updates their MCP server entries.

### Removed
- Removed orphan `Runtime/Tests.meta` file from the public package.

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
