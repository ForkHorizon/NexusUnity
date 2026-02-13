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

## 2024-10-25 - [Editor Feedback Patterns]
**Learning:** In Unity Editor Windows, replacing `Debug.Log` with `ShowNotification` for transient actions (like copying to clipboard) provides immediate, contextual feedback that is far superior to console logging.
**Action:** Use `this.ShowNotification(new GUIContent("Message"))` for all successful user actions in EditorWindows instead of logging.
