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

## 2026-02-03 - Tooltips for Complex Actions
**Learning:** Button text alone is often insufficient for explaining complex Editor actions (like "Link to Gemini CLI"). Tooltips provide necessary context without cluttering the UI.
**Action:** Always include a tooltip in `GUIContent` for Editor buttons that perform non-trivial operations.
