# Phase 1 design system

Status: approved

This system translates the selected Direction D2 into deterministic, native WPF rules. Generated references communicate intent; the values below govern implementation.

## Identity

Waterline's product signature is a living vertical reservoir within a calm deep-ocean workspace. The reservoir represents actual hydration progress. Atmospheric water motion supports the identity without carrying data or interfering with content.

The interface character is calm, dimensional, precise, and responsive. It avoids novelty-aquarium styling, generic dashboard cards, large uncontrolled glow, and browser-like framing.

## Color tokens

| Token | Value | Use |
| --- | --- | --- |
| Canvas | `#030914` | window background and deepest depth |
| Canvas raised | `#05101D` | subtle atmospheric layer |
| Surface | `#071526` | primary content surfaces |
| Surface raised | `#0B1D31` | controls, menus, dialogs |
| Surface hover | `#102B43` | pointer-over state |
| Surface pressed | `#0D2235` | pressed state |
| Activity rail | `#061221` | quiet right-side information rail |
| Boundary | `#32677F` | meaningful control boundaries |
| Divider | `#183447` | decorative grouping lines |
| Text primary | `#F2F8FC` | headings and primary values |
| Text secondary | `#A9BAC8` | descriptions and secondary values |
| Text tertiary | `#7F95A6` | metadata that remains readable |
| Cyan | `#43E4F7` | progress, active state, selected navigation |
| Aqua | `#68F2D0` | water highlights and positive state |
| Focus | `#7DEBFF` | keyboard focus only |
| Success | `#67E1B0` | completion with icon/text support |
| Warning | `#FFC46B` | attention states |
| Error | `#FF7D8C` | validation and failure states |
| Scrim | `#B8020710` | modal backdrop |

Primary, secondary, tertiary, cyan, focus, and error text all exceed a 4.5:1 contrast ratio on their intended surfaces. The normal meaningful control boundary is approximately 3:1 against Surface. Decorative dividers are not used to communicate state.

Atmospheric effects derive from Canvas, Cyan, and Aqua with local opacity between 2% and 10%. No new foreground color is sampled directly from a generated image.

## Typography

Use installed Windows typography without bundling a web font:

| Role | Family | Size | Weight | Line height |
| --- | --- | --- | --- | --- |
| Display value | Segoe UI Variable Display | 52 | Semibold | 60 |
| Page title | Segoe UI Variable Display | 28 | Semibold | 36 |
| Section title | Segoe UI Variable Display | 20 | Semibold | 28 |
| Control label | Segoe UI Variable Text | 14 | Semibold | 20 |
| Body | Segoe UI Variable Text | 14 | Regular | 21 |
| Secondary | Segoe UI Variable Text | 12 | Regular | 18 |
| Compact metadata | Segoe UI Variable Text | 11 | Semibold | 16 |

Segoe UI is the fallback. Amounts, percentages, times, chart axes, and changing counts use tabular numerals. Uppercase and letter spacing are reserved for rare metadata labels and never applied to sentences.

Text does not fall below 11 effective pixels. Essential actions and values remain at least 12.

## Spacing and layout

The spacing scale is `4, 8, 12, 16, 20, 24, 32, 40, 48, 64` effective pixels. Layouts use 8 as the normal rhythm and 4 only for optical adjustment.

- Title bar height: 40
- Navigation command row: 56
- Window content inset: 24 at preferred size, 16 compact
- Primary/rail gap: 1-pixel divider plus 24 internal inset
- Standard control height: 40
- Primary control height: 48
- Compact icon control: 36 minimum
- Standard pointer target: 40 × 40 minimum
- Compact widget: no smaller than 88 × 160 before scaling

## Geometry and depth

| Element | Radius |
| --- | --- |
| Window interior surfaces | 12 |
| Dialog and expanded widget | 16 |
| Standard controls | 8 |
| Primary controls | 10 |
| Compact widget | 18 |
| Pills/status badges | half the element height |

Depth uses one soft exterior shadow per floating surface and one subtle upper edge light. Interior dashboard regions use dividers and value changes instead of independent shadows. Bright glow is limited to active water, focus, and short confirmation moments.

