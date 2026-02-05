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

## 2024-05-22 - Immediate Mode GUI Feedback Loops
**Learning:** Unity's IMGUI is stateless, making temporary feedback (like "Copied!") tricky. It requires manual state tracking (`timeSinceStartup`) and explicit `Repaint()` calls loop during the active feedback window to animate/revert the state.
**Action:** Use a dedicated `Update` loop to handle the "revert" logic and trigger `Repaint()` only while the feedback effect is active to preserve performance.
