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

## 2024-05-23 - Editor Window Feedback
**Learning:** Users in Unity Editor Windows often miss console logs for transient actions. `ShowNotification` provides immediate, contextual feedback overlaying the window.
**Action:** Use `ShowNotification(new GUIContent("Message"))` for confirmation of actions like copying to clipboard or clearing data, instead of `Debug.Log`.
