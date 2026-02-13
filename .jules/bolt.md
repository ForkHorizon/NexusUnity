## 2024-05-23 - WebSocket Fragmentation
**Learning:** `WebSocket.ReceiveAsync` returns fragments, not complete messages. Assuming a single read is a full message causes bugs for payloads > 4KB or split packets.
**Action:** Always use a loop with `!result.EndOfMessage` and accumulate bytes (e.g., in a `MemoryStream`) before processing.

## 2024-05-24 - Large JSON Payloads
**Learning:** Reading full request bodies into strings (`reader.ReadToEnd()`) causes massive allocations (LOH) for large JSON payloads.
**Action:** Use `JsonTextReader` with a `StreamReader` to parse directly from the stream. Also use `Formatting.None` for responses to minimize network traffic.
