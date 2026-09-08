# Quality plan and rebuild phases

Status: Phase 0 draft for approval

## Definition of done

A phase is complete when its approved scope is implemented, its relevant automated checks pass, its visual states have been reviewed at target sizes and scaling levels, accessibility checks have no unresolved critical findings, and the owner approves the result.

After approval, the phase receives one or more coherent commits and is pushed to the GitHub branch. Each report records branch, commit, tests, visual checks, known limitations, and push status.

## Rebuild sequence

### Phase 0 — Foundation specification

Deliverables:

- product behavior and state catalogue;
- architecture and data-preservation contract;
- quality matrix, milestone boundaries, and release gate;
- explicit exclusion of prior visual designs.

Gate: owner approves the written foundation.

### Phase 1 — Design language and application structure

Deliverables:

- original structural explorations for the dashboard and widget;
- typography, color, spacing, geometry, elevation, icon, motion, and sound direction;
- component-state specifications;
- narrow-window, scaling, keyboard, and high-contrast treatments.

Review order:

1. information hierarchy and layout without decoration;
2. typography and density;
3. palette and surface depth;
4. icon construction and brand identity;
5. motion and sound samples.

Gate: owner approves the design system and representative dashboard/widget states. No production UI implementation precedes this approval.

### Phase 2 — Data and domain foundation

Deliverables:

- solution/project boundaries;
- deterministic hydration, pace, and reminder rules;
- versioned state, atomic persistence, recovery, and migration;
- isolated clock, filesystem, and snapshot fixtures;
- read-only legacy-data discovery proof.

Gate: automated domain/persistence tests pass, migration evidence is reviewed, and owner approves the implementation milestone.

### Phase 3 — Native shell and component library

Deliverables:

- accessible custom chrome;
- navigation and responsive content host;
- complete buttons, inputs, toggles, check boxes, combo boxes, progress controls, scrollbars, menus, tooltips, dialogs, and focus visuals;
- standard and reduced-motion behavior;
- design-time component gallery and deterministic captures.

Gate: component states pass keyboard, contrast, scaling, and visual review.

### Phase 4 — Today and History

Deliverables:

- complete logging, custom amount, undo, progress, pace, recent-entry, history, and midnight-rollover flows;
- designed empty, goal-complete, over-goal, validation, save-failed, and recovery states;
- dashboard reflow at supported sizes.

Gate: owner approves the dashboard and all functional/visual checks pass.

### Phase 5 — Schedule, Settings, and updates

Deliverables:

- distinct schedule and settings destinations;
- draft/save/cancel and unsaved-change protection;
- next-reminder preview;
- update-state experience and safe installer handoff;
- privacy, version, data, and diagnostic information.

Gate: owner approves both workflows after state and failure-path review.

### Phase 6 — Widget and Windows integration

Deliverables:

- expanded and compact widgets;
- placement persistence across monitors and DPI changes;
- custom tray menu, notification activation, close-to-tray, quit, and single-instance restore;
- final taskbar, executable, window, tray, and notification icons.

Gate: owner approves the desktop experience on Windows 10 and Windows 11 targets.

### Phase 7 — Motion, audio, and polish

Deliverables:

- approved state transitions and restrained ambient motion;
- reduced-motion alternatives;
- original log and reminder sounds with overlap control;
- final copy, icon optical alignment, layout rhythm, and rendering performance.

Gate: owner approves motion and sound in context.

### Phase 8 — Upgrade and release candidate

Deliverables:

- clean-install and upgrade installers;
- update from the last public release to the candidate;
- full data preservation and rollback rehearsal;
- final automated, visual, accessibility, and packaging evidence;
- release notes draft and version proposal.

Gate: explicit owner approval is required before creating any tag or GitHub release.

## Automated verification matrix

