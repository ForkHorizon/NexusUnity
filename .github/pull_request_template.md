## Summary

<!-- What changed and why? -->

## Validation

- [ ] Package compiles in Unity.
- [ ] Relevant Editor tests pass.
- [ ] Python bridge still compiles (`python3 -m py_compile Editor/nexus_unity_bridge.py Editor/nexus_bridge/*.py`).
- [ ] Public docs updated for behavior/API changes.
- [ ] `CHANGELOG.md` updated for public changes.

## Safety checklist

- [ ] Server remains loopback/local-only.
- [ ] File operations remain constrained to the Unity project root.
- [ ] No generated caches, Python bytecode, `.DS_Store`, Unity `Library/`, or local agent artifacts included.
- [ ] No secrets, credentials, private project source, or proprietary assets included.
