# TASKS_v1 — Frozen task set for the agent-architecture pilot (S4 / S5 / S6* / E)

Author: Agent G (blind task author: has NOT read F_facade_hypothesis.md, F_facade_evidence/, bench/facade_mock/).
Date: 2026-10-03. Status: **FROZEN v1** once §0.4 checksums are filled in by the builder. Any change after freeze = new file `TASKS_v2.md`; v1 runs are never re-scored against v2.

Inputs used: BRIEF.md; MASTER_PLAN_V2.md §2.1 (persona), §7 P0-08/P0-09; C_agent_efficiency.md §3; AI Chat reports 1–4.
Labels: REPO = checked in this repo; REPORTED = from reports 1–4; HYPOTHESIS = untested assumption the builder must verify in assert-mode.

---------------------------------------------------------------------
## 0. Ground rules

### 0.1 Arm neutrality
- Prompts never name a tool, command, MCP server, CLI, menu path of the arm, or "Unity CLI/Nexus". They may name project assets and Unity concepts (prefab, scene, console, import settings, Play mode), as a real user would.
- Every arm gets the identical prompt string, byte for byte. Arm-specific instructions (AGENTS.md, skills) are the arm's business and are not part of this file.
- Read-only prompts end with a fixed `ANSWER:` line format so that scoring is deterministic. The format line is part of the frozen prompt.

### 0.2 Hidden-answer hygiene (builder MUST follow)
- The fixture generator, this file, answer key, oracle snippets, task variants (T2 break set) and reference solutions live **outside** the fixture Unity project (harness dir, e.g. `NexusFeature~/bench/fixture_harness/`). Only generated assets are committed into the fixture project.
- Nothing inside the fixture project may contain the words bench, planted, decoy, bug, oracle, task, answer, trap (in names, comments, strings, asset labels). The only exception is the folder name `Assets/BenchFixture/` mandated by the requester. In-game naming uses the fictional project "Saltmarsh" (a harbour town game).
- Code comments in fixture scripts are realistic (as in a production repo) and never hint at the planted problem.

