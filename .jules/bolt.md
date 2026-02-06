## 2024-05-23 - WebSocket Fragmentation
**Learning:** `WebSocket.ReceiveAsync` returns fragments, not complete messages. Assuming a single read is a full message causes bugs for payloads > 4KB or split packets.
**Action:** Always use a loop with `!result.EndOfMessage` and accumulate bytes (e.g., in a `MemoryStream`) before processing.

## 2024-05-24 - Streaming JSON Parsing
**Learning:** Reading full request bodies into strings (`reader.ReadToEnd()`) before parsing creates massive GC spikes in Unity for large payloads.
**Action:** Use `ProcessJsonRpc(TextReader)` overload with `JObject.Load(new JsonTextReader(reader))` to stream data directly from the socket stream.
