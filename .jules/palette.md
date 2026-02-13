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

## 2024-10-24 - [Transient Editor Feedback]
**Learning:** Unity Editor Windows lack built-in transient feedback for actions like clipboard operations. The `ShowNotification(GUIContent)` method is the standard but often overlooked pattern for this in IMGUI.
**Action:** Always pair invisible actions (clipboard copy, background tasks) with `ShowNotification` to provide immediate visual confirmation without console log noise.
