# Technical foundation

Status: Phase 0 approved

## Architecture goals

The codebase should make data preservation, time-dependent behavior, and visual iteration independently testable. WPF-specific types should stay at the presentation boundary. Domain calculations should accept explicit clocks and inputs so tests do not depend on the actual date, local state, or network.

## Proposed solution structure

```text
Waterline.sln
src/
  Waterline.App/             WPF startup, windows, views, resources
  Waterline.Core/            hydration, pace, reminder and state rules
  Waterline.Infrastructure/  JSON storage, migration, GitHub updates, Windows integration
tests/
  Waterline.Core.Tests/
  Waterline.Infrastructure.Tests/
  Waterline.App.Tests/
assets/
  brand/
  icons/
  audio/
docs/rebuild/
```

This is a target structure, not a requirement to rewrite working code at once. Migration should occur in coherent, compiling increments.

## Responsibility boundaries

### Core

Core owns immutable or narrowly mutable models and deterministic services for:

- hydration entries and daily totals;
- goal progress;
- seven-day summaries;
- pace classification and target calculation;
- reminder eligibility and next-due calculation;
- validation rules;
- local-day and unit semantics.

Core has no WPF, filesystem, HTTP, notification, audio, or process dependencies.

### Infrastructure

Infrastructure owns:

- versioned JSON persistence and migration;
- atomic save, backup, recovery, and quarantine;
- import discovery for installed Waterline versions;
- GitHub release checks and installer downloads;
- Windows notification, tray, startup, audio, and monitor services;
- application paths and runtime diagnostics.

Each external behavior is behind an interface consumed by the application layer.

### WPF application

The application project owns:

- composition and startup;
- application lifetime and single-instance activation;
- views and view models;
- commands, navigation, dialogs, and focus management;
- design tokens, styles, templates, vector assets, animations, and accessibility metadata;
- dashboard and widget windows.

View models do not access named XAML elements or open windows directly. Views may contain narrowly scoped code for native window interop, focus transfer, dragging, and animation coordination.

## State model

The new persisted document will have an explicit schema version and stable identifiers. Proposed logical shape:

```json
{
  "schemaVersion": 1,
  "settings": {
    "dailyGoalOz": 80.0,
    "reminders": {
      "enabled": false,
      "intervalMinutes": 60,
      "startLocalTime": "09:00:00",
      "endLocalTime": "17:00:00",
      "days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
    },
    "soundsEnabled": true
  },
  "drinks": [
    {
      "id": "stable-unique-id",
      "amountOz": 12.0,
      "recordedAt": "2026-09-08T10:30:00-05:00"
    }
  ],
  "desktop": {
    "widgetMode": "expanded",
    "widgetPlacement": null
  },
  "runtime": {
    "lastNotificationAt": null
  }
}
```

Existing `v2.0.1` JSON is schema zero and must migrate without changing its drink timestamps, amounts, reminder choices, or goal. Unknown fields should be tolerated during reads.

## Persistence contract

The canonical path remains `%LOCALAPPDATA%\Waterline\state.json` to preserve installed-version continuity.

Saving follows this contract:

1. Validate and serialize the complete candidate state in memory.
2. Write to a unique temporary file in the same directory.
3. Flush file content to disk.
4. Preserve the last valid state as `state.backup.json`.
5. Atomically replace the canonical file.
6. Keep only bounded diagnostic metadata that contains no hydration details.

Loading follows this contract:

1. Read and validate the canonical file without modifying it.
2. Apply ordered migrations in memory.
3. If canonical state is invalid, try the backup.
4. If backup succeeds, present a non-blocking recovery notice and do not overwrite the corrupt original until the recovered state is explicitly saved.
5. If both fail, quarantine copies and present a recovery choice. Never silently replace existing data with defaults.

Persistence writes are serialized so dashboard, widget, timer, and shutdown cannot race. Save failures remain visible and retryable; the in-memory state is retained.

## Legacy data discovery

The native `v2.0.x` file is the first migration source. The Electron release stored settings and date-keyed drink arrays in Chromium local storage under its per-user application-data directory. Phase 2 will add read-only discovery for known legacy locations, show exactly what was found, and import only when the native state is absent or the user approves a merge.

Importer requirements:

- never delete or modify legacy data;
- deduplicate using timestamp, amount, and stable imported identity;
- report imported and skipped counts;
- create a pre-import backup;
- remain safe to run more than once;
- test representative `v1.0.0`, `v1.0.1`, `v2.0.0`, and `v2.0.1` fixtures.