| Area | Required checks |
| --- | --- |
| Hydration | add boundaries, rounding, totals, remaining, over-goal, undo, date partitioning |
| Pace | every schedule state, goal changes, zero/invalid goal defense, timezone and DST boundaries |
| Reminders | eligibility, next day, goal completion, drink reset, notification reset, sleep/resume suppression |
| Persistence | first save, atomic replace, concurrent requests, schema migration, corrupt primary, valid backup, total failure |
| Import | representative legacy fixtures, duplicates, partial records, repeat import, backup before merge |
| View models | commands, enabled states, draft/cancel/save, navigation, error and recovery messages |
| Lifecycle | single instance, hidden/minimized restore, close-to-tray, widget singleton, clean quit |
| Updates | current, available, malformed release, missing asset, offline, cancelled download, closed file before launch |
| Packaging | executable starts, resources resolve, icon presence, per-user upgrade, uninstall preserves user data by default |

Tests use temporary directories and fake clocks. They must never read or modify the developer's real `%LOCALAPPDATA%\Waterline` state.

## Visual verification matrix

Capture every primary surface at 100%, 125%, 150%, 175%, and 200% scaling where the harness permits. At minimum, manually verify 100%, 150%, and 200% on Windows.

Required dashboard sizes:

- 900 × 650 minimum;
- 1024 × 768 compact;
- 1280 × 860 preferred;
- 1600 × 1000 large;
- maximized on 1920 × 1080 and one higher-density display.

Required capture states:

- empty first launch;
- partial progress with entries;
- behind and ahead pace;
- goal complete and over goal;
- History with zero and completed days;
- Schedule and Settings rest, focused, modified, invalid, saving, and failure states;
- custom amount rest, focused, invalid, and success feedback;
- expanded and compact widget;
- update available, downloading, ready, and failed;
- data recovery notice and unrecoverable-data dialog;
- tray menu and reminder notification where automation permits.

Each capture is checked for clipping, overlap, unexpected scrollbars, unreadable text, weak focus, aliasing, icon clarity, inconsistent spacing, and incorrect window bounds. Approved baselines are created only from the new design.

## Accessibility verification

- Complete all workflows using only the keyboard.
- Inspect automation names, roles, states, and reading order with Windows accessibility tooling.
- Verify normal text, large text, controls, focus rings, and meaningful graphics against approved contrast targets.
- Verify high-contrast behavior and that selection/error/completion never relies only on color.
- Verify reduced motion with Windows animations disabled.
- Verify minimum pointer targets and tooltips for every icon-only action.
- Verify focus containment and return for dialogs and panels.

## Manual functional journeys

1. Clean install → set goal and schedule → enable reminders → log water → open and compact widget → close dashboard → reopen from tray → quit.
2. Upgrade from `v2.0.1` with real-shaped data → verify every setting and entry → log and undo → restart → verify persistence.
3. Start before the schedule → cross into active time → log → sleep past a due time → resume → receive at most one valid reminder.
4. Move dashboard and widget between monitors at different scaling levels → compact/expand → disconnect a monitor → restart and verify visible placement.
5. Simulate corrupt state with valid backup → recover without overwriting the corrupt source → save → restart.
6. Check for an update offline → retry online → download → verify closed and complete installer → start visible installation.

## Performance and reliability budgets

Initial targets, subject to measurement on a representative Windows machine:

- dashboard usable within 1.5 seconds after process start;
- logging feedback begins within 100 ms and persistence completes without blocking the UI thread;
- idle application avoids continuous high-frequency work;
- ambient animation remains smooth without impairing text rendering;
- widget and dashboard do not create duplicate timers or update checks;
- retained diagnostic files remain bounded.

## Source-control protocol

- Begin each phase from a clean tree based on the latest `origin/main`.
- Use a `codex/` branch unless the owner requests another branch.
- Preserve unrelated changes and stop if a required edit overlaps unresolved user work.
- Commit only coherent, reviewed milestones.
- Push only after the phase approval gate unless the owner explicitly asks for an earlier backup branch.
- Report branch, commit, tests, and push status after each phase.
- Never create or push a release tag without explicit publication approval.

## Release gate

Publication remains blocked until all Phase 8 evidence is complete and the owner explicitly approves release. Because the repository publishes when a matching `v*` tag is pushed, creating that tag is itself a publication action.
