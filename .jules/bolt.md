## 2024-05-23 - WebSocket Fragmentation
**Learning:** `WebSocket.ReceiveAsync` returns fragments, not complete messages. Assuming a single read is a full message causes bugs for payloads > 4KB or split packets.
**Action:** Always use a loop with `!result.EndOfMessage` and accumulate bytes (e.g., in a `MemoryStream`) before processing.

## 2026-02-11 - JSON-RPC String Allocation Bottleneck
**Learning:** The `ProcessJsonRpc` method signature `string -> string` forces full allocation of request and response bodies. While `TextReader` overload mitigates request allocation, response serialization still allocates full strings before sending.
**Action:** Future optimizations should introduce `ProcessJsonRpc(TextReader, TextWriter)` to stream responses directly to the network stream.
