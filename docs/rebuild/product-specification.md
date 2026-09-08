# Product specification

Status: Phase 0 approved

## Product promise

Waterline helps someone maintain a steady hydration rhythm during their chosen day. It should feel calm, immediate, and trustworthy: logging takes one action, current progress is obvious, reminders explain themselves, and the widget provides the essential workflow without requiring the full dashboard.

Waterline is private by default. It requires no account and does not upload hydration history or settings.

## Experience principles

1. **Progress at a glance.** The current amount, goal, remaining amount, and pace must be understandable within a few seconds.
2. **One-action logging.** The primary amount can be logged from both dashboard and widget without a dialog.
3. **Quiet confidence.** Motion and audio confirm actions without demanding attention.
4. **Honest state.** Reminder, update, persistence, and validation states use precise language and never imply success before it occurs.
5. **Native behavior.** Window management, keyboard navigation, notifications, scaling, installation, and taskbar behavior follow Windows expectations while retaining Waterline's visual identity.
6. **Local ownership.** User data remains readable, recoverable, and preserved during upgrades.

## Information architecture

The product has six primary destinations:

| Destination | Purpose | Required content |
| --- | --- | --- |
| Today | Log water and understand today's progress | total, goal, remaining, progress, primary and quick logging, reminder status, recent entries |
| Insights | Review recent hydration patterns | seven-day summary, goal markers, daily totals, selected-day details, streaks, empty state |
| Goals | Configure and understand the hydration target | daily target, goal progress explanation, future goal-related options |
| Schedule | Control when reminders may run | enabled state, days, start/end time, interval, next eligible reminder explanation |
| Widget | Open or manage the desktop companion | launch/focus widget, always-on-top explanation, expanded/compact state |
| Settings | Configure application behavior | goal, sounds, update status, data location/recovery actions, version and privacy information |

The destination order is Today, Insights, Goals, Schedule, Widget, Settings. On smaller windows, Widget and Settings may move into a labeled overflow menu, but navigation must preserve destination names, keyboard access, and state.

## Surfaces

### Dashboard shell

The shell provides custom native chrome, an application identity area, primary navigation, current operating status, and content. It supports minimize, maximize/restore, close-to-tray, resize, system menu access, keyboard window commands, snap layouts where Windows permits them, and correct behavior on mixed-DPI monitors.

The shell must remain usable at 900 × 650 effective pixels. The preferred initial size is 1280 × 860. Content must reflow or scroll without overlap or clipped actions.

### Today

Today includes:

- local date and clear daily context;
- current amount, daily goal, percentage, and amount remaining;
- one emphasized 12 oz action plus 8 oz, 16 oz, and custom actions;
- animated progress feedback when an entry changes;
- current reminder state and next scheduled time when applicable;
- pace state: before schedule, on pace, ahead, behind, completed, after schedule, or no plan today;
- recent entries ordered newest first;
- undo of the most recently logged entry with an accessible confirmation message;
- designed empty, loading, saving, save-failed, and goal-complete states.

Amounts must accept 0.1–64.0 oz inclusive, be finite, and be stored to one decimal place. An amount that rounds below 0.1 oz is invalid.

Undo affects only the most recent entry from the current local day. If there is no eligible entry, the action is disabled and explains why through accessible help text.

### Custom amount dialog

The dialog contains a numeric amount field, unit label, Add action, and Cancel action. It validates inline without opening a Windows message box. Enter submits a valid amount; Escape cancels. Opening the dialog focuses and selects the value. Closing it returns focus to the control that opened it.

The dialog traps keyboard focus while open, announces its title and validation status, and remains fully visible at supported scaling levels.

### Insights and history

The initial rebuilt release presents seven local calendar days ending today. Each day shows its total relative to the goal that applies to the view. Zero intake renders as zero rather than a decorative positive value. Goal completion is communicated through shape or icon as well as color.

Selecting a day reveals its total and entries. History must handle no data, partial data, completed goals, amounts over goal, and a goal change without rewriting historical drink amounts.

### Pace

Pace compares actual intake with an even progression between the configured start and end time. It must label the comparison as guidance rather than medical advice.

| State | Required behavior |
| --- | --- |
| Reminders disabled | Show schedule status without implying that a notification will occur |
| Before schedule | Show the schedule start and no amount due yet |
| Active/on pace | Show actual and expected progress with a target marker |
| Active/behind | Show the deficit and a bounded suggested next amount |
| Active/ahead | Show the lead without encouraging excessive intake |
| Goal reached | Mark the plan complete and suppress further reminders |
| After schedule | Show remaining amount and state that the scheduled period ended |
| Excluded day | Show that no plan applies today |

### Recent entries

Each entry shows amount, local time, and an identifiable water icon. The list changes at local midnight and never displays yesterday's entries as today's. Date, timezone, daylight-saving, sleep, and resume changes must refresh derived state safely.

### Schedule

Schedule is a distinct experience rather than a differently titled copy of Settings. It includes:

- reminders on/off;
- 30, 45, 60, 90, and 120 minute interval presets;
- start and end time using locale-appropriate editing and display;
- seven explicitly named day toggles with accessible names;
- a live plain-language preview of the next eligible reminder;
- validation that the end is later than the start for a same-day schedule;
- Save and Cancel with a true draft state.

