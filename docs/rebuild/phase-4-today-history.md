# Phase 4 Today and history

Status: approved September 9, 2026

Branch: `codex/today-history`

## Today

The Phase 3 component gallery has been replaced by the working Today experience. It provides:

- local date, current amount, daily goal, percentage, remaining amount, and distinct goal-complete and over-goal copy;
- a native reservoir whose fill reflects the bounded visible percentage and animates to its new level when Windows client-area animations are enabled;
- emphasized 12 oz logging plus 8 oz, 16 oz, and custom amount actions;
- atomic local persistence with rollback when saving fails;
- current reminder and pace explanations, including before, active, ahead, behind, complete, after-hours, and excluded-day states;
- newest-first current-day entries and an undo action limited to the most recent eligible entry;
- polite accessible confirmation text for successful logging and undo;
- designed empty, persistence-failed, recovered-backup, unrecoverable, goal-complete, and over-goal states.

The existing custom amount dialog continues to enforce finite amounts from 0.1 through 64.0 oz, rounds storage to one decimal place, validates inline, traps focus, and supports Enter, Escape, and focus return.

## Insights and history

Insights now presents seven local calendar days ending today. Each row displays the exact total, current-goal percentage, and an explicit status. Goal completion uses a check shape in addition to color, and over-goal totals remain visible rather than being clamped.

Selecting a day reveals its entries newest first with amount and local time. Zero-intake days render an intentional empty detail state. Recalculating against a changed goal does not modify historical drink amounts.

## Date, time, and responsive behavior

The view model rebuilds Today and history collections at local midnight and reevaluates the system timezone whenever the main window activates. This covers midnight rollover, timezone changes, daylight-saving transitions, and sleep/resume return without presenting yesterday's entries as today.

At widths below 1040 effective pixels, the primary dashboard and history list span the full content width, secondary rails move below them, and the reservoir shortens. At 900 × 650, the full logging action group remains in the first viewport while the fixed title and navigation rows stay visible.

## Deterministic states and verification

Snapshot fixtures are available for populated Today, empty first launch, loading, compact layout, keyboard focus, history, goal complete, over goal, save failure, backup recovery, unrecoverable data, and high contrast. Snapshot state remains isolated in a temporary profile and does not touch installed Waterline data.

Automated verification for this candidate covers amount boundaries, local-day partitioning and ordering, current-day undo selection, over-goal totals, goal-change history recalculation, pace states, reminders, migration, atomic persistence, backup recovery, and unrecoverable-state protection.

Mouse and keyboard logging, custom-dialog focus return, history selection, undo announcements, recovery action messaging, and mixed-DPI movement remain on the ongoing manual regression checklist. The owner approved the Phase 4 implementation candidate on September 9, 2026.