## Icon system

Interface icons use a custom 20 × 20 drawing grid with a nominal 1.5-pixel optical stroke at 100% scaling. Shapes use rounded joins and consistent terminal treatment. A 16 × 16 simplified variant is created when downscaling would close counters or blur strokes.

Required families:

- Today, Insights, Goals, Schedule, Widget, Settings;
- log water, custom amount, undo, reminder, pace, history;
- expand, collapse, overflow, close, minimize, maximize, restore;
- check, warning, error, update, download, retry, recovery;
- tray and notification variants.

Generated-reference icons are placement examples only and are never traced.

## Navigation

The wide command row contains Today, Insights, Goals, Schedule, Widget, and Settings in that order. Selected state combines cyan text/icon, semibold weight, and a 2-pixel lower indicator. Hover uses Surface hover without shifting geometry. Keyboard focus adds the Focus keyline outside the hover/selected treatment.

At compact widths, Widget and Settings move into a labeled More menu. Today, Insights, Goals, and Schedule remain visible. The More button communicates whether its current child destination is selected.

## Water rendering

The reservoir, idle background, logging response, reduced-motion alternative, and rendering budgets are defined in [Direction D2](direction-d2-living-water.md).

The reservoir outline, fill, meniscus, and percentage are one accessible control. Automation exposes its name, current amount, goal, and percentage. Color and fill height are never the only representations of progress.

## Control language

All controls use native WPF templates backed by semantic tokens.

### Buttons

- Primary: cyan tonal fill, dark readable label, subtle upper highlight.
- Secondary: Surface raised fill with Boundary outline.
- Quiet: transparent at rest with a visible hover surface.
- Destructive: dark surface with Error text/border; solid error fill only for final irreversible confirmation.
- Icon-only: accessible name and tooltip, 36 × 36 visual size within a 40 × 40 target where space permits.

Pressed state moves content by at most one effective pixel and darkens the surface. It does not scale the entire control. Disabled state reduces contrast while keeping labels readable and exposes the disabled reason through adjacent text or accessible help.

### Inputs

Text, numeric, time, and combo inputs share a 40-pixel height, Surface raised background, Boundary outline, 8-pixel radius, selection color, and clear caret. Focus uses a 2-pixel Focus outline without changing layout. Errors combine Error outline, icon, and inline message.

Numeric amount input supports locale-aware decimal entry while storing normalized ounces. Schedule time inputs use locale-aware display and an explicit keyboard-editing contract.

### Toggles and check boxes

Switches use a moving thumb plus On/Off accessible state. Check boxes use a custom square and check geometry. Day selectors are toggle buttons with full weekday automation names. No selection depends on fill color alone.

### Menus, tooltips, and dialogs

Menus and tooltips use opaque Surface raised backgrounds to remain readable over atmospheric motion. Menus have consistent icon and shortcut columns, selected/focused rows, separators, and submenus. Dialogs use a modal scrim, explicit title, primary/secondary actions, focus containment, Escape handling, and focus return.

### Scrollbars and lists

Scrollbars have a 12-pixel interaction channel with a 6-pixel resting thumb that expands on hover/focus. Lists provide hover, selection, keyboard focus, empty state, and accessible item position. Essential content remains discoverably scrollable.

## Feedback states

- Success: short Aqua confirmation line/icon plus concise message.
- Validation: inline Error state announced when submission fails.
- Save failure: persistent retryable callout; unsaved in-memory state remains intact.
- Busy: progress indicator and stable label; layout does not jump.
- Offline/update failure: plain-language state with Retry.
- Goal complete: reservoir reaches goal, motion settles, Success icon/text appears, reminders stop.

## Sound language

The log cue is a soft water/glass pair lasting 220–280 ms. The reminder cue is a warmer three-part liquid chime lasting 550–700 ms. Neither uses the default Windows alert sound. Both have soft attacks, short tails, and controlled peak level. Repeated log actions coalesce playback rather than stacking.

Sound assets or deterministic synthesis will be auditioned and approved in Phase 7. Phase 1 approves only this character and behavior.
