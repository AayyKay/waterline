# Waterline rebuild charter

Status: Phase 0 approved

Baseline: `main` at `0a266ba` (`v2.0.1`)
Target: native .NET 8 WPF application for Windows 10 and Windows 11

## Purpose

Waterline will be rebuilt as a polished native Windows hydration application with a new product design. Existing Waterline screenshots, the former Electron interface, and the current WPF styling are excluded as visual references. They may be inspected only to identify behavior, data formats, upgrade obligations, and regressions.

The rebuild must:

- use WPF and native Windows APIs without a browser, Electron, WebView, or hosted interface;
- keep hydration data and settings on the user's device;
- preserve compatible data from installed versions;
- provide a cohesive dashboard, desktop widget, tray experience, notifications, sounds, installer, and update flow;
- remain usable with a mouse, keyboard, touchpad, screen reader, different window sizes, and common Windows scaling levels;
- use maintainable XAML, styles, templates, vector resources, and C# rather than rendered screenshots as interface elements.

## Source-of-truth hierarchy

When requirements conflict, use this order:

1. The current owner's approved decisions.
2. This rebuild specification after approval.
3. Functional behavior required to preserve user data and expected workflows.
4. Current or historical code as implementation evidence only.

Old visual treatments never resolve a new design decision.

## Phase 0 deliverables

- [Product specification](product-specification.md)
- [Technical foundation](technical-foundation.md)
- [Quality plan and rebuild phases](quality-and-phases.md)

No production implementation, generated imagery, release tag, or published release is part of Phase 0.

## Proposed decisions for approval

The first implementation phase will use these defaults unless the owner changes them during review:

- Ounces remain the supported unit for the first rebuilt release; the data model will allow a later metric display preference without rewriting stored entries.
- The daily goal defaults to 80 oz and quick-add actions default to 8, 12, and 16 oz.
- The widget remains optional, always on top while open, movable, and available in expanded and compact forms.
- Closing the dashboard keeps Waterline available in the notification area; Quit exits it completely.
- Reminders remain disabled on a clean install and run only on selected days within a same-day schedule.
- Sounds remain enabled by default but can be disabled independently from reminders.
- Waterline does not start with Windows unless the owner later approves that feature.
- Updates are checked after startup and manually from Settings. Installation always requires a visible user action.
- The rebuilt design supports Windows light/dark system accessibility settings where they affect contrast, but Waterline's product theme remains a dark navy experience.

## Approval gate

Phase 1 implementation may begin only after the owner approves this charter and the three linked specifications, including any requested changes.
