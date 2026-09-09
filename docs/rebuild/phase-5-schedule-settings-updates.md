# Phase 5 Schedule, settings, and updates

Status: approved September 9, 2026

Branch: `codex/schedule-settings-updates`

## Schedule and goals

Schedule is now a distinct destination with a true draft. It provides:

- an independent reminder toggle, locale-aware start and end time entry, supported reminder intervals, and seven explicit day controls;
- immediate validation that the end time follows the start time on the same day;
- a live next-reminder explanation that never implies an unsaved notification has already been scheduled;
- Save and Cancel actions that persist only a valid draft and leave the active reminder schedule unchanged until Save succeeds;
- three-way navigation protection that lets the user keep editing, discard the draft, or save it before leaving.

Goals is also a distinct destination. Its daily-goal editor uses the same save/cancel draft contract and explains how goal changes affect progress without rewriting historical drink amounts.

## Settings and local data

Settings now provides draft-based controls for the daily goal, Waterline sounds, and the widget's default expanded or compact presentation. It also exposes:

- the application version and current update status;
- the exact local state location, recovery health, and an explicit folder action;
- a plain-language privacy statement covering local hydration data, preferences, schedules, widget placement, and diagnostics;
- a bounded diagnostic log location and status without including hydration entries, schedules, or full persisted state.

Preference saves are atomic. If persistence fails, active values are restored and the draft remains available for correction or retry.

## Updates and installer safety

The update experience has designed idle, checking, current, available, downloading, ready, installing, failed, and development-build unavailable states. Automatic and manual checks use a bounded network timeout and identify the client with a Waterline user agent.

Only HTTPS release pages and installer assets from the official `AayyKay/waterline` GitHub repository are accepted. The installer filename must exactly match the advertised version. Downloads use a unique temporary filename and are flushed and closed before Waterline enables the separate, visible Install action. Update failures are isolated from hydration logging and recorded only as sanitized diagnostic events.

## Responsive, accessibility, and deterministic states

Schedule and Settings remain usable at the 900 × 650 minimum size with fixed navigation, a scrollable content region, and no horizontal clipping. Every day control has an explicit accessible name, validation and update states use text in addition to color, and the unsaved-changes dialog exposes three plainly labeled choices.

Deterministic snapshot fixtures cover Goals, Schedule rest/modified/invalid/compact/high-contrast states, Settings rest/modified/compact/high-contrast states, the update-state sequence, and the unsaved-changes dialog. Snapshot state remains isolated in a temporary profile and does not touch installed Waterline data.

## Verification and remaining approval gate

The Release solution build completes without warnings or errors. The automated console suite contains 31 passing checks, including trusted-installer acceptance and rejection of wrong hosts, repositories, filenames, and versions. Visual review covers standard and compact Schedule and Settings layouts, dirty and invalid drafts, update available/downloading/ready/failed states, and the unsaved-changes dialog.

The owner approved the Phase 5 implementation candidate on September 9, 2026. Keyboard-only traversal, screen-reader inspection, live offline/online update retry, completed installer handoff, and mixed-DPI checks remain on the manual regression checklist and continue into the full release gate.
