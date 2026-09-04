# Waterline for Windows

Waterline is a native Windows hydration tracker built with .NET 8 and WPF. It does not use Electron, a browser, a local web server, WebView, or OpenAI Sites.

## Windows features

- Native WPF dashboard with quick 8 oz, 12 oz, 16 oz, and custom drink logging.
- Always-on-top mini widget that collapses into a small hydration orb.
- Windows system-tray behavior and reminder notifications.
- Configurable daily goal, work hours, weekdays, reminder interval, and sounds.
- Single-instance enforcement.
- Local data storage in `%LOCALAPPDATA%\Waterline\state.json`.
- Update checks and installer downloads from this repository's GitHub Releases.

No hydration history is uploaded. GitHub is used only for source control and application releases.

## Repository workflow

The `main` branch of [AayyKay/waterline](https://github.com/AayyKay/waterline) is the source of truth. Every push and pull request is compiled and tested by `.github/workflows/ci.yml`. Tagged versions are compiled into a self-contained x64 Windows application and packaged as an installer by `.github/workflows/release.yml`.

Before starting work:

```powershell
git pull --rebase origin main
```

After a verified change:

```powershell
git add -A
git commit -m "Describe the change"
git push origin main
```

Git cannot safely upload uncommitted edits automatically. Committing and pushing makes the repository and local checkout match without risking silent or partial source changes.

## Development

Requirements:

- Windows 10 or 11
- .NET 8 SDK

Build and test:

```powershell
dotnet build Waterline.csproj
dotnet run --project Waterline.Tests\Waterline.Tests.csproj
```

Run the app:

```powershell
dotnet run --project Waterline.csproj
```

Create the self-contained application:

```powershell
dotnet publish Waterline.csproj -c Release -r win-x64 --self-contained true -o publish
```

## Publishing

1. Update `<Version>` in `Waterline.csproj`.
2. Commit and push the version change.
3. Create and push a matching tag, such as `v2.0.0`.

The release workflow publishes `Waterline-Setup-<version>.exe` to GitHub Releases. Installed copies can discover and install that release from the Waterline settings panel.
