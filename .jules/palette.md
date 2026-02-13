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

## 2026-02-09 - Unity Editor Feedback Patterns
**Learning:** In Unity Editor Windows (`EditorWindow`), `Debug.Log` is insufficient for immediate user feedback. `ShowNotification(new GUIContent(...))` provides a standard, visible toast notification that overlays the window, which is much better for confirming actions like "Copied to Clipboard".
**Action:** Always use `ShowNotification` for transient success messages in Editor tools instead of logging to the console.
