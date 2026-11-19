# AssetImpact fixture corpus (V3-10)

Frozen ground truth for the Layer A asset-impact scanner (`find_references` symbol mode,
`analyze_change_impact`, `validate_asset_integrity`). The `~` suffix keeps Unity from importing it.

- `Assets/` — a minimal Force Text Unity project tree. Scripts, prefabs, the variant, the nested prefab,
  the scene, the ScriptableObjects and the animation clip were written by Unity 6000.3.16f1 in batchmode
  (`FixtureGen.cs.txt`). `Prefabs/Broken.prefab`, `Data/Binary.asset` and `Data/Malformed.asset` are
  hand-written failure cases.
- `expected_findings.json` — labelled findings. Its SHA-256 is frozen; change it only with a new task id.

Do not edit or reserialize these files. Regenerating with `FixtureGen.cs.txt` produces new GUIDs and
file ids, which invalidates `expected_findings.json`.
