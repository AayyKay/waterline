# Phase 6 Widget and Windows integration

Status: approved September 9, 2026

Branch: `codex/widget-windows-integration`

## Desktop companion

The Widget destination now explains and launches the working desktop companion. Only one widget may exist, and subsequent open commands focus the existing instance.

The expanded 400 × 500 widget provides current progress, remaining amount, 12 oz primary logging, 8 oz and 16 oz quick actions, custom logging, reminder status, pace status, collapse, close, and dashboard restore. The compact 104 × 168 widget preserves brand identity, percentage, reservoir progress, and one keyboard-focusable expand action. Both remain topmost while open and bind to the same application state as Today.

Changing the saved widget preference while the widget is open updates its presentation immediately. Expanding and compacting preserve the normalized nearest-edge anchor rather than returning the window to a fixed corner.

## Placement and display changes

Widget placement persists monitor identity, normalized work-area anchors, logical size, and DPI scale. Capture uses the native window rectangle and the active monitor work area. Restore selects the saved monitor when available, falls back to the primary display when a monitor was removed, and clamps the complete widget into the available work area.

The application declares Per-Monitor V2 awareness. Widget placement is reevaluated after display-topology changes, and DPI changes retain the widget on its new monitor rather than snapping it back to the prior display.

## Notification area and lifecycle

The notification-area service uses a Waterline-themed native menu with Open Waterline, Open Widget, contextual Pause/Resume reminders, and Quit. The menu retains native keyboard behavior. Double-clicking the icon restores the dashboard, and selecting a reminder balloon restores a hidden or minimized dashboard.

Closing the dashboard hides it without ending reminder processing. Quit explicitly closes and persists the widget, closes the dashboard, stops application timers and update work, disposes tray and single-instance resources, and exits.

A mutex and activation event are scoped to the current interactive Windows user. A second launch signals the existing process and exits. The existing process restores hidden or minimized dashboard state, requests foreground activation, and flashes the taskbar as a supported fallback when Windows declines focus transfer.

System time changes refresh derived local state. Session resume performs one current reminder evaluation; it never replays every missed interval. Pause state is distinct from disabling the saved schedule and is included in local runtime state.

## Windows identity assets

The executable, taskbar, Alt+Tab, main window, widget, installer, and uninstaller share the Waterline application icon. Its `.ico` contains intentional 16, 20, 24, 32, 40, 48, 64, 128, and 256 px frames. A simplified transparent tray and notification mark contains 16, 20, 24, 32, 40, 48, and 64 px frames. Both packages are generated deterministically from Waterline's approved vector droplet by the checked-in icon builder.

## Verification and remaining approval gate

The Release solution build completes without warnings or errors. The automated console suite contains 34 passing checks, including normalized widget anchor capture, removed-monitor fallback, work-area size clamping, widget-mode validation, and invalid-placement rejection.

Deterministic captures cover standard and 900 × 650 widget-management layouts, populated expanded and compact widgets, and both widget forms under the high-contrast theme. Captures use an isolated temporary state location and do not affect installed Waterline data.

The owner approved the Phase 6 implementation candidate on September 9, 2026. Live tray keyboard inspection, notification activation, close-to-tray and second-launch journeys, monitor disconnect, and mixed-DPI movement remain on the Windows 10 and Windows 11 manual matrix and continue into the full release gate.
