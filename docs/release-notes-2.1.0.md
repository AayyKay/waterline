# Waterline 2.1.0

Waterline 2.1.0 completes the native Windows rebuild with a new dashboard, desktop widget, schedule and settings workspaces, notification-area integration, and a safer upgrade path from 2.0.1.

## Highlights

- Rebuilt Waterline as a native .NET 8 WPF application for Windows 10 and Windows 11.
- Added a responsive Today dashboard, hydration history, pace guidance, goals, schedules, and quick or custom drink logging.
- Added expanded and compact always-on-top widgets with remembered placement.
- Added notification-area controls, reminder notifications, single-instance restore, close-to-tray behavior, and original Waterline sounds.
- Added keyboard navigation, visible focus states, high-contrast treatments, and reduced-motion behavior.

## Data and upgrade safety

- Keeps hydration history and settings locally in `%LOCALAPPDATA%\Waterline`.
- Preserves the existing profile during install, upgrade, and uninstall.
- Creates a durable copy before the first schema migration and a separate backup before importing older Electron data.
- Adds an opt-in, read-only importer for compatible Waterline Electron data.
- Verifies downloaded update installers against the SHA-256 digest supplied by GitHub Releases before offering installation.

## Installation

Download `Waterline-Setup-2.1.0.exe` and run it. Installing over Waterline 2.0.1 uses the same per-user application identity and upgrades in place.

The 2.1.0 installer is not code-signed. Windows may display an unknown-publisher warning; verify the published SHA-256 file before running the installer.

