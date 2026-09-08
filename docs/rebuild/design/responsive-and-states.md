# Phase 1 responsive and state specification

Status: final draft for Phase 1 approval

## Window classes

| Class | Effective width | Dashboard behavior |
| --- | --- | --- |
| Wide | 1180 and above | six navigation destinations; primary canvas and activity rail side by side |
| Standard | 1040–1179 | six destinations with reduced gaps; narrower rail; compact page insets |
| Compact | 900–1039 | Widget and Settings in More; activity rail moves below the primary canvas |

The supported minimum remains 900 × 650 effective pixels. Height pressure introduces vertical scrolling within content while title bar and navigation remain fixed. Logging actions remain visible in the first viewport at the minimum size.

At 150% and 200% Windows scaling, effective-pixel rules remain unchanged. Physical pixels increase with DPI. No layout branches on raw physical resolution.

## Dashboard reflow

At Wide and Standard widths, the dashboard uses an asymmetric main region and activity rail. At Compact width:

1. amount and reservoir remain in one row;
2. logging controls wrap as one primary full-width action plus three equal secondary actions;
3. reminder and pace form a two-column row when space allows;
4. recent entries span full width below;
5. long content scrolls under the fixed navigation row.

The reservoir may shorten from 500 to 330 effective pixels as height decreases, but its percentage and minimum readable scale remain intact.

## Widget sizes

- Expanded preferred: approximately 400 × 500 effective pixels.
- Expanded minimum: 360 × 440.
- Compact target: approximately 96 × 168.

Expanded widget content scrolls only as a last resort at extreme scaling. The compact widget never clips percentage or reservoir. Compact/expanded changes preserve the nearest work-area edge and keep the entire window visible.

## Component-state matrix

| Component | Required visual states |
| --- | --- |
| Navigation | rest, hover, selected, keyboard focus, selected+focus, overflow-child selected, disabled |
| Button | rest, hover, pressed, keyboard focus, disabled, busy, default action, destructive |
| Text/numeric/time input | rest, hover, focused, populated, invalid, disabled, read-only |
| Toggle/check box | off, on, hover, pressed, focus, disabled, mixed where supported |
| Menu item | rest, hover, focus, selected/checked, disabled, submenu open |
| Dialog | opening, active, validation error, busy, closing, focus return |
| Reservoir | empty, partial, goal, over goal, logging transition, undo transition, reduced motion |
| History | empty, partial week, completed day, selected day, over-goal day |
| Reminder | disabled, before schedule, active/scheduled, due, sent, complete, paused, unavailable |
| Update | idle, checking, current, available, downloading, ready, installing, failed, offline |
| Persistence | clean, saving, saved, failed, backup recovered, unrecoverable |

## Representative review states

The first native component gallery and dashboard implementation must render deterministic fixtures for:

- empty first launch;
- 45% progress with recent entries;
- behind pace;
- goal complete;
- invalid custom amount;
- save failure;
- update available and downloading;
- backup recovery;
- expanded and compact widget;
- keyboard focus on each component family;
- reduced motion and high contrast.

## Atmospheric-motion boundaries

- Text and controls sit on quiet zones without bright motion behind them.
- Main-canvas background effects remain between 2% and 10% local contrast.
- The activity rail has no continuous particle motion.
- Animation pauses while minimized or hidden.
- Repeated logging replaces the current response animation rather than queueing another full sequence.
- Battery saver may reduce frame rate or disable decorative motion without affecting progress feedback.

## High contrast

High contrast replaces product background and foreground tokens with compatible system brushes, removes atmospheric layers and shadows, uses explicit boundaries, and preserves the reservoir through outline, fill pattern/position, and text. Custom chrome controls retain system-recognizable shapes and automation names.

## Keyboard model

- Alt+Space opens the system menu.
- Ctrl+1 through Ctrl+6 navigate to Today, Insights, Goals, Schedule, Widget, and Settings.
- Tab follows visual reading order; Shift+Tab reverses it.
- Enter and Space activate according to control conventions.
- Escape closes the topmost menu, dialog, or transient panel.
- The compact widget is one focusable expand command; arrow movement is not required.
- Menu access and all logging workflows remain possible without a pointer.
