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
