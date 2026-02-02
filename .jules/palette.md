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

## 2024-11-26 - [Editor Window Feedback Loop]
**Learning:** Unity Editor Windows require explicit `Repaint()` calls to animate feedback (like "Copied!") since they are immediate mode and only repaint on events.
**Action:** Use `EditorApplication.timeSinceStartup` for timing and conditional `Repaint()` in `OnGUI` for temporary feedback states.