### 0.3 Oracle execution model
- The harness runs oracles **after** the agent's final message, through its own channel (not visible to or shared with any arm), against the same live Editor.
- Before any oracle: wait until `!EditorApplication.isCompiling && !EditorApplication.isUpdating`; record `EditorApplication.isPlaying`; if playing, exit Play mode and wait for Edit mode (record "left in Play mode" as metadata, not failure — except where stated).
- Oracle snippets below are C# method bodies returning `"PASS"` or `"FAIL:<reason>"`, prefixed by the PREAMBLE (§0.5). For read-only tasks, the harness additionally extracts the last line matching `^\s*ANSWER:\s*(.*)$` (case-insensitive) from the agent's final message; no ANSWER line = FAIL.
- **Read-only integrity check** (all read-only tasks): `git status --porcelain -- Assets ProjectSettings Packages` in the fixture project shows no change except files the arm's own package is allowed to touch (whitelist recorded per arm), and no open scene is dirty. Violation → task FAIL (category "mutated during read-only task").
- **Write tasks are judged on saved disk state** (scene reopened from disk), not in-memory state.
- Scores per run: `semantic_ok` (oracle PASS), `claimed_ok` (agent's final message claims success/completion), `false_success = claimed_ok && !semantic_ok`.

### 0.4 Freeze record (builder fills in, then commits; no other edits allowed)
| Item | Value |
|---|---|
| Fixture commit / tag | `fixture-v1` @ `<sha>` |
| Unity | 6000.3.16f1 (REPO: ProjectSettings/ProjectVersion.txt of host project) |
| Packages | URP 17.5.0, UGUI 2.5.0, Input System 1.19.0 (Active Input Handling = Input System Package, REPO), Test Framework 1.7.0, com.unity.pipeline 0.7.x `<exact>` |
| Active build target | Standalone (StandaloneOSX on mac / StandaloneWindows64 on win) |
| Harbor.unity total GameObjects | `<KEY_TOTAL>` (must be 1,100–1,400) |
| Harbor.unity max depth / count of nodes at depth ≥8 | 9 / `<KEY_DEEP>` (must be ≥30) |
| sha256 of TASKS_v1.md, oracle file, answer key | `<...>` |

### 0.5 PREAMBLE (prepended to every oracle snippet)
```csharp
using System; using System.IO; using System.Linq; using System.Collections.Generic;
using UnityEngine; using UnityEngine.SceneManagement; using UnityEngine.UI;
using UnityEditor; using UnityEditor.SceneManagement;

static GameObject F(Scene s, string path) {            // path WITHOUT scene name, e.g. "Harbor/Town/..."
    var parts = path.Split('/');
    foreach (var r in s.GetRootGameObjects()) {
        if (r.name != parts[0]) continue;
        if (parts.Length == 1) return r;
        var t = r.transform.Find(string.Join("/", parts, 1, parts.Length - 1)); // finds inactive too
        if (t != null) return t.gameObject;
    }
    return null;
}
static Scene OpenFromDisk(string p, out bool wasDirty) {
    var cur = SceneManager.GetSceneByPath(p);
    wasDirty = cur.IsValid() && cur.isDirty;           // recorded as metadata
    return EditorSceneManager.OpenScene(p, OpenSceneMode.Single); // discards unsaved edits (HYPOTHESIS: no prompt via API; verify in assert-mode)
}
static IEnumerable<GameObject> All(Scene s) =>
    s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
static string PathOf(GameObject g) { var t = g.transform; var p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
static string Sha(string assetPath) { using var h = System.Security.Cryptography.SHA256.Create();
    return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(assetPath))).Replace("-", ""); }
const string FX = "Assets/BenchFixture/";
```
`<KEY_*>` and `<SHA_*>` constants come from the answer key the builder writes at build time.

---------------------------------------------------------------------
## 1. Fixture specification (`Assets/BenchFixture/`)

### 1.1 Layout
```
Assets/BenchFixture/
  Scenes/      Harbor.unity (large, irregular), MainMenu.unity (UI), Docks_Test.unity (small)
  Scripts/Runtime/   asmdef Saltmarsh.Runtime
      UI/MenuController.cs
      Quests/QuestBoard.cs
      Tides/TideMath.cs, Tides/TideClock.cs
      Economy/PriceTable.cs, Economy/CargoId.cs
      Audio/HarborAmbience.cs, Audio/AmbienceProfile.cs (SO)
      Props/PropSet.cs (SO), Props/Bobbing.cs, Props/CargoSpot.cs, Props/NpcSpawnPoint.cs
      Data/CargoManifest.cs (SO), Data/LocalizationTable.cs (SO)
  Scripts/Weather/   asmdef Saltmarsh.Weather  — WeatherProfile.cs (SO, namespace Saltmarsh.Weather)
  Scripts/Editor/    asmdef Saltmarsh.Editor (Editor platform only; references Saltmarsh.Runtime ONLY)
      Icons/IconAtlasBaker.cs, Icons/IconAtlasPostprocessor.cs, Icons/IconAtlasSettings.cs (SO)
      Validation/SaltmarshStartupChecks.cs, ManifestValidator.cs, LocalizationCheck.cs, AudioBankCheck.cs, AmbienceWarmup.cs
  Tests/EditMode/    asmdef Saltmarsh.Tests.EditMode (Editor only, refs Saltmarsh.Runtime, nunit, UNITY_INCLUDE_TESTS)
      TideMathTests.cs, TideClockTests.cs, PriceTableTests.cs, CargoIdTests.cs
  Prefabs/Props/  Lantern_Post.prefab, Lantern_Post_Rusty.prefab (variant of Lantern_Post), StreetKit.prefab, Buoy.prefab, Crate.prefab
  Prefabs/UI/     Notice_Card_v2.prefab        (Notice_Card.prefab existed and was deleted — see 1.5)
  Art/Legacy/     Lantern_Post.prefab           (same file name, different prefab)
  Art/UI/         harbor_map.png (2048x1024), vignette.png (512x512, soft radial alpha)
  Art/Icons/Source/Market/   12 PNGs + icons.json
  Art/Icons/Source/Harbor/   11 PNGs + icons.json (manifest lists 10)
  Art/Icons/Source/Harbor_old/ 6 PNGs, NO icons.json
  Art/Icons/Generated/       IconAtlas_Market.png + IconAtlas_Market.json (pre-baked, 256x128)
  Audio/          bell_buoy.wav, gull_01..03.wav, waves_loop.wav, wind_loop.wav (short generated tones are fine)
  Data/           Manifests/Northpier.asset, Manifests/Southpier.asset, Loc/UI_en.asset, Ambience/AmbienceProfile_Default.asset,
                  Props/LanternSet.asset (PropSet), Tides/Tides_Default.asset
  Settings/       IconAtlasSettings.asset
  Materials/      3–5 URP/Lit materials shared by filler
```
Builder generates everything with an editor script run once from the harness (seeded `System.Random(6000317)`), then removes the generator from the project before committing.

### 1.2 Harbor.unity — irregular large hierarchy
Roots (in this order): `Systems`, `Main Camera`, `Directional Light`, `Global Volume`, `Harbor`, `NPCs`, `Audio`, `UI_World`, `[Debug]` (root **inactive**).

Approximate subtree quotas (filler fills to these; total 1,100–1,400 incl. prefab-instance children):
| Subtree | ~objects | Shape |
|---|---|---|
| Systems | 12 | flat-ish: GameManager, Pools/pool_crates, Pools/pool_fx, SaveSystem, TideClock (has TideClock component) |
| Harbor/Waterfront | ~520 | Piers/Pier_01..Pier_04 (~70 each), Breakwater (~150), Promenade (~60), Warehouses/wh_A, wh_B (~60) |
| Harbor/Town | ~430 | Square_Market, Alleys/alley_01..alley_07, Tavern (Exterior, Interior), Old_Quarter, Houses (~150) |
| Harbor/Terrain_Props | ~120 | **wide**: one parent `scatter` with 90 direct children (rocks/trees/bushes), depth 2–4 |
| NPCs | ~40 | NpcSpawnPoint components |
| Audio | ~20 | Ambience (HarborAmbience), gull emitters (AudioSource gull_*) |
| UI_World | ~25 | world-space Canvas with labels |
| [Debug] | ~15 | LightingTest, probes |

Filler generation rules:
- Depth (root = 1) ranges 2–9. At least 30 filler nodes at depth 8–9 in branches other than the planted ones, so "the deepest node" is not a giveaway.
- Child count per group node: weighted random 1–12; ~3% of group nodes are wide (25–90 children).
- Names: drawn from mixed pools + random suffix style, never a single `Item_N` pattern. Pools (examples, builder may extend in the same spirit): props `Crate, crate_small, Barrel, Barrell, SM_Rope_Coil_A, rope_coil, Net_Hanging, fish_box, Pallet, pallet_broken, Sack, sacks_pile, Bollard, Cleat, Ladder, Plank_Long, plank, Tarp, Lobster_Trap, Oar, Bucket`; structure `Root, Mesh, LOD0, LOD1, Collider, colliders, pivot, Visual, Geo, GameObject, Group, misc, __tmp, -- props --`; buildings `House_A, house_fisher_03, window, door, roof, chimney, Stairs`; nature `Tree_Pine_01, tree_birch, Bush, rock_big, Rock_Small`. Suffix style per node: none / ` (n)` / `_01` / `_A` / `_old` / lowercase variant. Duplicate sibling names allowed for filler (Unity-realistic), but never on a planted path.
- Reserved names that filler must never use: anything starting with `Lantern`, `lantern`, `lamp`, `Light Pole`, `StreetLight`, `BellAudio`, `buoy.`, `BuoyField`, `QuestBoard`, `Board`, `Notice`, `WorkLight`, `VignetteOverlay`, `StreetKit`, `rusty`.
- Components: ~40% MeshFilter+MeshRenderer (primitives, shared materials), ~10% BoxCollider, 30 CargoSpot, 14 NpcSpawnPoint, 12 Bobbing (on buoys), 5 ParticleSystem, 6 AudioSource with gull_*/waves_loop (never bell_buoy), 8 filler point Lights named `window_glow` (intensity 0.8, shadows None). ~5% of filler objects inactive.
- Filler never carries QuestBoard, MenuController, or an AudioSource with bell_buoy.

Planted content in Harbor.unity (exact paths; root-relative, no scene prefix):

**(a) Lantern prefabs.** `Prefabs/Props/Lantern_Post.prefab` structure: `Lantern_Post` (cylinder mesh) → `Head` (mesh) → `Lamp` (Light: Point, intensity 2.2, range 6, shadows Soft) and `Head/Glass` (mesh). Direct (outermost) instances — 17, five of them renamed:
```
 1 Harbor/Waterfront/Piers/Pier_01/Lamps/Lantern_Post
 2 Harbor/Waterfront/Piers/Pier_01/Lamps/Lantern_Post (1)
 3 Harbor/Waterfront/Piers/Pier_02/Lamps/Lantern_Post
 4 Harbor/Waterfront/Piers/Pier_03/Lamps/Lantern_Post
 5 Harbor/Waterfront/Piers/Pier_03/Lamps/lamp
 6 Harbor/Waterfront/Piers/Pier_03/Deck/bollard_row (1)/Lantern_Post (3)
 7 Harbor/Waterfront/Piers/Pier_03/End/Light Pole
 8 Harbor/Waterfront/Piers/Pier_04/Lamps/Lantern_Post (1)
 9 Harbor/Waterfront/Promenade/lights/Lantern_Post
10 Harbor/Waterfront/Promenade/lights/Lantern_Post (1)
11 Harbor/Waterfront/Promenade/lights/StreetLight
12 Harbor/Town/Square_Market/Corners/NE/Lantern_Post
13 Harbor/Town/Square_Market/Corners/SW/lantern
14 Harbor/Town/Alleys/alley_02/props/Lantern_Post
15 Harbor/Town/Tavern/Exterior/Lantern_Post
16 Harbor/Town/Tavern/Exterior/lamp_03
17 [Debug]/LightingTest/Lantern_Post
```
- `Pier_03/End/WorkLight`: Spot light, intensity 3, shadows Hard (not a lantern).
- `StreetKit.prefab` = `StreetKit` → `bench`, `bin`, nested `Lantern_Post` instance. Instances in Harbor: `Harbor/Town/Alleys/alley_03/StreetKit`, `Harbor/Town/Square_Market/StreetKit (1)`.
- `Lantern_Post_Rusty.prefab` (variant; overrides Lamp intensity 1.8, a rust material). Instances: `Harbor/Waterfront/Warehouses/wh_A/ext/Lantern_Post_Rusty`, `Harbor/Town/Old_Quarter/rusty lamp`.
- Decoy `Art/Legacy/Lantern_Post.prefab` (structure `Lantern_Post` → `Lamp`, Light intensity 1.2, shadows None). Instances all named `Lantern_Post`: `Harbor/Town/Alleys/alley_05/Lantern_Post`, `Harbor/Town/Old_Quarter/Lantern_Post`, `Harbor/Town/Old_Quarter/Lantern_Post (1)`, `Harbor/Waterfront/Warehouses/wh_B/ext/Lantern_Post`.
- `Data/Props/LanternSet.asset` (PropSet) `prefabs` list contains Props/Lantern_Post.prefab and Crate.prefab.
- Docks_Test.unity: root `Test` with 3 direct instances `Lantern_Post`, `Lantern_Post (1)`, `Lantern_Post (2)` + a QuestBoard set up correctly + ~25 filler.
- Consequence (intended): a name search for "Lantern_Post" in Harbor hits 12 Props instances + 4 legacy + 2 nested + 1 rusty = 19 objects; the true direct count is 17.

**(b) Bell buoys (deep, irregular).** Under `Harbor/Waterfront/Breakwater/` siblings `Segment_A`, `Segment_B`, `Segment_C`, `Segment_C (1)`, `Segment_C (2)`, `Segment_C (3)`, each with a `Markers` child (filler content). Only `Segment_C (2)/Markers` contains:
- `BuoyField_East` (**activeSelf = false**) → `buoy.r01` … `buoy.r12`, each `buoy.rNN/Mount/BellAudio` with AudioSource clip `bell_buoy`, `playOnAwake = false` — **except `buoy.r12`: playOnAwake = true**, component enabled.
- `BuoyField_West` (active) → `buoy.w01` … `buoy.w06`, same structure; `buoy.w04` playOnAwake = true; `buoy.w02/Mount/BellAudio` AudioSource component **disabled** (decoy).
- Target path (depth 9): `Harbor/Waterfront/Breakwater/Segment_C (2)/Markers/BuoyField_East/buoy.r12/Mount/BellAudio`.
- Buoy objects carry Bobbing + mesh. Total AudioSources with clip bell_buoy in Harbor = 18.

**(c) Quest boards.**
- `Harbor/Town/Square_Market/QuestBoard_Stand/Board` has `QuestBoard` with fields: `noticeTemplate` (GameObject) = **Missing** (see 1.5), `noticeRoot` (Transform) = `Board/Notices` child, `headerIcon` (Sprite, `[Tooltip("Optional")]`) = None, `maxNotices` = 6.
- `Harbor/Town/Tavern/Interior/notice_board (1)` has `QuestBoard` with `noticeTemplate` = `Prefabs/UI/Notice_Card_v2.prefab`, noticeRoot set, headerIcon None, maxNotices 4.
- `QuestBoard.Start()` instantiates `maxNotices` copies of `noticeTemplate` under `noticeRoot` (throws in Play mode when template is missing — only in Play mode).

**(d) Other:** `Audio/Ambience` has HarborAmbience referencing `AmbienceProfile_Default.asset`; `Systems/TideClock` references `Tides_Default.asset`.

### 1.3 MainMenu.unity — UI with a raycast-blocking overlay
```
Main Camera
MenuRoot                      (MenuController)
EventSystem                   (EventSystem + InputSystemUIInputModule — correct for Input System-only projects)
EventSystem (1)               (inactive leftover; decoy)
MenuCanvas                    (Canvas Screen Space-Overlay, sortingOrder 0, CanvasScaler ScaleWithScreenSize 1920x1080, GraphicRaycaster)
  Background                  (Image, raycastTarget true)
  MainPanel                   (CanvasGroup alpha 1, interactable true, blocksRaycasts true)
    Title                     (legacy UI Text "SALTMARSH")
    Buttons                   (VerticalLayoutGroup)
      PlayButton              (Image + Button, interactable true; onClick → MenuController.OnPlayPressed, 1 persistent listener)
      SettingsButton          (onClick → MenuController.OnSettingsPressed)
      CreditsButton           (interactable FALSE, label "Credits (soon)" — intentional, decoy)
      QuitButton              (onClick → MenuController.OnQuitPressed)
  MapPanel                    (inactive) → Map (Image, sprite harbor_map, RectTransform 1600x800, preserveAspect false)
  SettingsPanel               (inactive)
FxCanvas                      (Canvas Screen Space-Overlay, overrideSorting n/a (root canvas), sortingOrder 5, GraphicRaycaster)
  VignetteOverlay             (Image, sprite vignette, color white alpha 0.35, full-stretch anchors, raycastTarget TRUE)  ← the defect
```
- MenuController: `public static string LastAction;` each handler sets it and logs `[Menu] Play pressed` etc.
- Result: in Play mode every click is captured by VignetteOverlay (higher sorting order canvas); nothing on MenuCanvas reacts. Decoys that look suspicious but are fine: CreditsButton not interactable, inactive EventSystem (1), CanvasGroup on MainPanel, two canvases.

### 1.4 Console noise (deterministic, emitted on every domain reload)
`SaltmarshStartupChecks` is `[InitializeOnLoad]` and on `EditorApplication.delayCall` runs, in order:
1. 40 × `Debug.Log("[Startup] Registered cargo type <name>")` (distinct names).
2. `AudioBankCheck`: 300 × `Debug.LogWarning("[AudioBank] Clip 'amb_gull_<000..299>' has no loudness metadata; using default -14 LUFS")`.
3. 120 × identical `Debug.LogWarning("[Tides] Legacy tide curve format detected in 'Tides_Default'; re-save the asset to upgrade")`.
4. 1 × `Debug.LogWarning("[Net] Socket error budget at 0/5 (ok)")` — a warning containing the word "error" (decoy).
5. **E1** `ManifestValidator.Run()` → `Debug.LogError("[Cargo] Duplicate cargo id 'crate_fish_07' in Northpier (entries 4 and 11)")` — the call is on **line 47** of `Scripts/Editor/Validation/ManifestValidator.cs`. Northpier.asset really contains the duplicate at list indices 4 and 11.
6. **E2** `LocalizationCheck.Run()` → 5 × identical `Debug.LogError("[Loc] Missing key 'menu.play.subtitle' in table UI_en")` — call on **line 31** of `Scripts/Editor/Validation/LocalizationCheck.cs` (one per referencing UI prefab/scene usage). UI_en.asset really lacks the key.
7. **E3** `AmbienceWarmup.Run()` calls `HarborAmbience.BuildMix(profile)` inside try/catch → `Debug.LogException(e)`; the NullReferenceException originates at **line 58** of `Scripts/Runtime/Audio/HarborAmbience.cs` (dereferencing `layers[i].clip` where `AmbienceProfile_Default.layers[2]` is a null element).
Builder pads files so these line numbers are exact and records them in the answer key. Totals: 3 distinct errors (7 error entries), 421 warnings, 40 logs. No compile warnings are emitted (reset clears console after compiling).

### 1.5 Missing serialized reference
Builder: create `Prefabs/UI/Notice_Card.prefab`, assign to Square_Market QuestBoard.noticeTemplate, save Harbor.unity, then delete `Notice_Card.prefab` via AssetDatabase. Scene YAML keeps `{fileID: …, guid: <KEY_DELETED_GUID>, type: 3}`; Inspector shows "Missing (GameObject)". `Notice_Card_v2.prefab` remains. Answer key records `<KEY_DELETED_GUID>`.

### 1.6 Editor tool (report-4 analogue, no UI)
- `IconAtlasSettings.asset`: `sourceFolder = "Assets/BenchFixture/Art/Icons/Source/Market"`, `outputFolder = "Assets/BenchFixture/Art/Icons/Generated"`, `padding = 2`.
- Menu item `Tools/Saltmarsh/Icons/Bake Atlas (from settings)` → `IconAtlasBaker.Bake(settings.sourceFolder)` (public static). No dialogs.
- `Bake(folder)`: if `<folder>/icons.json` missing → `Debug.LogError("[IconAtlas] Missing icons.json in '<folder>'")` and return. Otherwise load the PNGs listed in the manifest (`File.ReadAllBytes` + `Texture2D.LoadImage`, so no importer dependency), pack as a grid: cell = (maxW+padding, maxH+padding), cols = ceil(sqrt(n)), rows = ceil(n/cols), atlas = next power of two of (cols·cellW) × next power of two of (rows·cellH); write `IconAtlas_<FolderName>.png` + `.json` (`{"set":..,"generator":"IconAtlasBaker/1.3","icons":[{name,x,y,w,h}...]}`) to outputFolder; then schedule `AssetDatabase.Refresh()` on `EditorApplication.delayCall` (import completes asynchronously after the menu call returns).
- `IconAtlasPostprocessor` (paths under Generated/): textureType Sprite, spriteImportMode Single, mipmaps off, filterMode Point, maxTextureSize 512; in `OnPostprocessAllAssets`, when `IconAtlas_<Set>.png` was imported, `SessionState.SetString("Saltmarsh.IconAtlas.LastBake", "<Set>|<n>|<W>x<H>")` from the json count and imported texture size.
- Harbor set: manifest lists 10 icons (anchor 64x40, crane 72x38, crate 40x40, fish 56x30, gull 48x36, lighthouse 32x40, net 60x34, rope 44x28, ship 72x40, wheel 40x40) + an unlisted `ship_wip.png` 80x80. → cell 74x42, 4 cols × 3 rows → 296x126 → **512x128**.
- Market set: 12 × 32x32 → 256x128 (pre-baked, committed, stale decoy).
- Harbor_old: 6 PNGs, no manifest → produces the report-4 style error if chosen.

### 1.7 Import-state plant
`Art/UI/harbor_map.png` 2048x1024: Default platform maxTextureSize 2048, textureType Sprite, mipmaps ON, filter Bilinear; **Standalone platform override: maxTextureSize 256** (compression Normal). Imported size on Standalone = **256x128**. MainMenu `MapPanel/Map` displays it at 1600x800.

### 1.8 Compile-break variant (applied only for T2, from harness dir `variants/T2/`)
- Replace `Scripts/Runtime/Tides/TideClock.cs` with a version whose `NormalizedHeight(float h)` calls `TideMath.Lerp01(low, high, h)` → CS0117 (TideMath only has `InverseLerpClamped(a,b,v)`, same intended semantics).
- Add new file `Scripts/Editor/Lighting/HarborLightBaker.cs` (namespace `Saltmarsh.EditorTools`, `[MenuItem("Tools/Saltmarsh/Lighting/Bake Harbor Light Budget")] public static void Bake()`, uses `Saltmarsh.Weather.WeatherProfile`) → CS0246 because Saltmarsh.Editor.asmdef does not reference Saltmarsh.Weather.
- This mimics "a merge added a file and used an old API". HYPOTHESIS: a shell-only `dotnet build` on a stale `.csproj` may not include the new file and may report success; builder verifies and records in the answer key.

### 1.9 Tests (Saltmarsh.Tests.EditMode) — 11 tests: 9 pass, 1 fail, 1 ignored
- TideMathTests (3 pass), TideClockTests.NormalizedHeight_AtHalfTide_IsHalf (pass), CargoIdTests (2 pass + `Parse_HandlesUnicodeSuffix` `[Ignore("Waiting on design for unicode ids")]`).
- PriceTableTests: `BulkDiscount_StartsAtHundredUnits` **fails** (`Assert.AreEqual(0.85f, PriceTable.UnitPriceMultiplier(100))` → "Expected: 0.85f But was: 0.9f"); 3 others pass (99→0.9, 20→0.9, 19→1.0).
- `PriceTable.UnitPriceMultiplier(int units) => units > BulkThreshold ? 0.85f : (units >= 20 ? 0.9f : 1f); BulkThreshold = 100`.

### 1.10 Builder acceptance (before freeze)
For every task: oracle on pristine reset → expected FAIL (write tasks) / harness self-test with the key answer → PASS (read-only tasks); oracle after applying the reference solution (§4) → PASS; a human eyeballs both. Three dry runs per task end-to-end (P0-08b). Record all `<KEY_*>` values.

---------------------------------------------------------------------
## 2. Tasks

Legend: **RO** = read-only, **W** = writes. ★ = also replicable on a third-party open-source project (template in §3). Source = which real report the task is modelled on.

| ID | Short name | RO/W | Difficulty | Source | ★ |
|---|---|---|---|---|---|
| T1 | Menu buttons don't react | W (scene) | M | R3 (Canvas/Image setup), C §3.5-1 | |
| T2 | Compile errors after merge | W (C#, asmdef) | M–H | R1, R2 (compile verification) | |
| T3 | Who uses Lantern_Post | RO | M | C §3.5-3, R3 ("Unity-aware search") | ★ |
| T4 | Quest board shows nothing | RO | M | R3 (serialized refs) | |
| T5 | Console triage | RO | L–M | R1/R2 (console/verification need) | |
| T6 | Run icon atlas bake + confirm import | W (via tool) | M–H | R4 | |
| T7 | Pier 3 lantern shadows off | W (scene) | M–H | R3 (scene edits), persona | |
| T8 | Blurry map — import or UI? | RO | M | R3/R4 (import state) | ★ |
| T9 | Silent buoy bell (deep hierarchy) | RO | H | persona, large scene | ★ |
| T10 | Failing edit-mode test | W (C#) | L–M | R1/R2 | |

### Task-start state (set by reset, §5)
T1, T8: MainMenu.unity open. T2–T7, T9, T10: Harbor.unity open. All: Edit mode, console seeded per §1.4 (T2: plus compile errors), selection empty.

---

### T1 — Menu buttons don't react (W)
**Prompt (frozen):**
> On the main menu scene none of the buttons react when I click them in Play mode — not even Play. Figure out why and fix it. Keep the vignette look on the menu as it is, and save the scene when you're done.

**Required end state:** MainMenu.unity saved such that a pointer at PlayButton's centre hits PlayButton (raycast reaches MenuCanvas); VignetteOverlay still visible on top (exists, active, Image enabled, alpha ≥ 0.3, FxCanvas sortingOrder > MenuCanvas sortingOrder); PlayButton wiring intact; CreditsButton still non-interactable.

**Oracle (static, required):**
```csharp
var s = OpenFromDisk(FX+"Scenes/MainMenu.unity", out var dirty);
var ov = F(s, "FxCanvas/VignetteOverlay"); if (ov == null || !ov.activeInHierarchy) return "FAIL:overlay removed/inactive";
var img = ov.GetComponent<Image>(); if (img == null || !img.enabled || img.color.a < 0.3f) return "FAIL:vignette look changed";
var fx = F(s, "FxCanvas").GetComponent<Canvas>(); var menu = F(s, "MenuCanvas").GetComponent<Canvas>();
if (fx.sortingOrder <= menu.sortingOrder) return "FAIL:vignette no longer drawn over menu";
bool groupsBlock = ov.GetComponentsInParent<CanvasGroup>(true).All(g => g.blocksRaycasts);
var gr = fx.GetComponent<GraphicRaycaster>();
bool stillBlocks = img.raycastTarget && groupsBlock && gr != null && gr.enabled;
if (stillBlocks) return "FAIL:overlay still blocks raycasts";
var play = F(s, "MenuCanvas/MainPanel/Buttons/PlayButton").GetComponent<Button>();
if (!play.interactable || play.onClick.GetPersistentEventCount() != 1 || play.onClick.GetPersistentMethodName(0) != "OnPlayPressed") return "FAIL:play button wiring changed";
if (F(s, "MenuCanvas/MainPanel/Buttons/CreditsButton").GetComponent<Button>().interactable) return "FAIL:credits button changed";
return "PASS";
```
**Oracle (dynamic, required):** harness opens MainMenu from disk, sets Game view 1920x1080, enters Play mode, waits 3 frames, runs:
```csharp
var b = GameObject.Find("PlayButton"); var rt = (RectTransform)b.transform;
var pos = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = pos };
var res = new List<UnityEngine.EventSystems.RaycastResult>(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, res);
return res.Count > 0 && res[0].gameObject.transform.IsChildOf(b.transform) ? "PASS" : "FAIL:first hit " + (res.Count > 0 ? res[0].gameObject.name : "none");
```
Both must PASS.
**False success:** claims fixed but (a) scene not saved, (b) overlay deleted/disabled/made transparent or moved under the menu, (c) "fix" was making CreditsButton interactable / rewiring onClick / adding a second EventSystem, (d) changed only something irrelevant (StandaloneInputModule swap, CanvasGroup on MainPanel).
**Difficulty:** M. Agents that only read scene YAML can solve it in principle; agents that never consider cross-canvas raycast order cannot.

---

### T2 — Compile errors after merge (W)
**Prompt (frozen):**
> I just merged a branch and Unity now shows compile errors. Fix them so the project compiles in Unity again. Keep the new code from the merge and don't change behaviour.

**Required end state:** Unity compile succeeds for all assemblies; `HarborLightBaker` still exists and still uses `WeatherProfile`; `WeatherProfile` still lives in assembly `Saltmarsh.Weather`; TideClock behaviour unchanged.

**Oracle:** harness runs `AssetDatabase.Refresh(); UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();`, waits for compile + reload to finish (timeout 180 s), then:
```csharp
if (EditorUtility.scriptCompilationFailed) return "FAIL:Unity compile still failing";
if (Type.GetType("Saltmarsh.Weather.WeatherProfile, Saltmarsh.Weather") == null) return "FAIL:WeatherProfile moved/removed";
var baker = Type.GetType("Saltmarsh.EditorTools.HarborLightBaker, Saltmarsh.Editor");
if (baker == null || baker.GetMethod("Bake", System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static) == null) return "FAIL:merged HarborLightBaker removed";
var src = File.ReadAllLines(FX+"Scripts/Editor/Lighting/HarborLightBaker.cs").Where(l => !l.TrimStart().StartsWith("//"));
if (!src.Any(l => l.Contains("WeatherProfile")) || File.ReadAllText(FX+"Scripts/Editor/Lighting/HarborLightBaker.cs").Contains("#if false")) return "FAIL:merged code disabled";
return "PASS";
```
Then harness runs EditMode tests (TestRunnerApi, assembly Saltmarsh.Tests.EditMode): require TideMathTests 3/3 and TideClockTests 1/1 pass, and overall result equal to baseline (9 pass, 1 fail = BulkDiscount_StartsAtHundredUnits, 1 ignored). Test files sha256 unchanged.
**False success:** claims it compiles because a shell build passed / old assemblies still loaded, while Unity reports errors; deleted or `#if false`'d HarborLightBaker; moved WeatherProfile; aliased `Lerp01` with different semantics (caught by TideClockTests); edited tests.
**Difficulty:** M–H (two assemblies; the asmdef reference fix is not visible from C# alone).

---

### T3 ★ — Who uses the Lantern_Post prefab (RO)
**Prompt (frozen):**
> I'm about to change the Lantern_Post prefab in Prefabs/Props. Before I touch it, tell me what in the project uses it: for each scene, how many copies are placed directly in that scene (not counting ones that come inside other prefabs or from prefab variants), and separately which other prefabs or assets reference it. Don't change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: <Scene>=<count>; <Scene>=<count>; assets=<file name>, <file name>, ...`

**Required answer:** `Harbor=17; Docks_Test=3; assets=StreetKit.prefab, Lantern_Post_Rusty.prefab, LanternSet.asset` (order-insensitive; MainMenu absent or `=0`).
**Oracle:** parse ANSWER: scene counts dict must have Harbor=17, Docks_Test=3, any other scene absent or 0; `assets` set (file names, case-insensitive, `.prefab`/`.asset` extension optional) must equal exactly {StreetKit, Lantern_Post_Rusty, LanternSet}. Read-only integrity check. Ground truth self-check (run at build time, also usable on third-party):
```csharp
var src = AssetDatabase.LoadAssetAtPath<GameObject>(FX+"Prefabs/Props/Lantern_Post.prefab");
var s = OpenFromDisk(FX+"Scenes/Harbor.unity", out _);
int n = All(s).Count(g => PrefabUtility.IsOutermostPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromSource(g) == src);
var path = AssetDatabase.GetAssetPath(src);
var users = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p != path && AssetDatabase.GetDependencies(p, false).Contains(path));
return n == 17 ? "PASS " + string.Join(",", users) : "FAIL:" + n;
```
**False success:** 19 or 21+ for Harbor (name search incl. legacy decoy / nested / variant), counting legacy `Art/Legacy/Lantern_Post.prefab` users, missing the variant or the ScriptableObject list, or modifying files.
**Difficulty:** M.
**Sufficiency questions:**
- SQ3.1: How many direct instances of Props/Lantern_Post.prefab in Harbor are named something not starting with "Lantern_Post", and what are their names? → 5: `lamp`, `Light Pole`, `StreetLight`, `lantern`, `lamp_03`.
- SQ3.2: How many objects in Harbor named exactly "Lantern_Post" are instances of a *different* prefab, and which prefab? → 4, `Art/Legacy/Lantern_Post.prefab`.

---

### T4 — Quest board shows nothing (RO)
**Prompt (frozen):**
> In the Harbor scene the quest board on the market square doesn't show any notices when I play. Look at how it's set up and tell me what's wrong. Don't fix it yet.
> Finish your reply with one line in exactly this form:
> `ANSWER: object=<hierarchy path>; component=<component type>; field=<field name>; problem=<short description>`

**Required answer:** object path ends with `QuestBoard_Stand/Board` (with or without leading `Harbor/Town/Square_Market/`); component `QuestBoard`; field `noticeTemplate` (accept "Notice Template"); problem says the reference points to an asset that no longer exists.
**Oracle:** parse ANSWER; `object` normalised (trim, collapse spaces around `/`, strip optional `Harbor.unity:` prefix) ends with `QuestBoard_Stand/Board`; `component` == `QuestBoard` (case-insensitive); `field` matches `/notice\s*template/i`; `problem` matches `/missing|deleted|no longer exist|does not exist|broken|nonexist/i`. Field `headerIcon` named as *the* problem → FAIL. Read-only integrity check.
Ground-truth self-check: `File.ReadAllText(FX+"Scenes/Harbor.unity").Contains("guid: <KEY_DELETED_GUID>") && AssetDatabase.GUIDToAssetPath("<KEY_DELETED_GUID>") == ""`.
**False success:** blaming the Tavern board, `headerIcon` (None by design, marked optional), `maxNotices`, or "reference is null/unassigned" without recognising it points to a deleted asset (the `problem` regex requires that), or editing the scene.
**Difficulty:** M.
**Sufficiency questions:**
- SQ4.1: Which serialized fields on the Square_Market QuestBoard do not resolve to an object, and is each "None" or "Missing"? → `noticeTemplate` Missing; `headerIcon` None.
- SQ4.2: Which prefab does the other QuestBoard in Harbor use as its notice template, and on which object is it? → `Notice_Card_v2.prefab`, `Harbor/Town/Tavern/Interior/notice_board (1)`.

---

### T5 — Console triage (RO)
**Prompt (frozen):**
> The Unity console is flooded since I pulled. Which of these are actual errors I need to fix? Give me each distinct error once, with where it comes from in our code. Don't change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: count=<number of distinct errors>; errors=<file>:<line> | <file>:<line> | ...`

**Required answer:** count=3; errors = `ManifestValidator.cs:47`, `LocalizationCheck.cs:31`, `HarborAmbience.cs:58` (order-insensitive; paths may be full or file name only).
**Oracle:** parse ANSWER; count == 3; the three `file:line` tokens present (match on file name + line); no token naming `AudioBankCheck`, `SaltmarshStartupChecks`, Tides or `[Net]` as an error. For the NRE also accept `AmbienceWarmup.cs:<KEY_AMBIENCE_WARMUP_LINE>` **only if** `HarborAmbience.cs:58` is also present (origin required). Read-only integrity check (console clearing is allowed and not a mutation).
**False success:** reporting warnings (AudioBank/Tides/"[Net] Socket error budget") as errors; reporting 7 errors (no dedup); missing the exception because it is a LogException rather than LogError; reporting only the logging site of E3, not the origin.
**Difficulty:** L–M (volume/noise, not reasoning).
**Sufficiency questions:**
- SQ5.1: How many times does the missing localization key error appear, and what key/table? → 5; `menu.play.subtitle` in `UI_en`.
- SQ5.2: How many warning entries are in the console, and how many distinct warning messages? → 421 entries; 302 distinct texts (300 AudioBank variants + Tides + Net). Accept "3 distinct warning sources" as an equivalent grouping.

---

### T6 — Run the icon atlas bake and confirm import (W, via existing tooling)
**Prompt (frozen):**
> We added new harbor icons. Run our existing icon atlas bake for the Harbor icon set (not Market), make sure Unity has finished importing the result, and tell me the atlas size it produced. Use the project's tool, don't recreate the atlas by hand.
> Finish your reply with one line in exactly this form:
> `ANSWER: <width>x<height>`

**Required end state:** `Art/Icons/Generated/IconAtlas_Harbor.png` + `.json` produced by `IconAtlasBaker` and imported with postprocessor settings; answer `512x128`.
**Oracle:**
```csharp
var p = FX+"Art/Icons/Generated/IconAtlas_Harbor.png";
var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) return "FAIL:not imported";
var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p); if (t.width != 512 || t.height != 128) return "FAIL:size " + t.width + "x" + t.height;
if (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || ti.filterMode != FilterMode.Point) return "FAIL:import settings not from project postprocessor";
if (SessionState.GetString("Saltmarsh.IconAtlas.LastBake", "") != "Harbor|10|512x128") return "FAIL:project tool did not complete a Harbor bake in this session";
var json = File.ReadAllText(FX+"Art/Icons/Generated/IconAtlas_Harbor.json");
if (!json.Contains("\"generator\":\"IconAtlasBaker/1.3\"") || json.Contains("ship_wip")) return "FAIL:json not from tool";
if (Sha(FX+"Art/Icons/Generated/IconAtlas_Market.png") != "<SHA_MARKET_PNG>") return "FAIL:market atlas changed";
return "PASS";
```
plus ANSWER == `512x128` (whitespace-insensitive, `×` accepted). Editing `IconAtlasSettings.asset` is allowed (not checked).
**False success:** answer given before import finished (file absent/unimported at oracle time), PNG regenerated outside Unity (report-4 path; fails SessionState marker and/or json), baked Harbor_old or Market, reports Market's 256x128, answer without running the tool.
**Difficulty:** M–H (discover the tool, notice it reads its folder from a settings asset, run it headless, wait for async import).

---

### T7 — Pier 3 lantern shadows off (W)
**Prompt (frozen):**
> The lantern posts on Pier 3 in the Harbor scene cast shadows and it's too expensive. Turn shadows off for the lanterns on Pier 3 only and set their light intensity to 1.5. Don't change the lantern prefab itself or any other lanterns. Save the scene.

**Required end state:** the Lamp Light of the 4 Lantern_Post instances under `Harbor/Waterfront/Piers/Pier_03` (paths 4–7 in §1.2a) has shadows None and intensity 1.5 as instance overrides in saved Harbor.unity; everything else unchanged.
**Oracle:**
```csharp
var s = OpenFromDisk(FX+"Scenes/Harbor.unity", out var dirty);
string[] pier3 = { "Harbor/Waterfront/Piers/Pier_03/Lamps/Lantern_Post", "Harbor/Waterfront/Piers/Pier_03/Lamps/lamp",
  "Harbor/Waterfront/Piers/Pier_03/Deck/bollard_row (1)/Lantern_Post (3)", "Harbor/Waterfront/Piers/Pier_03/End/Light Pole" };
foreach (var p in pier3) { var go = F(s, p); if (go == null) return "FAIL:missing " + p;
  var l = go.transform.Find("Head/Lamp").GetComponent<Light>();
  if (l.shadows != LightShadows.None || Mathf.Abs(l.intensity - 1.5f) > 0.001f) return "FAIL:not changed " + p; }
var work = F(s, "Harbor/Waterfront/Piers/Pier_03/End/WorkLight").GetComponent<Light>();
if (work.shadows != LightShadows.Hard || Mathf.Abs(work.intensity - 3f) > 0.001f) return "FAIL:WorkLight changed";
var props = AssetDatabase.LoadAssetAtPath<GameObject>(FX+"Prefabs/Props/Lantern_Post.prefab");
var pl = props.transform.Find("Head/Lamp").GetComponent<Light>();
if (pl.shadows != LightShadows.Soft || Mathf.Abs(pl.intensity - 2.2f) > 0.001f) return "FAIL:prefab asset changed";
foreach (var l in All(s).Select(g => g.GetComponent<Light>()).Where(l => l != null && l.name == "Lamp")) {
  var path = PathOf(l.gameObject); if (pier3.Any(p => path.StartsWith(p + "/"))) continue;
  var root = PrefabUtility.GetCorrespondingObjectFromOriginalSource(l.gameObject);
  bool legacy = AssetDatabase.GetAssetPath(root).Contains("/Art/Legacy/");
  bool rusty = PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetNearestPrefabInstanceRoot(l.gameObject)) is GameObject src && src.name == "Lantern_Post_Rusty";
  float want = legacy ? 1.2f : rusty ? 1.8f : 2.2f; var sh = legacy ? LightShadows.None : LightShadows.Soft;
  if (l.shadows != sh || Mathf.Abs(l.intensity - want) > 0.001f) return "FAIL:other lantern changed " + path; }
if (All(s).Count() != <KEY_TOTAL>) return "FAIL:objects added/removed";
return "PASS";
```
**False success:** edited the prefab asset (all lanterns change), changed only the 2 objects named "Lantern_Post*" and missed `lamp`/`Light Pole`, changed WorkLight, scene not saved, unpacked/replaced instances (object count or structure changes).
**Difficulty:** M–H.

---

### T8 ★ — Blurry harbor map: import or UI? (RO)
**Prompt (frozen):**
> The harbor map on the menu's map panel looks blurry. Before I change anything: is that coming from how the texture is imported or from the UI setup? Tell me the cause and the size the texture actually ends up at in the editor right now. Don't modify anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: cause=<short description>; imported_size=<width>x<height>`

**Required answer:** imported_size=256x128; cause attributes it to the import max size for the current (Standalone/PC) platform being 256 while the source is 2048x1024.
**Oracle:** parse ANSWER; `imported_size` == `256x128`; `cause` matches `/(max(imum)?\s*(texture\s*)?size|256)/i` AND matches `/standalone|platform|override|pc|mac|windows/i`. Read-only integrity check. Ground truth: `AssetDatabase.LoadAssetAtPath<Texture2D>(FX+"Art/UI/harbor_map.png")` → width 256, height 128 (active target Standalone).
**False success:** reading only Default settings (answers 2048x1024 / "import is fine, it's the UI scaling"), blaming mipmaps or bilinear filter alone, or changing the import settings.
**Difficulty:** M.
**Sufficiency questions:**
- SQ8.1: What are the source pixel dimensions and the imported dimensions on the active build target? → 2048x1024 → 256x128.
- SQ8.2: Which import setting differs between Default and the active platform, and its two values? → Max Size: 2048 (Default) vs 256 (Standalone override).

---

### T9 ★ — Silent buoy bell, deep in a large hierarchy (RO)
**Prompt (frozen):**
> In the Harbor scene there's a buoy bell that is set to play automatically when the scene starts, but we never hear it. Find that audio source and tell me why it doesn't play. Don't change anything.
> Finish your reply with one line in exactly this form:
> `ANSWER: path=<full hierarchy path of the object>; reason=<short reason>`

**Required answer:** path = `Harbor/Waterfront/Breakwater/Segment_C (2)/Markers/BuoyField_East/buoy.r12/Mount/BellAudio`; reason: ancestor `BuoyField_East` is inactive.
**Oracle:** parse ANSWER; normalised path (trim, collapse whitespace around `/`, optional `Harbor.unity:`/`Harbor.unity/` prefix stripped) equals the target exactly; `reason` contains `BuoyField_East` and matches `/inactive|disabled|not active|deactivated|turned off/i`. Read-only integrity check.
Ground truth self-check:
```csharp
var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FX+"Audio/bell_buoy.wav");
var s = OpenFromDisk(FX+"Scenes/Harbor.unity", out _);
var hits = All(s).Select(g => g.GetComponent<AudioSource>()).Where(a => a != null && a.clip == clip && a.playOnAwake && a.enabled && !a.gameObject.activeInHierarchy).Select(a => PathOf(a.gameObject)).ToList();
return hits.Count == 1 && hits[0] == "Harbor/Waterfront/Breakwater/Segment_C (2)/Markers/BuoyField_East/buoy.r12/Mount/BellAudio" ? "PASS" : "FAIL:" + string.Join(";", hits);
```
**False success:** naming `buoy.w04` (active, plays), `buoy.w02` (component disabled but playOnAwake false), a wrong `Segment_C (n)`, a truncated path, or giving a reason like "volume/clip missing".
**Difficulty:** H (needs component+property search over ~1.2k objects including inactive ones, then ancestry).
**Sufficiency questions:**
- SQ9.1: How many AudioSources in Harbor use the bell_buoy clip, and which of them have Play On Awake enabled? → 18; `buoy.r12` and `buoy.w04` BellAudio.
- SQ9.2: For the target object, what is its depth (root = 1) and which ancestor is the highest inactive one? → 9; `BuoyField_East`.

---

### T10 — Failing edit-mode test (W)
**Prompt (frozen):**
> One of our edit-mode tests is failing. Find out which one and fix the code under test so it passes. Don't change the tests.

**Required end state:** all non-ignored tests in Saltmarsh.Tests.EditMode pass; test sources unchanged.
**Oracle:** harness waits for compile; `EditorUtility.scriptCompilationFailed == false`; sha256 of every file under `Tests/EditMode/` (incl. asmdef) equals `<SHA_TESTS_*>`; TestRunnerApi EditMode run filtered to assembly `Saltmarsh.Tests.EditMode` → exactly: total 11, passed 10 (the 9 original passes + `BulkDiscount_StartsAtHundredUnits`), failed 0, skipped/ignored 1 (`Parse_HandlesUnicodeSuffix`). `PriceTable.cs` differs from pristine.
**False success:** edited the test or its expected value, removed/ignored the failing test, "fixed" the ignored test, broke another PriceTable test (e.g. making 99 units discounted), claims pass without a Unity test run when compile actually fails.
**Difficulty:** L–M.

---------------------------------------------------------------------
## 3. Third-party replication templates (★ tasks)

Apply to one licence-checked open-source Unity project (selection is P0-09's job; candidates UNKNOWN here). The generic prompt keeps the same wording with names substituted; ground truth comes from the same self-check snippets with parameters.
- **T3★** Pick a prefab with ≥5 direct scene instances in ≥1 scene and ≥1 referencing prefab/asset; prefer one whose name collides with another prefab or whose instances were renamed. Prompt = T3 with prefab path substituted. Truth = T3 self-check per scene + `GetDependencies(p,false)` scan.
- **T8★** Pick a texture whose active-platform max size or compression differs from Default (or whose imported size < source size). Prompt = T8 with object/texture named by its visible role. Truth = imported `Texture2D.width/height` + `TextureImporter.GetPlatformTextureSettings(<active>)`.
- **T9★** Pick a component+property condition (e.g. AudioSource/Light/Collider with a specific setting) that is satisfied by ≥2 objects of which exactly one is at depth ≥6 under an inactive ancestor. If none exists naturally, use a different factual deep question with a unique answer (e.g. "which object at depth ≥6 has component X with property Y=Z"), validated by an `All(s)` filter that returns exactly one path.
Third-party runs are always read-only (no reset beyond `git clean`), so only RO tasks are templated.

---------------------------------------------------------------------
## 4. Reference solutions (harness-only; used for builder acceptance, never shown to agents)
- T1: set `FxCanvas/VignetteOverlay` Image.raycastTarget = false; save. (Alternatives that also pass: CanvasGroup on overlay with blocksRaycasts=false; disabling FxCanvas GraphicRaycaster.)
- T2: TideClock → `TideMath.InverseLerpClamped(low, high, h)`; add `Saltmarsh.Weather` to `Saltmarsh.Editor.asmdef` references.
- T6: set IconAtlasSettings.sourceFolder to `.../Source/Harbor`, run the menu item (or `IconAtlasBaker.Bake("Assets/BenchFixture/Art/Icons/Source/Harbor")`), wait for import.
- T7: on each of the 4 Pier_03 instances set Lamp Light shadows None, intensity 1.5 (instance overrides); save.
- T10: `units >= BulkThreshold`.

---------------------------------------------------------------------
## 5. Reset procedure (between every run; harness-owned)

The fixture is its own Unity project/repo, tagged `fixture-v1`. The arm's own package/config is installed per arm via a recorded `Packages/manifest.json` + `packages-lock.json` + arm files; the harness owns that list.

1. **Stop the agent process.** If the Editor is in Play mode → exit and wait for Edit mode. Close any Prefab Stage (`StageUtility.GoToMainStage()`).
2. **Files:** `git -C <fixture> reset --hard fixture-v1 && git -C <fixture> clean -fdx -e Library -e Temp -e Logs -e UserSettings -e obj`. Then re-apply the arm's package/config files (manifest, lock, AGENTS.md/skills per arm). This restores `Assets/` (incl. any files an agent created anywhere, e.g. `Assets/Editor/*.cs`), `ProjectSettings/`, `Packages/`, scenes, `.meta` files, IconAtlasSettings, generated atlases.
3. **Editor sync:** open an empty new scene (`EditorSceneManager.NewScene(EmptyScene, Single)`, discarding unsaved changes), `AssetDatabase.Refresh(ForceSynchronousImport)`, request script compilation, wait until not compiling/updating and the domain reload finished. Abort the run (infrastructure failure, not a task failure) if compile fails on the pristine fixture.
4. **Editor state:** active build target Standalone (assert, do not switch silently — switching reimports); Game view 1920x1080; Interaction Mode "No Throttling" (record); clear selection; `SessionState.EraseString("Saltmarsh.IconAtlas.LastBake")`; `MenuController.LastAction = null` irrelevant (static reset on reload).
5. **Console seed:** clear console (`LogEntries.Clear` via reflection), then `EditorUtility.RequestScriptReload()` and wait for reload + one `delayCall` tick, so §1.4 noise is emitted exactly once. Assert counts: 7 errors, 421 warnings, 40 logs (± Unity's own unrelated lines; record).
6. **Task variant:** T2 only — copy `variants/T2/TideClock.cs` and `variants/T2/HarborLightBaker.cs(.meta)` into place, `AssetDatabase.Refresh()`, wait for the (failing) compile to finish; assert `EditorUtility.scriptCompilationFailed == true` and exactly 2 compiler errors (CS0117 in TideClock.cs, CS0246 in HarborLightBaker.cs).
7. **Start scene:** open per "Task-start state" (§2). Scene must be non-dirty.
8. **Integrity assert:** sha256 of a fixed manifest of fixture files equals the frozen list; the task's oracle (write tasks) returns FAIL on this pristine state. Record `Library/` warm/cold and Editor uptime.
9. Release the harness lock (P0-08c) only after the post-run oracle has executed; oracles always run **before** the next reset.

Not restored (by design): `Library/` (kept warm; cold runs are flagged), Editor process (restart only on crash or every 20 runs, recorded), EditorPrefs/user preferences other than those set in step 4.

---------------------------------------------------------------------
## 6. Coverage map (requirement → task)
| Requested category | Task |
|---|---|
| Inspect why UI is wrong | T1 (diagnose + fix), T8 (UI vs import) |
| Edit C# and verify compile state | T2, T10 |
| Inspect object/component relationships | T4 |
| Find references | T3 ★ |
| Diagnose broken Editor state (console triage) | T5 (and T2's starting state) |
| Run existing editor tooling and confirm import | T6 |
| Simple scene/object change and verify | T7 (T1 also writes) |
| Inspect import/result state | T8 ★, T6 |
| Large-hierarchy deep irregular lookup | T9 ★ (T7 also needs renamed instances) |
| Real-session derived (P0-09 requirement) | T1/T8 (R3), T2/T10 (R1/R2), T6 (R4) |
