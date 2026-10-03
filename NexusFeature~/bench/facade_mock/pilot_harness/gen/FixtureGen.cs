// Research-only fixture generator (P0-16 pilot). Copied into the fixture project temporarily,
// run once, then deleted. Never shipped. Follows TASKS_v1.md §1 (subset: no icon-atlas tool, see PREREG).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Saltmarsh.Audio;
using Saltmarsh.Data;
using Saltmarsh.Props;
using Saltmarsh.Quests;
using Saltmarsh.Tides;
using Saltmarsh.UI;

public static class FixtureGen
{
    const string FX = "Assets/BenchFixture/";
    static System.Random R;
    static Material[] Mats;
    static Mesh CubeMesh, CylMesh, SphereMesh;
    static int _filler;

    static readonly string[] Props = { "Crate", "crate_small", "Barrel", "Barrell", "SM_Rope_Coil_A", "rope_coil", "Net_Hanging", "fish_box", "Pallet", "pallet_broken", "Sack", "sacks_pile", "Bollard", "Cleat", "Ladder", "Plank_Long", "plank", "Tarp", "Lobster_Trap", "Oar", "Bucket", "Anchor", "chain_link", "Keg" };
    static readonly string[] Struct = { "Root", "Mesh", "LOD0", "LOD1", "Collider", "colliders", "pivot", "Visual", "Geo", "GameObject", "Group", "misc", "__tmp", "-- props --", "Offset", "Socket" };
    static readonly string[] Build = { "House_A", "house_fisher_03", "window", "door", "roof", "chimney", "Stairs", "Shed", "fence", "Porch", "wall_segment", "Gutter" };
    static readonly string[] Nature = { "Tree_Pine_01", "tree_birch", "Bush", "rock_big", "Rock_Small", "Reeds", "grass_clump", "Driftwood" };

    static string Pick(string[] a) => a[R.Next(a.Length)];

    static string FillerName(int depth)
    {
        string b = depth <= 3 ? (R.Next(3) == 0 ? Pick(Build) : Pick(Struct)) : R.Next(4) switch { 0 => Pick(Struct), 1 => Pick(Build), 2 => Pick(Nature), _ => Pick(Props) };
        switch (R.Next(6))
        {
            case 0: return b;
            case 1: return b + " (" + (1 + R.Next(9)) + ")";
            case 2: return b + "_" + (1 + R.Next(20)).ToString("00");
            case 3: return b + "_" + (char)('A' + R.Next(5));
            case 4: return b + "_old";
            default: return b.ToLowerInvariant();
        }
    }

