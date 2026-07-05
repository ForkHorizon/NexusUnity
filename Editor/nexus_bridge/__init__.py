"""NexusUnity Python bridge package.

Exposes the public API for routing MCP tool calls to the Unity editor server:

- :data:`.schemas.STATIC_TOOLS` — MCP tool definitions advertised to AI clients.
- :data:`.schemas.STATIC_RESOURCES` — static resources advertised via ``resources/list``.
- :func:`.routing.route_tool` — dispatch a tool call by name and arguments.
- :func:`.client.call_unity` — low-level JSON-RPC call to the Unity server.
"""
