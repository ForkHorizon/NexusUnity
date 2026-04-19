# PALETTE'S JOURNAL

## CRITICAL LEARNINGS ONLY
⚠️ ONLY add journal entries when you discover:
- An accessibility issue pattern specific to this app's components
- A UX enhancement that was surprisingly well/poorly received
- A rejected UX change with important design constraints
- A surprising user behavior pattern in this app
- A reusable UX pattern for this design system

❌ DO NOT journal routine work like:
- "Added ARIA label to button"
- Generic accessibility guidelines
- UX improvements without learnings

Format: `## YYYY-MM-DD - [Title]
**Learning:** [UX/a11y insight]
**Action:** [How to apply next time]`

## 2024-10-24 - [Editor Window Feedback]
**Learning:** In Unity Editor Windows (`EditorWindow`), `ShowNotification` is vastly superior to `Debug.Log` for transient user feedback (like "Copied to clipboard"). `Debug.Log` disconnects the user from the UI context and clutters the console.
**Action:** Always prefer `ShowNotification(new GUIContent("message"))` for immediate confirmation of user actions in Editor tools.

## 2024-05-22 - [Resource Accessibility in Unity Packages]
**Learning:** Users of Unity packages often struggle to find documentation because it lives in the `Packages/` folder, which is separate from `Assets/`. Providing direct "Open Documentation" buttons in the main editor window using `AssetDatabase.OpenAsset` significantly reduces friction.
**Action:** When building Editor Tools for packages, always include a "Resources" or "Help" section with direct links to key documentation files.

## 2026-04-19 - [Adding tooltips to GUILayout.Button]
**Learning:** Adding tooltips using `GUIContent("text", "tooltip")` instead of plain strings for `GUILayout.Button` significantly improves accessibility and discoverability in Unity Editor toolings.
**Action:** Always prefer `new GUIContent(text, tooltip)` over simple string titles for buttons in IMGUI to help users understand the purpose of each button, especially when space is constrained or icons are used.
