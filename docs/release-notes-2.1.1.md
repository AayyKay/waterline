# Waterline 2.1.1

Waterline 2.1.1 is a focused visual and window-sizing hotfix for the native Windows application.

## Fixes

- Caption controls now render the minimize, maximize/restore, and close glyphs completely at supported display scales.
- Caption keyboard-focus outlines remain inside the window instead of clipping against its upper edge.
- Expanding the compact widget now restores its full 400 × 500 layout, including quick logging, reminder, pace, and dashboard actions.
- The Today reservoir now follows the approved Living Water design with a glass vessel, layered fill, curved meniscus, subtle caustics, and restrained bubbles.
- The reservoir scale uses the configured ounce goal and keeps its bottom zero fully visible.
- Navigation icons retain their native coordinate padding so the Insights bars and other edge strokes are not clipped.

## Updating

Run `Waterline-Setup-2.1.1.exe` over the existing installation. Waterline uses the same per-user application identity, upgrades in place, and preserves local hydration data and settings.

The installer is not code-signed, so Windows may display an unknown-publisher warning. Verify the published SHA-256 file before running it.
