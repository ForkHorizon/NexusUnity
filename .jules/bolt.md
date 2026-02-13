## 2024-05-23 - WebSocket Fragmentation
**Learning:** `WebSocket.ReceiveAsync` returns fragments, not complete messages. Assuming a single read is a full message causes bugs for payloads > 4KB or split packets.
**Action:** Always use a loop with `!result.EndOfMessage` and accumulate bytes (e.g., in a `MemoryStream`) before processing.

## 2024-05-27 - Hierarchy Path Optimization
**Learning:** Constructing paths from leaf to root using string concatenation (path = parent + "/" + path) creates O(N^2) allocations.
**Action:** Use Stack<string> to collect names and string.Join("/", stack) for O(N) efficiency.
