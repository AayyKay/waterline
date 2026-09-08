# Phase 2 data and domain foundation

Status: approved

Branch: `codex/data-domain-foundation`

## Implemented boundary

The solution now separates deterministic product rules from WPF and storage code:

- `Waterline.Core` owns hydration totals, local-day grouping, pace, reminders, validation, state models, and the clock contract.
- `Waterline.Infrastructure` owns versioned JSON loading, migration, validation, atomic replacement, backup recovery, quarantine, and legacy-location discovery.
- `Waterline` remains the WPF composition and presentation project while the view layer is rebuilt in later phases.
- `Waterline.Tests` is a platform-neutral executable test harness for the Phase 2 rules and storage contract.

`Waterline.sln` is the build entry point for all four projects. Moving the remaining WPF files under `src/Waterline.App` is deferred until that move can be made without mixing it into the data milestone.

## Data preservation contract

The canonical installed path remains `%LOCALAPPDATA%\Waterline\state.json`. Loading never creates a directory or rewrites source data. The current native `v2.0.1` document is treated as schema zero and migrates in memory to schema one:

- goal, schedule, reminder days, sound preference, drink amounts, timestamps, and last-notification time are retained;
- numeric legacy drink identifiers become stable schema-one string identifiers;
- malformed entries are skipped with a visible migration message;
- unknown JSON properties remain tolerated;
- the source remains byte-for-byte unchanged until a successful user-initiated state save.

Every save validates the full document, writes a unique same-directory temporary file, flushes it, and replaces the canonical file while retaining the prior valid document as `state.backup.json`.

If the primary file is corrupt and the backup is valid, Waterline loads the backup but blocks writes. A recovery acknowledgement preserves the bad primary as `state.corrupt.<timestamp>.json` before saving can resume. If neither file is valid, saving stays blocked; this state cannot be cleared through the backup acknowledgement path.

## Time and calculation rules

Calculations receive an explicit instant and timezone. Drink timestamps keep their original offsets, while daily totals use the supplied current timezone. The rules cover:

- zero-intake days without fabricated chart bars;
- goal progress and bounded remaining volume;
- before-schedule, active, behind, on-pace, ahead, after-schedule, excluded-day, and complete states;
- reminder start, drink reset, notification reset, next selected day, goal completion, and invalid schedules;
- deterministic handling of invalid and ambiguous local times around daylight-saving transitions.

The running view model uses an injectable clock. Snapshot mode uses an isolated temporary state path and disables update checks, so captures cannot read or modify the developer's real profile or depend on GitHub.

## Legacy Electron discovery evidence

The repository's `v1.0.1` tag identifies Electron Builder `productName` as `Waterline`, which maps the Windows Chromium profile to `%APPDATA%\Waterline`. The historical application stored these Local Storage keys:

- `waterline-settings`;
- `waterline-drinks-YYYY-MM-DD` for each local day.

Chromium Local Storage is therefore discovered read-only at `%APPDATA%\Waterline\Local Storage\leveldb`. Discovery performs no write and reports whether the directory exists.

Reading LevelDB records and merging Electron data are intentionally outside this candidate. A production importer still requires packaged-version fixtures for `v1.0.0` and `v1.0.1`, repeat-import tests, deduplication, and a reviewed LevelDB reader. No new dependency has been introduced.

## Verification evidence

The Phase 2 harness uses temporary directories and a fixed timezone. It does not access the installed Waterline profile. Its cases cover amount validation, timezone partitioning, empty history, pace states, reminder scheduling, schema validation, first-run loading, schema-zero migration, schema-one reload, atomic backup, backup recovery, unrecoverable-state protection, and read-only Electron discovery.

Required approval evidence:

1. `dotnet build Waterline.sln --configuration Release`
2. `dotnet run --project Waterline.Tests\Waterline.Tests.csproj --configuration Release`
3. clean `git diff --check`

Approval verification completed with a warning-free Release solution build, all 24 foundation tests passing, and a clean `git diff --check`. The resulting commit and push status are reported with the approved milestone.
