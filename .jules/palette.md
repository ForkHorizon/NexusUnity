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

## 2024-05-23 - Immediate Feedback for Clipboard Actions
**Learning:** Users lack confidence when copying values (like URLs) without visual confirmation. Relying on `Debug.Log` is insufficient as it requires looking away from the UI.
**Action:** Use `ShowNotification(new GUIContent("Text"))` for transient actions to provide immediate, in-context feedback.