    static string PathOfT(Transform t) { var p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }
    static int Depth(Transform t) { int d = 1; while (t.parent != null) { d++; t = t.parent; } return d; }

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((float)R.NextDouble() * 4 - 2, 0, (float)R.NextDouble() * 4 - 2);
        return go;
    }

    static void Decorate(GameObject go)
    {
        int roll = R.Next(100);
        if (roll < 40)
        {
            go.AddComponent<MeshFilter>().sharedMesh = R.Next(3) == 0 ? CylMesh : CubeMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats[R.Next(Mats.Length)];
        }
        if (R.Next(100) < 10) go.AddComponent<BoxCollider>();
        if (R.Next(100) < 5) go.SetActive(false);
    }

    // Ensures a path under scene root exists (group nodes), returns the leaf.
    static readonly Dictionary<string, GameObject> Nodes = new Dictionary<string, GameObject>();
    static GameObject Node(string path)
    {
        if (Nodes.TryGetValue(path, out var g)) return g;
        int slash = path.LastIndexOf('/');
        Transform parent = slash < 0 ? null : Node(path.Substring(0, slash)).transform;
        var go = new GameObject(slash < 0 ? path : path.Substring(slash + 1));
        if (parent != null) go.transform.SetParent(parent, false);
        Nodes[path] = go;
        return go;
    }

    // Random irregular filler under root until `count` filler objects were added.
    static void Fill(GameObject root, int count, int maxDepth = 9)
    {
        if (Depth(root.transform) >= maxDepth) return;
        var pool = new List<Transform> { root.transform };
        int added = 0, guard = 0;
        while (added < count && guard++ < count * 50)
        {
            var p = pool[R.Next(pool.Count)];
            int d = Depth(p);
            if (d >= maxDepth) continue;
            int kids = R.Next(100) < 3 ? 25 + R.Next(30) : 1 + R.Next(R.Next(2) == 0 ? 4 : 12);
            for (int k = 0; k < kids && added < count; k++)
            {
                var c = Child(p, FillerName(d + 1));
                Decorate(c);
                added++; _filler++;
                if (Depth(c.transform) < maxDepth && R.Next(100) < 55) pool.Add(c.transform);
            }
        }
    }

    // Adds a filler chain of `len` nodes below a random node at depth >= 4 in root, to populate depths 8-9.
    static void DeepChain(GameObject root, int targetDepth)
    {
        var cands = root.GetComponentsInChildren<Transform>(true).Where(t => Depth(t) >= 4 && Depth(t) <= 6 && !PrefabUtility.IsPartOfPrefabInstance(t)).ToList();
        if (cands.Count == 0) return;
        var p = cands[R.Next(cands.Count)];
        while (Depth(p) < targetDepth)
        {
            var c = Child(p, FillerName(Depth(p) + 1));
            Decorate(c);
            c.SetActive(true);
            _filler++;
            p = c.transform;
        }
        // a sibling at the bottom level
        var s = Child(p.parent, FillerName(Depth(p)));
        Decorate(s); _filler++;
    }

    static Light AddLight(GameObject go, LightType type, float intensity, LightShadows sh, float range = 6f)
    {
        var l = go.AddComponent<Light>();
        l.type = type; l.intensity = intensity; l.shadows = sh; l.range = range;
        return l;
    }

    static void MeshOn(GameObject go, Mesh m, int mat = 0)
    {
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mats[mat % Mats.Length];
    }

    static void WriteWav(string path, float freq, float seconds)
    {
        int rate = 22050, n = (int)(rate * seconds);
        using var bw = new BinaryWriter(File.Create(path));
        bw.Write("RIFF".ToCharArray()); bw.Write(36 + n * 2); bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray()); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
        bw.Write("data".ToCharArray()); bw.Write(n * 2);
        for (int i = 0; i < n; i++) bw.Write((short)(Math.Sin(2 * Math.PI * freq * i / rate) * 6000 * (1 - (double)i / n)));
    }

    static void WritePng(string path, int w, int h, Func<int, int, Color> f)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
    }

    static T Asset<T>(string path, Action<T> init) where T : ScriptableObject
    {
        var a = ScriptableObject.CreateInstance<T>();
        init(a);
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    static GameObject Inst(GameObject prefab, string parentPath, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(Node(parentPath).transform, false);
        go.transform.localPosition = new Vector3((float)R.NextDouble() * 6 - 3, 0, (float)R.NextDouble() * 6 - 3);
        go.name = name;
        return go;
    }

    static int Count(Scene s) => s.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);

    public static string Run(string keyPath)
    {
        R = new System.Random(6000317);
        Nodes.Clear(); _filler = 0;
        foreach (var d in new[] { "Scenes", "Prefabs/Props", "Prefabs/UI", "Art/Legacy", "Art/UI", "Audio", "Data/Manifests", "Data/Loc", "Data/Ambience", "Data/Props", "Data/Tides", "Materials" })
            Directory.CreateDirectory(FX + d);

        // ---------- raw assets ----------
        WriteWav(FX + "Audio/bell_buoy.wav", 880, 0.6f);
        WriteWav(FX + "Audio/gull_01.wav", 1400, 0.3f); WriteWav(FX + "Audio/gull_02.wav", 1500, 0.3f); WriteWav(FX + "Audio/gull_03.wav", 1300, 0.3f);
        WriteWav(FX + "Audio/waves_loop.wav", 110, 1f); WriteWav(FX + "Audio/wind_loop.wav", 220, 1f);
        WritePng(FX + "Art/UI/vignette.png", 512, 512, (x, y) => { float dx = (x - 256f) / 256f, dy = (y - 256f) / 256f; float r = Mathf.Sqrt(dx * dx + dy * dy); return new Color(0, 0, 0, Mathf.Clamp01((r - 0.5f) * 1.6f)); });
        WritePng(FX + "Art/UI/harbor_map.png", 2048, 1024, (x, y) => new Color(0.2f + 0.5f * x / 2048f, 0.4f + 0.3f * y / 1024f, 0.6f, 1));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var vi = (TextureImporter)AssetImporter.GetAtPath(FX + "Art/UI/vignette.png");
        vi.textureType = TextureImporterType.Sprite; vi.alphaIsTransparency = true; vi.SaveAndReimport();
        var mi = (TextureImporter)AssetImporter.GetAtPath(FX + "Art/UI/harbor_map.png");
        mi.textureType = TextureImporterType.Sprite; mi.mipmapEnabled = true; mi.filterMode = FilterMode.Bilinear; mi.maxTextureSize = 2048;
        mi.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = 256, textureCompression = TextureImporterCompression.Compressed, format = TextureImporterFormat.Automatic });
        mi.SaveAndReimport();

        var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        string[] matNames = { "M_Wood_Weathered", "M_Stone_Wet", "M_Metal_Painted", "M_Rope", "M_Rust" };
        Color[] matCols = { new Color(.45f, .33f, .22f), new Color(.4f, .42f, .45f), new Color(.2f, .35f, .5f), new Color(.7f, .62f, .45f), new Color(.5f, .25f, .12f) };
        Mats = new Material[5];
        for (int i = 0; i < 5; i++) { var m = new Material(lit) { color = matCols[i] }; AssetDatabase.CreateAsset(m, FX + "Materials/" + matNames[i] + ".mat"); Mats[i] = m; }
        CubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx"); CylMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"); SphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

        var bell = AssetDatabase.LoadAssetAtPath<AudioClip>(FX + "Audio/bell_buoy.wav");
        var gulls = new[] { "gull_01", "gull_02", "gull_03", "waves_loop" }.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(FX + "Audio/" + n + ".wav")).ToArray();

        // ---------- data assets ----------
        Asset<CargoManifest>(FX + "Data/Manifests/Northpier.asset", m =>
        {
            m.pier = "Northpier";
            string[] ids = { "crate_salt_01", "crate_salt_02", "barrel_cider_01", "net_bundle_03", "crate_fish_07", "rope_coil_02", "sack_flour_05", "crate_tea_01", "barrel_tar_02", "pallet_bricks_04", "crate_tin_03", "crate_fish_07", "lobster_pot_09" };
            foreach (var id in ids) m.entries.Add(new CargoEntry { id = id, quantity = 1 + id.Length % 7 });
        });
        Asset<CargoManifest>(FX + "Data/Manifests/Southpier.asset", m =>
        {
            m.pier = "Southpier";
            foreach (var id in new[] { "crate_coal_01", "crate_coal_02", "barrel_oil_01", "sack_salt_02", "crate_glass_01" }) m.entries.Add(new CargoEntry { id = id, quantity = 3 });
        });
        Asset<LocalizationTable>(FX + "Data/Loc/UI_en.asset", t =>
        {
            foreach (var kv in new[] { ("menu.play", "Set Sail"), ("menu.settings", "Settings"), ("menu.quit", "Quit"), ("menu.credits", "Credits"), ("map.title", "Saltmarsh Harbour") })
                t.entries.Add(new LocEntry { key = kv.Item1, text = kv.Item2 });
        });
        var amb = Asset<AmbienceProfile>(FX + "Data/Ambience/AmbienceProfile_Default.asset", p =>
        {
            p.layers.Add(new AmbienceLayer { clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FX + "Audio/waves_loop.wav"), volume = 0.7f });
            p.layers.Add(new AmbienceLayer { clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FX + "Audio/wind_loop.wav"), volume = 0.4f });
            p.layers.Add(null);
        });
        var tides = Asset<TideTable>(FX + "Data/Tides/Tides_Default.asset", t => { });

        // ---------- prefabs ----------
        GameObject Save(GameObject go, string path) { var p = PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); return p; }

        var lp = new GameObject("Lantern_Post"); MeshOn(lp, CylMesh, 2);
        var head = new GameObject("Head"); head.transform.SetParent(lp.transform, false); head.transform.localPosition = Vector3.up * 1.2f; MeshOn(head, CubeMesh, 2);
        var lamp = new GameObject("Lamp"); lamp.transform.SetParent(head.transform, false); AddLight(lamp, LightType.Point, 2.2f, LightShadows.Soft, 6f);
        var glass = new GameObject("Glass"); glass.transform.SetParent(head.transform, false); MeshOn(glass, SphereMesh, 1);
        var lanternPost = Save(lp, FX + "Prefabs/Props/Lantern_Post.prefab");

        var rustyInst = (GameObject)PrefabUtility.InstantiatePrefab(lanternPost);
        rustyInst.transform.Find("Head/Lamp").GetComponent<Light>().intensity = 1.8f;
        rustyInst.GetComponent<MeshRenderer>().sharedMaterial = Mats[4];
        rustyInst.name = "Lantern_Post_Rusty";
        var rusty = Save(rustyInst, FX + "Prefabs/Props/Lantern_Post_Rusty.prefab");

        var sk = new GameObject("StreetKit");
        var benchGo = new GameObject("bench"); benchGo.transform.SetParent(sk.transform, false); MeshOn(benchGo, CubeMesh, 0);
        var bin = new GameObject("bin"); bin.transform.SetParent(sk.transform, false); MeshOn(bin, CylMesh, 2);
        var nested = (GameObject)PrefabUtility.InstantiatePrefab(lanternPost); nested.transform.SetParent(sk.transform, false); nested.name = "Lantern_Post";
        var streetKit = Save(sk, FX + "Prefabs/Props/StreetKit.prefab");

        var by = new GameObject("Buoy"); MeshOn(by, SphereMesh, 2); by.AddComponent<Bobbing>();
        var buoyPrefab = Save(by, FX + "Prefabs/Props/Buoy.prefab");
        var cr = new GameObject("Crate"); MeshOn(cr, CubeMesh, 0); cr.AddComponent<BoxCollider>();
        var crate = Save(cr, FX + "Prefabs/Props/Crate.prefab");

        var legacy = new GameObject("Lantern_Post"); MeshOn(legacy, CylMesh, 1);
        var llamp = new GameObject("Lamp"); llamp.transform.SetParent(legacy.transform, false); AddLight(llamp, LightType.Point, 1.2f, LightShadows.None, 5f);
        var legacyPrefab = Save(legacy, FX + "Art/Legacy/Lantern_Post.prefab");

        GameObject NoticeCard(string name) { var n = new GameObject(name); MeshOn(n, CubeMesh, 0); n.transform.localScale = new Vector3(0.4f, 0.5f, 0.02f); return n; }
        var noticeOld = Save(NoticeCard("Notice_Card"), FX + "Prefabs/UI/Notice_Card.prefab");
        var noticeV2 = Save(NoticeCard("Notice_Card_v2"), FX + "Prefabs/UI/Notice_Card_v2.prefab");

        Asset<PropSet>(FX + "Data/Props/LanternSet.asset", p => { p.prefabs.Add(lanternPost); p.prefabs.Add(crate); });

        // ---------- Harbor.unity ----------
        var harbor = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var r in new[] { "Systems", "Main Camera", "Directional Light", "Global Volume", "Harbor", "NPCs", "Audio", "UI_World", "[Debug]" }) Node(r);
        Node("Main Camera").AddComponent<Camera>(); Node("Main Camera").tag = "MainCamera";
        AddLight(Node("Directional Light"), LightType.Directional, 1f, LightShadows.Soft);
        foreach (var s in new[] { "GameManager", "Pools/pool_crates", "Pools/pool_fx", "SaveSystem" }) Node("Systems/" + s);
        Node("Systems/TideClock").AddComponent<TideClock>().table = tides;

        // planted lanterns (TASKS_v1 §1.2a)
        string[] lanterns = {
            "Harbor/Waterfront/Piers/Pier_01/Lamps|Lantern_Post", "Harbor/Waterfront/Piers/Pier_01/Lamps|Lantern_Post (1)",
            "Harbor/Waterfront/Piers/Pier_02/Lamps|Lantern_Post", "Harbor/Waterfront/Piers/Pier_03/Lamps|Lantern_Post",
            "Harbor/Waterfront/Piers/Pier_03/Lamps|lamp", "Harbor/Waterfront/Piers/Pier_03/Deck/bollard_row (1)|Lantern_Post (3)",
            "Harbor/Waterfront/Piers/Pier_03/End|Light Pole", "Harbor/Waterfront/Piers/Pier_04/Lamps|Lantern_Post (1)",
            "Harbor/Waterfront/Promenade/lights|Lantern_Post", "Harbor/Waterfront/Promenade/lights|Lantern_Post (1)",
            "Harbor/Waterfront/Promenade/lights|StreetLight", "Harbor/Town/Square_Market/Corners/NE|Lantern_Post",
            "Harbor/Town/Square_Market/Corners/SW|lantern", "Harbor/Town/Alleys/alley_02/props|Lantern_Post",
            "Harbor/Town/Tavern/Exterior|Lantern_Post", "Harbor/Town/Tavern/Exterior|lamp_03", "[Debug]/LightingTest|Lantern_Post" };
        foreach (var l in lanterns) { var p = l.Split('|'); Inst(lanternPost, p[0], p[1]); }
        var work = Node("Harbor/Waterfront/Piers/Pier_03/End/WorkLight"); AddLight(work, LightType.Spot, 3f, LightShadows.Hard, 12f);
        Inst(streetKit, "Harbor/Town/Alleys/alley_03", "StreetKit"); Inst(streetKit, "Harbor/Town/Square_Market", "StreetKit (1)");
        Inst(rusty, "Harbor/Waterfront/Warehouses/wh_A/ext", "Lantern_Post_Rusty"); Inst(rusty, "Harbor/Town/Old_Quarter", "rusty lamp");
        Inst(legacyPrefab, "Harbor/Town/Alleys/alley_05", "Lantern_Post"); Inst(legacyPrefab, "Harbor/Town/Old_Quarter", "Lantern_Post");
        Inst(legacyPrefab, "Harbor/Town/Old_Quarter", "Lantern_Post (1)"); Inst(legacyPrefab, "Harbor/Waterfront/Warehouses/wh_B/ext", "Lantern_Post");

        // bell buoys (§1.2b)
        foreach (var seg in new[] { "Segment_A", "Segment_B", "Segment_C", "Segment_C (1)", "Segment_C (2)", "Segment_C (3)" }) Node("Harbor/Waterfront/Breakwater/" + seg + "/Markers");
        void Buoys(string field, string prefix, int n, Func<int, bool> pa, int disabledIdx)
        {
            for (int i = 1; i <= n; i++)
            {
                var b = Node(field + "/" + prefix + i.ToString("00")); MeshOn(b, SphereMesh, 2); b.AddComponent<Bobbing>();
                var ba = Node(field + "/" + prefix + i.ToString("00") + "/Mount/BellAudio");
                var a = ba.AddComponent<AudioSource>(); a.clip = bell; a.playOnAwake = pa(i); if (i == disabledIdx) a.enabled = false;
            }
        }
        string east = "Harbor/Waterfront/Breakwater/Segment_C (2)/Markers/BuoyField_East", west = "Harbor/Waterfront/Breakwater/Segment_C (2)/Markers/BuoyField_West";
        Buoys(east, "buoy.r", 12, i => i == 12, -1);
        Buoys(west, "buoy.w", 6, i => i == 4, 2);
        Node(east).SetActive(false);

        // quest boards (§1.2c)
        var board = Node("Harbor/Town/Square_Market/QuestBoard_Stand/Board"); var qb = board.AddComponent<QuestBoard>();
        qb.noticeTemplate = noticeOld; qb.noticeRoot = Node("Harbor/Town/Square_Market/QuestBoard_Stand/Board/Notices").transform; qb.maxNotices = 6;
        var tav = Node("Harbor/Town/Tavern/Interior/notice_board (1)"); var qb2 = tav.AddComponent<QuestBoard>();
        qb2.noticeTemplate = noticeV2; qb2.noticeRoot = Node("Harbor/Town/Tavern/Interior/notice_board (1)/Notices").transform; qb2.maxNotices = 4;

        // audio + npcs + world ui + debug
        Node("Audio/Ambience").AddComponent<HarborAmbience>().profile = amb;
        for (int i = 0; i < 6; i++) { var g = Node("Audio/emitters/" + (i < 3 ? "gull_emitter_" + (i + 1) : "surf_" + i)); var a = g.AddComponent<AudioSource>(); a.clip = gulls[Math.Min(i, 3)]; }
        for (int i = 0; i < 14; i++) Node("NPCs/" + (i % 3 == 0 ? "Dock" : i % 3 == 1 ? "Market" : "Tavern") + "/spawn_" + (i + 1).ToString("00")).AddComponent<NpcSpawnPoint>();
        var wc = Node("UI_World/LabelsCanvas"); var c = wc.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
        foreach (var lbl in new[] { "lbl_market", "lbl_tavern", "lbl_pier1", "lbl_pier2", "lbl_pier3", "lbl_pier4", "lbl_lighthouse", "lbl_warehouse" }) Node("UI_World/LabelsCanvas/" + lbl);
        Node("[Debug]/LightingTest/probe_A"); Node("[Debug]/LightingTest/probe_B");

        // filler to quotas (§1.2 table)
        foreach (var pier in new[] { "Pier_01", "Pier_02", "Pier_03", "Pier_04" }) Fill(Node("Harbor/Waterfront/Piers/" + pier), 58);
        foreach (var seg in new[] { "Segment_A", "Segment_B", "Segment_C", "Segment_C (1)", "Segment_C (2)", "Segment_C (3)" }) Fill(Node("Harbor/Waterfront/Breakwater/" + seg + "/Markers"), 14);
        Fill(Node("Harbor/Waterfront/Breakwater"), 30);
        Fill(Node("Harbor/Waterfront/Promenade"), 55);
        Fill(Node("Harbor/Waterfront/Warehouses/wh_A"), 28); Fill(Node("Harbor/Waterfront/Warehouses/wh_B"), 28);
        Fill(Node("Harbor/Town/Square_Market"), 45);
        for (int i = 1; i <= 7; i++) Fill(Node("Harbor/Town/Alleys/alley_" + i.ToString("00")), 18);
        Fill(Node("Harbor/Town/Tavern/Exterior"), 25); Fill(Node("Harbor/Town/Tavern/Interior"), 30);
        Fill(Node("Harbor/Town/Old_Quarter"), 45); Fill(Node("Harbor/Town/Houses"), 100);
        var scatter = Node("Harbor/Terrain_Props/scatter");
        for (int i = 0; i < 90; i++) { var g = Child(scatter.transform, Pick(Nature) + (R.Next(3) == 0 ? "" : " (" + (i % 13) + ")")); Decorate(g); _filler++; if (R.Next(4) == 0) Fill(g, 1 + R.Next(3), 5); }
        Fill(Node("Harbor/Terrain_Props"), 20, 4);
        Fill(Node("NPCs"), 20, 4); Fill(Node("UI_World/LabelsCanvas"), 12, 5); Fill(Node("[Debug]"), 10, 4);
        foreach (var root in new[] { "Harbor/Waterfront/Piers/Pier_01", "Harbor/Waterfront/Piers/Pier_04", "Harbor/Town/Houses", "Harbor/Town/Old_Quarter", "Harbor/Waterfront/Promenade", "Harbor/Town/Tavern/Interior", "Harbor/Waterfront/Warehouses/wh_B", "Harbor/Town/Alleys/alley_06" })
            for (int k = 0; k < 2; k++) DeepChain(Node(root), 9);
        // 5 particle systems, 12 cargo spots
        var all = harbor.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.GetComponent<MeshRenderer>() != null && PrefabUtility.GetPrefabInstanceHandle(t.gameObject) == null && t.GetComponent<Light>() == null && !PathOfT(t).Contains("Breakwater/Segment_C (2)/Markers/BuoyField")).ToList();
        for (int i = 0; i < 30; i++) all[R.Next(all.Count)].gameObject.AddComponent<CargoSpot>();
        for (int i = 0; i < 8; i++) { var g = all[R.Next(all.Count)].gameObject; if (g.GetComponent<Light>() == null) { var w = Child(g.transform, "window_glow"); AddLight(w, LightType.Point, 0.8f, LightShadows.None, 3f); } }
        Node("[Debug]").SetActive(false);

        int total = Count(harbor);
        int deep = harbor.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Count(t => Depth(t) >= 8);
        int maxDepth = harbor.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Max(t => Depth(t));
        EditorSceneManager.SaveScene(harbor, FX + "Scenes/Harbor.unity");

        // missing reference: delete Notice_Card after saving the scene (§1.5)
        string deletedGuid = AssetDatabase.AssetPathToGUID(FX + "Prefabs/UI/Notice_Card.prefab");
        AssetDatabase.DeleteAsset(FX + "Prefabs/UI/Notice_Card.prefab");

        // ---------- Docks_Test.unity ----------
        Nodes.Clear();
        var docks = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Inst(lanternPost, "Test", "Lantern_Post"); Inst(lanternPost, "Test", "Lantern_Post (1)"); Inst(lanternPost, "Test", "Lantern_Post (2)");
        var db = Node("Test/board_test"); var dq = db.AddComponent<QuestBoard>(); dq.noticeTemplate = noticeV2; dq.noticeRoot = Node("Test/board_test/Notices").transform; dq.maxNotices = 2;
        Fill(Node("Test"), 25, 5);
        EditorSceneManager.SaveScene(docks, FX + "Scenes/Docks_Test.unity");

        // ---------- MainMenu.unity (§1.3) ----------
        Nodes.Clear();
        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = Node("Main Camera"); cam.AddComponent<Camera>(); cam.tag = "MainCamera";
        var ctrl = Node("MenuRoot").AddComponent<MenuController>();
        var es = Node("EventSystem"); es.AddComponent<UnityEngine.EventSystems.EventSystem>(); es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        var es1 = Node("EventSystem (1)"); es1.AddComponent<UnityEngine.EventSystems.EventSystem>(); es1.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); es1.SetActive(false);
        Canvas MakeCanvas(string name, int order)
        {
            var g = Node(name); g.AddComponent<RectTransform>(); var cv = g.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = order;
            var sc = g.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            g.AddComponent<GraphicRaycaster>(); return cv;
        }
        RectTransform UI(string path, Vector2 size, bool stretch = false)
        {
            var g = Node(path); var rt = g.GetComponent<RectTransform>(); if (rt == null) rt = g.AddComponent<RectTransform>();
            if (stretch) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; } else rt.sizeDelta = size;
            return rt;
        }
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MakeCanvas("MenuCanvas", 0);
        UI("MenuCanvas/Background", Vector2.zero, true).gameObject.AddComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f);
        UI("MenuCanvas/MainPanel", new Vector2(600, 700)); var cg = Node("MenuCanvas/MainPanel").AddComponent<CanvasGroup>(); cg.alpha = 1; cg.interactable = true; cg.blocksRaycasts = true;
        var title = UI("MenuCanvas/MainPanel/Title", new Vector2(560, 120)).gameObject.AddComponent<Text>(); title.text = "SALTMARSH"; title.font = font; title.fontSize = 72; title.alignment = TextAnchor.MiddleCenter;
        UI("MenuCanvas/MainPanel/Buttons", new Vector2(400, 420)).gameObject.AddComponent<VerticalLayoutGroup>().spacing = 12;
        Button MakeButton(string name, string label, UnityAction a, bool interactable = true)
        {
            var rt = UI("MenuCanvas/MainPanel/Buttons/" + name, new Vector2(400, 90));
            rt.gameObject.AddComponent<Image>().color = new Color(0.85f, 0.8f, 0.65f);
            var b = rt.gameObject.AddComponent<Button>(); b.interactable = interactable;
            if (a != null) UnityEventTools.AddPersistentListener(b.onClick, a);
            var t = UI("MenuCanvas/MainPanel/Buttons/" + name + "/Label", Vector2.zero, true).gameObject.AddComponent<Text>(); t.text = label; t.font = font; t.fontSize = 36; t.alignment = TextAnchor.MiddleCenter; t.color = Color.black;
            return b;
        }
        MakeButton("PlayButton", "Set Sail", ctrl.OnPlayPressed); MakeButton("SettingsButton", "Settings", ctrl.OnSettingsPressed);
        MakeButton("CreditsButton", "Credits (soon)", null, false); MakeButton("QuitButton", "Quit", ctrl.OnQuitPressed);
        UI("MenuCanvas/MapPanel", new Vector2(1700, 900)); var map = UI("MenuCanvas/MapPanel/Map", new Vector2(1600, 800)).gameObject.AddComponent<Image>();
        map.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FX + "Art/UI/harbor_map.png"); map.preserveAspect = false; Node("MenuCanvas/MapPanel").SetActive(false);
        UI("MenuCanvas/SettingsPanel", new Vector2(800, 600)); Node("MenuCanvas/SettingsPanel").SetActive(false);
        MakeCanvas("FxCanvas", 5);
        var vig = UI("FxCanvas/VignetteOverlay", Vector2.zero, true).gameObject.AddComponent<Image>();
        vig.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FX + "Art/UI/vignette.png"); vig.color = new Color(1, 1, 1, 0.35f); vig.raycastTarget = true;
        EditorSceneManager.SaveScene(menu, FX + "Scenes/MainMenu.unity");

        AssetDatabase.SaveAssets();
        var key = $"{{\"KEY_TOTAL\":{total},\"KEY_DEEP\":{deep},\"MAX_DEPTH\":{maxDepth},\"FILLER\":{_filler},\"KEY_DELETED_GUID\":\"{deletedGuid}\",\"BUILD_TARGET\":\"{EditorUserBuildSettings.activeBuildTarget}\"}}";
        File.WriteAllText(keyPath, key);
        return key;
    }
}
