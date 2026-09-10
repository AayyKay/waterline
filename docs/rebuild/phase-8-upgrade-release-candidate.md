# Phase 8 Upgrade and release candidate

Status: approved September 9, 2026

Branch: `codex/upgrade-release-candidate`

Version proposal: `2.1.0`

## Candidate scope

The candidate completes the native rebuild and preserves the established per-user installer identity used by Waterline 2.0.1. The Release publish is a self-contained x64 single-file application with the native LevelDB dependency extracted by the .NET host. The Inno Setup package performs an in-place upgrade, closes a running copy before replacement, retains the previous install directory, and preserves user data on uninstall.

Before installation begins, the installer copies an existing `state.json` to `%LOCALAPPDATA%\Waterline\upgrade-backups\state-before-2.1.0.json`. Failure to create that safety copy blocks the installation. Application-level migration also preserves the original schema-zero file as `state.pre-schema-1.json`; legacy import creates a unique `state.pre-import.*.json` copy before merging.

## Upgrade rehearsal

The last public release is `v2.0.1`, published September 4, 2026. Its public installer was downloaded independently and matched the GitHub release asset digest:

`7d8981a3001893bb9da6fb47e095c0692c037a4faba895605ec3ae55a436c5b3`

The installed Waterline 2.0.1 registration and the real `%LOCALAPPDATA%\Waterline` profile were treated as out of scope and left unchanged. For an isolated, repeatable rehearsal, the exact `v2.0.1` source was archived and packaged with a dedicated rehearsal AppId. The 2.1.0 candidate was packaged with that same rehearsal identity.

The automated rehearsal verified:

- a clean 2.0.1 per-user install;
- an in-place upgrade from 2.0.1 to 2.1.0;
- the installed executable version changing from 2.0.1 to 2.1.0;
- the source state and installer rollback copy remaining byte-identical;
- uninstall removing application files while retaining the profile and rollback copy.

The rehearsal script refuses to run if its dedicated AppId is already registered, operates only beneath its exact temporary evidence root, and attempts cleanup if a step fails.

## Data import and update integrity

The optional legacy importer copies an Electron Local Storage LevelDB directory to temporary storage and reads only that copy. It recognizes Waterline settings and dated hydration records, validates every imported value, produces stable entry identifiers for repeat-import deduplication, and does not alter the source database. A real LevelDB fixture test verifies settings, valid and invalid entries, idempotent merge behavior, and byte-identical source files.

Update discovery now requires the official repository, release page, version-matched installer name, HTTPS URL, and a well-formed GitHub `sha256:` asset digest. Downloads are written to a temporary file, flushed and closed, hashed with SHA-256, and deleted on mismatch, cancellation, or failure. The release workflow publishes a matching `.sha256` sidecar.

## Automated and packaging evidence

- 40 domain, persistence, migration, import, placement, updater-policy, and audio checks pass.
- 6 application-level release parsing, offline/error, verified-download, mismatch-cleanup, and cancellation checks pass.
- The Release solution build completes with 0 warnings and 0 errors after restoring from the local package cache. A separate online restore resolved all packages but reported NU1900 because the environment could not reach NuGet's vulnerability-data endpoint; CI keeps normal NuGet auditing enabled.
- The package verifier checks the application version, PE payload, bundled LevelDB license, installer naming and PE payload, and an isolated startup capture from the published executable.
- The final self-contained `Waterline.exe` is 162,592,683 bytes and reports file version 2.1.0.0.
- The final `Waterline-Setup-2.1.0.exe` is 49,639,061 bytes and is not Authenticode-signed.
- Final candidate installer SHA-256: `009b93dae21999b1cb192f709e1e50c4fca7de54dab821c9d6bceae9d3c6ef50`.

## Visual and accessibility evidence

Thirty deterministic isolated captures cover the first-launch dashboard, populated dashboard, History, goal complete, over-goal, Schedule rest/modified/invalid, Settings rest/modified, every update state, recovery and unrecoverable-data states, expanded and compact widgets, keyboard focus, high contrast, reduced motion, and modal validation/unsaved-change states. Snapshot mode uses a temporary state path, disables update checks, and disables legacy-profile discovery so captures neither depend on nor expose the machine's installed Waterline data.

Static accessibility review confirms explicit automation names for actionable controls, keyboard navigation and shortcuts, visible focus treatments, dialog focus containment/return, high-contrast resources, reduced-motion fallbacks, and tooltips for icon-only actions. The existing manual regression matrix still requires representative Windows 10 and Windows 11 hardware for screen-reader reading order, 100/150/200% scaling, mixed-DPI monitor placement, speaker/headphone listening, and integrated-graphics profiling.

## Release risk and approval gate

No signing certificate is configured, so the candidate installer is unsigned and may trigger Windows SmartScreen or an unknown-publisher warning. Repository/asset identity, HTTPS transport, GitHub-provided SHA-256 enforcement, and the published sidecar protect update integrity, but do not provide publisher identity.

The owner approved Phase 8 on September 9, 2026. No tag, GitHub release, or push is part of that approval. Creating or pushing `v2.1.0` remains blocked until the owner separately authorizes publication.
