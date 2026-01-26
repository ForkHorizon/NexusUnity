# Changelog - NexusUnity

All notable changes to the `NexusUnity` library will be documented in this file.

## [1.2.0] - 2026-01-26

### Added
- **Console Log Capturing**: Added ability to capture Unity console logs in real-time.
- **Log API**: New JSON-RPC methods `read_logs` and `clear_logs` for external log retrieval and management.
- **Port Update**: Default port moved to `8081` to avoid common local development conflicts.

## [1.1.0] - 2026-01-26

### Changed
- **Architectural Split**: Separated `MCPServerWindow.cs` into `MCPServerWindow.cs` (Lifecycle/UI) and `MCPServerMethods.cs` (Logic) for better maintainability and compliance with file length standards.
- **JSON Engine**: Integrated `Newtonsoft.Json` (com.unity.nuget.newtonsoft-json) for more robust and flexible JSON-RPC 2.0 processing.
- **Performance**: Optimized main-thread dispatching using `ManualResetEventSlim`.

### Fixed
- Resolved compilation issues caused by missing assembly references.
- Fixed thread safety issues in server-to-main-thread communication.
- Corrected naming convention violations across the codebase.

### Added
- Comprehensive XML documentation for all public APIs.
- Support for dynamic script attachment with `SessionState` persistence across assembly reloads.

## [1.0.0] - 2026-01-25

### Added
- Basic MCP Server implementation with support for `initialize`, `create_primitive`, and `attach_script`.
- Internal main-thread task queue.
- Basic Editor GUI for server control.