The exact Electron storage paths and LevelDB-reading approach must be proven on packaged fixtures before implementation is approved.

## Time model

Persist entry instants with their offsets. Calculate “today” using the current local timezone, but keep the original timestamp unchanged. Inject a clock and local-time provider into calculations.

The application listens for:

- local date rollover;
- system time and timezone changes;
- session resume;
- display and DPI changes.

On those events it recomputes today, history, pace, and reminder plans. Reminder processing sends at most one currently eligible notification after resume and schedules the next interval from that notification; it never replays every missed interval.

## Application lifetime

- A named mutex enforces one process per interactive user.
- A second launch sends an activation message, then exits.
- Activation restores a hidden or minimized dashboard and brings it forward using supported Windows focus behavior.
- Closing the dashboard hides it to the notification area.
- Quit closes all windows, stops timers, completes or reports pending state writes, disposes native resources, and exits.
- Snapshot and test modes use isolated temporary state and disabled network/update checks.

## Presentation foundation

Resources are split by responsibility:

```text
Resources/
  Tokens/       Color, typography, spacing, radii, elevation, motion
  Controls/     Button, input, toggle, check box, combo box, scroll bar
  Surfaces/     Menu, tooltip, dialog, card, notification-like callout
  Icons/        Frozen DrawingImage and Geometry resources
  Themes/       Standard and high-contrast adaptations
```

Every interactive template covers rest, pointer-over, pressed, focused, disabled, selected/checked, validation-error, and busy states where applicable. Templates use semantic resource tokens rather than literal colors.

The design uses vector assets for interface icons. Raster assets are limited to approved brand artwork where a vector or multi-resolution Windows icon is unsuitable. The `.ico` package will contain optically reviewed 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel representations as appropriate.

## Window and DPI behavior

Window placement is stored with monitor identity, normalized work-area anchor, size, and DPI context. Restore logic clamps windows into the current work area when a display is removed or its scaling changes.

Custom chrome must retain:

- resize borders and minimum size;
- double-click maximize/restore;
- Alt+Space system menu;
- Windows snap behavior where supported;
- correct maximize bounds that exclude the taskbar;
- separate hover/pressed treatment for Close;
- keyboard and automation names for title-bar controls.

The widget's transparent-window rendering and resizing will be profiled because WPF per-pixel transparency can affect hardware acceleration and text rendering.

## Motion and audio

Motion is implemented through reusable WPF storyboards or state transitions. Durations and easing are tokens. Animations never block input, and rapid repeated logging converges on the current state rather than queueing long sequences.

The application observes the Windows client-area animation setting. Reduced motion removes ambient loops and spatial travel while retaining immediate state changes and subtle opacity feedback.

Audio cues are original Waterline assets or deterministic synthesis approved during design. Playback is asynchronous, cancellable, bounded, and mixed to prevent overlapping bursts. Logging and reminder cues are distinct. Disabling Waterline sounds takes effect immediately.

## Updates and security boundary

The update service uses a bounded timeout, cancellation, a declared user agent, and GitHub's official HTTPS release endpoints. It validates that the asset belongs to the expected repository, follows the version-matched filename convention, and includes a well-formed SHA-256 digest supplied by GitHub Releases. The downloaded installer is closed and verified against that digest before installation can be offered. Phase 8 records that no code-signing certificate is configured for 2.1.0, so the unsigned-installer warning remains an explicit release risk.

The installer file is downloaded to a unique temporary path, flushed, closed, and then launched visibly. No command line is constructed from release metadata. Update failures are isolated from application state.

## Dependency policy

Prefer .NET and WPF platform APIs. A third-party package requires a concrete maintenance or accessibility benefit, an active maintenance record, a compatible license, and approval during the phase that introduces it. No UI framework may introduce WebView or browser rendering.

## Observability

Local diagnostics may record timestamps, application version, operation category, and exception type/message. They must exclude drink amounts, schedules, and full serialized state. Diagnostic retention is bounded and can be cleared from Settings.

## Build and test baseline

The solution will build with the .NET 8 SDK on `windows-latest`. Release output remains a self-contained x64 WPF application until additional architectures are approved.

The release workflow must remain tag-triggered, but no tag is created until the owner explicitly approves publication after Phase 8.