Cancel discards changes. Navigating away with modified fields prompts through a custom Waterline dialog. Saving persists the entire valid draft atomically.

### Settings

Settings includes:

- Waterline sounds on/off;
- application version and update state;
- manual update check and approved install action;
- local-data location and recovery status;
- privacy statement;
- widget preferences that are approved for the first implementation;
- About and diagnostic version information.

Settings uses the same draft/save/cancel contract as Schedule.

### Expanded widget

The expanded widget shows current progress, remaining amount, primary and quick logging, custom logging, reminder status, and concise pace status. It can open the dashboard, collapse, close, and move between monitors. It remains above other windows while open.

Logging and settings changes synchronize immediately with the dashboard through the shared application state. Only one widget exists.

### Compact widget

The compact widget shows brand identity, progress, and an obvious expand action. Its hit target and focus treatment must remain usable at all supported scaling levels. Compacting or expanding preserves the user's chosen monitor and anchor position as closely as the available work area permits.

### Notification area

The tray icon uses an optically tuned small-size icon. Its custom-themed menu contains Open Waterline, Open Widget, Pause/Resume Reminders when applicable, and Quit. Double-clicking opens the dashboard. The menu is fully operable by keyboard.

Closing the dashboard hides it. Quit closes the widget, disposes timers and tray resources, flushes pending local data, and exits.

### Reminder notification

A reminder contains Waterline identity, remaining amount, and a concise action. Activating it opens and restores the dashboard. The reminder uses the Waterline reminder sound only when application sounds are enabled and must avoid a duplicate default Windows sound where the platform permits.

Reminder timing is anchored to the later of schedule start, last drink, or last sent reminder. No reminder is sent outside selected days/times or after the daily goal is reached. Sleep/resume must not create a rapid burst of missed reminders.

### Updates

Update states are idle, checking, current, available, downloading, ready, installing, failed, and unavailable in development. The interface provides progress when known, actionable errors, retry, and release-version context.

Waterline downloads only installer assets from the configured GitHub repository. The completed file is flushed and closed before launch. Installation requires a visible user action. A failed check or download cannot affect hydration logging.

### Installer and Windows identity

The executable, taskbar, Alt+Tab, window, notification area, notification, shortcuts, installer, and uninstaller use intentional assets suited to their rendered sizes. Upgrading retains local state and avoids launching duplicate instances. The installer supports per-user installation unless a later approved requirement changes it.

## Application state catalogue

Every visual surface must cover the relevant states below.

| Category | States |
| --- | --- |
| Data | first launch, no entries, entries today, historical entries, goal reached, over goal, corrupt file, recovered backup, save failure |
| Time | before schedule, active, after schedule, excluded day, midnight rollover, timezone change, sleep/resume |
| Reminder | disabled, enabled/inactive, scheduled, due, sent, paused, completed for day, no eligible days |
| Input | rest, hover, pressed, focused, invalid, disabled, busy, successful |
| Window | normal, maximized, minimized, hidden, restored by second launch, narrow, short, mixed-DPI monitor |
| Widget | closed, expanded, compact, moved, restored, dashboard hidden, dashboard minimized |
| Update | idle, checking, current, available, downloading, ready, installing, failed, offline |
| Accessibility | keyboard-only, screen reader, 100/125/150/175/200% scaling, high contrast, reduced motion |

## Keyboard and accessibility contract

- Logical Tab and Shift+Tab order follows reading order.
- Every interactive control has a visible focus state with at least 3:1 contrast against adjacent colors.
- Icon-only controls expose accessible names and tooltips.
- Enter and Space activate controls according to WPF conventions.
- Escape closes the topmost dismissible surface.
- The system menu remains reachable from the keyboard.
- Text and meaningful graphics meet WCAG 2.2 AA contrast targets where applicable.
- Color is never the sole indicator of selection, completion, error, or pace.
- Motion respects the Windows animation preference and a future in-app reduced-motion preference.
- Text remains readable at 200% Windows scaling without truncating essential content.

## Visual direction to design from scratch

The approved starting direction is a premium dark hydration dashboard with a deep navy foundation and luminous cyan/aqua accents. The new system must define its own typography, geometry, spacing, icon construction, depth, surfaces, and motion. No layout, icon selection, gradient, ornamental wave, or component shape will be copied from an earlier Waterline interface.

Visual exploration begins with low-detail structure and type hierarchy. Brand mark, color tokens, component shapes, and decorative assets follow after the structure is approved.

## Privacy and network boundary

Hydration entries, settings, schedules, widget position, and diagnostic state remain local. Network access is limited to update checks and downloads from the configured GitHub release endpoint unless the owner later approves another feature. Analytics and telemetry are excluded.

## Deferred decisions

These features are intentionally outside the first implementation unless separately approved:

- cloud synchronization or accounts;
- medical recommendations;
- social features or gamification beyond a factual streak;
- beverages or nutrient tracking;
- automatic startup with Windows;
- metric display and unit conversion;
- cross-platform versions;
- editable or deletable individual historical entries beyond undoing the latest current-day entry.
