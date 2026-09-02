# Waterline for Windows

Waterline is a Windows hydration tracker with a full dashboard, an always-on-top desktop widget, a system tray, and native scheduled notifications.

## Development

Run the interface and desktop shell in separate terminals:

```powershell
pnpm dev
pnpm desktop:dev
```

## Build the installer

```powershell
pnpm desktop:build
```

The Windows installer is written to `release`.

## Publish an auto-update release

Waterline checks GitHub Releases shortly after startup and every four hours. A release-built app downloads updates in the background, reports progress in Settings, and offers to restart when the update is ready. Downloaded updates also install on a normal app quit.

1. Create a public GitHub repository and push this project to it.
2. Increase `version` in `package.json`, for example from `1.0.0` to `1.0.1`.
3. Commit the version change, create the matching tag, and push it:

```powershell
git tag v1.0.1
git push origin main --tags
```

The workflow in `.github/workflows/release.yml` builds the Windows installer and publishes the release assets and update metadata. The tag must match the `package.json` version. Publish the GitHub Release if the workflow creates it as a draft.

Local development builds intentionally report that update checks are unavailable. Installers built by the GitHub release workflow contain the GitHub update feed automatically.
