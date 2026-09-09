# Phase 7 Motion, audio, and polish

Status: approved September 9, 2026

Branch: `codex/motion-audio-polish`

## Motion language

Waterline now uses a shared motion policy and duration tokens. Motion runs only while the relevant window is visible and not minimized, and it stops when Windows client-area animation is disabled or high-contrast mode is active. Snapshot baselines force reduced motion unless they explicitly request a motion fixture, keeping visual comparisons deterministic.

The dashboard uses one low-contrast 28-second current and a two-pixel reservoir-surface drift. Logging moves the fill to the exact value over 500 ms without overshoot, briefly increases the surface response, passes one clipped highlight upward, and cross-fades the total. Undo uses the same response at reduced intensity. Rapid changes replace active animation clocks instead of queueing them.

The expanded widget uses one restrained 18-second current. The compact widget adds only a faint 16-second edge sweep; it does not add particles or secondary content. Entrance movement is omitted under reduced motion, while all final values and control states remain immediate.

## Original Waterline audio

The previous runtime tone generator has been replaced by two checked-in deterministic PCM assets:

- `waterline-log.wav`: a 270 ms water-and-glass confirmation;
- `waterline-reminder.wav`: a distinct 660 ms three-part liquid chime.

Both are mono 44.1 kHz cues with bounded peaks. A checked-in builder reproduces the files from deterministic synthesis. Playback uses one asynchronous channel: a new cue stops and replaces the active cue, rapid logging cannot stack bursts, disabling Waterline sounds stops active playback immediately, and application shutdown disposes the player and stream. Embedded assets have a deterministic synthesis fallback if a packaged resource cannot be opened.

## Final interface polish

Widget and dashboard motion now use the same approved easing and timing vocabulary. The widget contains no text glyphs standing in for interface icons, all small controls retain explicit automation names and tooltips, and the minimum-width Widget destination wraps without clipping. The deterministic hydration fixture now splits daily totals above 64 oz into valid individual entries, matching the production per-entry contract.

The general UI refresh timer has been reduced from one second to 30 seconds. Reminder eligibility retains its independent 20-second timer. Hidden and minimized windows stop ambient storyboards, avoiding continuous visual work while Waterline is operating only from the notification area.

## Verification and remaining approval gate

The Release solution build completes without warnings or errors. The automated console suite contains 35 passing checks, including deterministic WAV structure, durations, peak bounds, and cue distinction in addition to the earlier domain, persistence, update, and placement coverage.

Deterministic captures cover static and reduced-motion Today, expanded widget, and compact widget states. Explicit motion fixtures cover the dashboard logging response, expanded current, and compact edge sweep. Snapshot data remains isolated from installed Waterline state and snapshot logging is silent.

The owner approved the Phase 7 implementation candidate on September 9, 2026. Subjective listening on representative speakers and headphones, reduced-motion inspection with the Windows setting disabled, rapid repeated logging, and dashboard/widget CPU and GPU profiling on integrated graphics remain on the manual regression matrix and continue into the full release gate.
