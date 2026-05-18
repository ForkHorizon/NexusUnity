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

## 2025-03-01 - [GetComponents Array Allocation in Scene Traversal]
**Learning:** Calling `go.GetComponents<Component>()` inside loops that iterate over the entire scene hierarchy (like `SemanticFind` or `FindReferences`) allocates a new array on the heap for every single GameObject, leading to extreme GC pressure and stuttering in large projects.
**Action:** Always acquire a `List<Component>` from `UnityEngine.Pool.ListPool<Component>.Get(out var list)` outside the loop, use the non-allocating overload `go.GetComponents(list)` inside the loop, and rely on the `using` block to safely release the buffer. In static utility functions, a persistent private static `List<Component>` can also be used if thread-safety is not a concern (e.g., Unity Main Thread execution).

## 2025-03-01 - [Hierarchy GetComponent Cache Allocation]
**Learning:** O(N) `GetComponent(string)` lookups within property-setting loops cause significant C#/C++ boundary crossings and memory allocations, degrading performance during deep hierarchy iterations.
**Action:** Use a local `Dictionary<string, Component>` mapped via `UnityEngine.Pool.ListPool<Component>` and `go.GetComponents(list)` to achieve O(1) lookups and eliminate cross-boundary garbage collection overhead per component.

## 2025-03-01 - [Hierarchy GetComponent Cache Allocation (Revised)]
**Learning:** Instantiating a `new Dictionary<string, Component>()` per GameObject during hierarchy operations introduces heap allocations and GC spikes that negate the benefits of avoiding `GetComponent(string)`. Furthermore, GameObjects can have multiple components of the same type; populating a dictionary via overwriting causes it to cache the *last* component rather than the *first* (which is what `GetComponent(string)` returns), causing a functional regression.
**Action:** Since the number of components on a single GameObject is typically small, a zero-allocation O(N) linear search through the pre-populated `ListPool<Component>` list is far more performant than allocating a Dictionary. Avoid heap allocations entirely inside high-frequency loops.

## 2025-03-01 - [GetObjectPath Allocation]
**Learning:** Constructing hierarchy paths from leaf to root by dynamically instantiating `Stack<string>` in `MCPServerMethods.GetObjectPath` (or using `string.Concat` in a loop) creates unnecessary heap allocations and GC churn, especially during deep scene traversal.
**Action:** Use a `private static Stack<string> _pathStackCache` and clear it before reuse to eliminate allocations per path query. Use `string.Join("/", stack)` for efficient final path assembly.

## 2025-03-02 - [FindReferences GetComponents Array Allocation]
**Learning:** During scene traversal in `FindReferences`, calling `go.GetComponents<Component>()` on every GameObject allocates a new array each time. In a large scene with thousands of objects, this causes massive GC pressure and unnecessary garbage collection pauses. Similarly, allocating a new `List<string>` for matching components per object compounds the issue.
**Action:** Use the non-allocating overload `GetComponents(List<Component>)` with a static/reused buffer list. Also reuse the matching components list with `.Clear()` instead of creating a new instance per GameObject.

## 2025-03-10 - [Direct Stream Reading for HTTP JSON Payloads]
**Learning:** Reading `context.Request.InputStream` fully into a `string` before parsing HTTP requests causes huge Large Object Heap (LOH) allocations for deep hierarchies or large payloads. Stream reading prevents this LOH penalty entirely.
**Action:** Always parse JSON payloads by passing a `StreamReader` directly to `JObject.Load` (or the internal `ProcessJsonRpc(TextReader)`) instead of using `ReadToEnd()` -> `JObject.Parse()`.

## 2025-02-28 - HTTP CSRF & DNS Rebinding
**Vulnerability:** The MCP server accepted HTTP requests without validating the `Origin` header, allowing arbitrary websites to connect to localhost (CSRF) and execute commands.
**Learning:** The previous fix only applied `Origin` validation to WebSocket connections, leaving HTTP requests vulnerable. Both endpoints need `Origin` checks.
**Prevention:** In `HandleHttpRequest`, verify `context.Request.Headers["Origin"]` to ensure it is either empty/safe or coming from a loopback address.

## 2024-04-26 - [High] DoS vulnerability via memory exhaustion in Unity Networking
**Vulnerability:** Unbounded use of `StreamReader.ReadToEnd()` for HTTP requests and unbounded accumulation via `MemoryStream.Write()` in WebSocket loop.
**Learning:** Unity networking logic that reads directly from standard input streams into memory (strings/byte arrays) without checking stream limits can cause total memory exhaustion and Denial of Service if the server handles massive or chunked payloads.
**Prevention:** Always enforce a maximum payload limit (e.g. 10MB) via `ContentLength64` or string length limits for HTTP requests, and accumulate check chunk sizes (`ms.Length`) inside the WebSocket `ReceiveAsync` loop before executing writes.

## 2025-03-10 - [Scene Roots Array Allocation]
**Learning:** `UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()` allocates a new `GameObject[]` array on the heap every time it is called. When called repeatedly (e.g., generating mermaid diagrams or taking scene snapshots), this creates unnecessary Garbage Collection (GC) pressure.
**Action:** Always use the non-allocating overload `GetRootGameObjects(List<GameObject>)` combined with `UnityEngine.Pool.ListPool<GameObject>.Get(out var list)` to reuse buffers and completely eliminate allocations during scene root retrieval. Include comments explaining the use of ListPool for GC avoidance to maintain readability.
## 2024-06-15 - Optimize Reflection and String Allocations in High-Frequency C# Loops
**Learning:** In C#, repeated calls to `Type.Name` incur significant reflection overhead and string allocations. Furthermore, string interpolation (`$"{a}{b}"`) inside large loops like recursive scene hierarchy traversals creates thousands of temporary strings, causing severe Garbage Collection (GC) spikes. Similarly, `string.ToLower().Contains()` allocates a new string copy every iteration.
**Action:** Use a `ConcurrentDictionary<Type, string>` to cache and retrieve type names globally using a helper method like `GetTypeName`. For large recursive text generation (like Mermaid graph dumping), strictly use chained `StringBuilder.Append()` calls instead of interpolation. For case-insensitive substring matching in loops, use allocation-free methods like `IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0`.
