## 2024-05-23 - WebSocket Fragmentation
**Learning:** `WebSocket.ReceiveAsync` returns fragments, not complete messages. Assuming a single read is a full message causes bugs for payloads > 4KB or split packets.
**Action:** Always use a loop with `!result.EndOfMessage` and accumulate bytes (e.g., in a `MemoryStream`) before processing.

## 2024-05-27 - [Stream JSON Parsing]
**Learning:** `JObject.Parse(string)` creates a massive LOH allocation for the input string before parsing even begins. Unity's GC (especially older ones) struggles with frequent LOH churn.
**Action:** Use `ProcessJsonRpc(TextReader)` with `JsonTextReader` and `leaveOpen: true` on `StreamReader` to parse directly from network streams/MemoryStreams without intermediate string allocations.
