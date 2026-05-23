> Normal contributor PRs must target `development`. `main` is release-only and accepts only maintainer release PRs from `development` or `release/*`.

## Summary

<!-- What changed and why? -->

## Validation

- [ ] Package compiles in Unity.
- [ ] Relevant Editor tests pass.
- [ ] `NexusQualityGate` passes or remaining warnings are intentional.
- [ ] Public/protected C# types and methods have useful XML documentation.
- [ ] Python bridge still compiles (`python3 -m py_compile Editor/nexus_unity_bridge.py Editor/nexus_bridge/*.py`).
- [ ] Public docs updated for behavior/API changes.
- [ ] `CHANGELOG.md` updated for public changes.

## Safety checklist

- [ ] Server remains loopback/local-only.
- [ ] File operations remain constrained to the Unity project root.
- [ ] No generated caches, Python bytecode, `.DS_Store`, Unity `Library/`, or local agent artifacts included.
- [ ] No secrets, credentials, private project source, or proprietary assets included.
