# Phase 3 native shell and component library

Status: approved September 9, 2026

Branch: `codex/native-shell-components`

## Native shell

The inherited dashboard shell has been replaced with a new WPF structure based on the approved Living Water direction. The shell provides:

- custom resizeable `WindowChrome` with purpose-drawn minimize, maximize/restore, and close controls;
- the six approved destinations in order: Today, Insights, Goals, Schedule, Widget, and Settings;
- compact navigation that moves Widget and Settings into a labeled More menu below 1040 effective pixels;
- `Ctrl+1` through `Ctrl+6` destination shortcuts and normal tab traversal;
- an activity rail that moves below primary content at the 900-pixel minimum width;
- a fixed title bar and navigation row with independently scrolling content;
- subtle decorative atmosphere behind quiet content regions.

The shell remains fully native WPF. It contains no browser renderer, WebView, embedded web content, or screenshot-based interface.

## Resource system

Application resources are separated by responsibility:

```text
Resources/
  Tokens/Colors.xaml
  Tokens/Typography.xaml
  Icons/Icons.xaml
  Controls/Controls.xaml
  Surfaces/Surfaces.xaml
```

The token dictionaries implement the approved navy, cyan, aqua, typography, spacing, focus, success, warning, and error language. Interface symbols use maintainable WPF geometry resources rather than font glyphs or traced artwork.

The component library includes native templates for:

- primary, secondary, quiet, destructive, icon, caption, and navigation buttons;
- text and numeric entry states, including focused, read-only, disabled, and invalid;
- switches, check boxes, mixed check boxes, and pill toggles;
- combo boxes and combo-box items;
- progress bars, lists, list items, and scrollbars;
- context menus, menu items, separators, and tooltips;
- cards, status chips, callouts, and dialog surfaces.

Pressed controls move content by one effective pixel without scaling the whole control. Keyboard focus is drawn inside templates so it remains visible in native captures. Disabled, checked, selected, validation, and busy-ready layout states do not shift geometry.

## Accessibility and system behavior

Interactive controls have accessible names where their visible content is insufficient. Navigation selection updates its automation name. The custom amount dialog uses a cyclical tab scope, assigns initial focus, supports Enter and Escape, returns focus through modal ownership, and reports validation inline without a Windows message box.

High contrast replaces product brushes with current Windows system brushes, removes decorative atmosphere, and preserves explicit boundaries. The theme responds to `SystemParameters.HighContrast` changes while the application is running.

The shell entrance runs only when Windows client-area animations are enabled. With animations disabled, content appears immediately in its final position. No ambient loop is part of the Phase 3 shell.

## Deterministic gallery and captures

The shell itself is the Phase 3 component gallery. It presents representative action, input, selection, progress, list, menu, tooltip, dialog, success, disabled, invalid, and keyboard-focus states. Snapshot mode continues to use isolated temporary state and disables update traffic.

Verified native captures:

- preferred 1280 × 860 resting state;
- minimum 900 × 650 compact navigation and single-column reflow;
- explicit keyboard focus on a secondary action;
- forced system-brush high-contrast treatment with atmosphere removed;
- focused invalid custom-amount dialog with inline error.
- lower-gallery normal and high-contrast states covering progress, selection, scrollbar, and success feedback.

All captures rendered from the compiled WPF application. No generated image is used by the interface.

## Verification and review boundary

Automated and deterministic checks completed for this candidate:

1. `dotnet build Waterline.sln --configuration Release` — zero warnings and zero errors.
2. `dotnet run --project Waterline.Tests\Waterline.Tests.csproj --configuration Release` — all 24 domain and persistence checks passed.
3. `git diff --check` — no whitespace errors.
4. Seven isolated native WPF capture modes launched and exited without runtime failure.

The installed computer-use runtime exposed browser surfaces but no native-app binding on this host. Hover/pressed interaction, complete keyboard traversal, Alt+Space, maximize/restore, snap behavior, popup-menu interaction, and DPI captures above the host's current scale remain on the ongoing manual regression checklist. The owner approved the Phase 3 implementation candidate on September 9, 2026.

The current widget and notification-area menu retain their transitional Phase 2 presentation. Their product-specific rebuild is intentionally assigned to Phase 6; the shared control resources are already available to them.
