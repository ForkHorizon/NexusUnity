## 2024-05-23 - WebSocket Fragmentation
**Learning:** `WebSocket.ReceiveAsync` returns fragments, not complete messages. Assuming a single read is a full message causes bugs for payloads > 4KB or split packets.
**Action:** Always use a loop with `!result.EndOfMessage` and accumulate bytes (e.g., in a `MemoryStream`) before processing.

## 2024-05-27 - [Stream JSON Parsing]
**Learning:** `JObject.Parse(string)` creates a massive LOH allocation for the input string before parsing even begins. Unity's GC (especially older ones) struggles with frequent LOH churn.
**Action:** Use `ProcessJsonRpc(TextReader)` with `JsonTextReader` and `leaveOpen: true` on `StreamReader` to parse directly from network streams/MemoryStreams without intermediate string allocations.

## 2024-06-01 - [GetRootGameObjects Allocation]
**Learning:** SceneManager.GetRootGameObjects() allocates a new array on every call, creating significant GC pressure in hot paths like hierarchy retrieval.
**Action:** Use the GetRootGameObjects(List<GameObject>) overload with a reused list buffer to achieve zero-allocation hierarchy traversal.

## 2025-03-01 - [FindObjects N+1 Component Allocation]
**Learning:** Calling `go.GetComponent(typeName)` in a LINQ query that iterates over all GameObjects (e.g., `Resources.FindObjectsOfTypeAll<GameObject>()`) creates a massive N+1 bottleneck, scaling poorly with project size. For Regex, while static `Regex.IsMatch` uses an internal cache and doesn't explicitly re-compile, it still incurs lookup and parsing overhead per iteration. However, using `RegexOptions.Compiled` on short-lived objects is a massive performance trap due to IL compilation latency.
**Action:** Start component searches with `Resources.FindObjectsOfTypeAll(type)` mapped back via `.OfType<Component>().Select(c => c.gameObject).Distinct()`. Pre-instantiate local `Regex` objects without `Compiled` before entering loops.

## 2025-03-01 - [Hierarchy GetComponent Cache Allocation]
**Learning:** O(N) `GetComponent(string)` lookups within property-setting loops cause significant C#/C++ boundary crossings and memory allocations, degrading performance during deep hierarchy iterations.
**Action:** Use a local `Dictionary<string, Component>` mapped via `UnityEngine.Pool.ListPool<Component>` and `go.GetComponents(list)` to achieve O(1) lookups and eliminate cross-boundary garbage collection overhead per component.

## 2025-03-01 - [Hierarchy GetComponent Cache Allocation (Revised)]
**Learning:** Instantiating a `new Dictionary<string, Component>()` per GameObject during hierarchy operations introduces heap allocations and GC spikes that negate the benefits of avoiding `GetComponent(string)`. Furthermore, GameObjects can have multiple components of the same type; populating a dictionary via overwriting causes it to cache the *last* component rather than the *first* (which is what `GetComponent(string)` returns), causing a functional regression.
**Action:** Since the number of components on a single GameObject is typically small, a zero-allocation O(N) linear search through the pre-populated `ListPool<Component>` list is far more performant than allocating a Dictionary. Avoid heap allocations entirely inside high-frequency loops.
