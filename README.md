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
- Optional read-only import of compatible data from an older Electron profile.

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
dotnet run --project Waterline.App.Tests\Waterline.App.Tests.csproj
```

Run the app:

```powershell
dotnet run --project Waterline.csproj
```

Create the self-contained application:

```powershell
dotnet publish Waterline.csproj -c Release -r win-x64 --self-contained true -o publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
```

## Publishing

1. Update `<Version>` in `Waterline.csproj`.
2. Commit and push the version change.
3. Create and push a matching tag, such as `v2.0.0`.

The release workflow publishes `Waterline-Setup-<version>.exe` and its SHA-256 sidecar to GitHub Releases. Installed copies accept only a version-matched installer from the official repository with a GitHub-provided SHA-256 digest, then verify the downloaded file before offering installation.

To sign the installer in the tag release workflow, the repository owner must add **both** of these GitHub Actions repository secrets under **Settings → Secrets and variables → Actions**:

- `WATERLINE_SIGNING_PFX_BASE64`: a single-line Base64 encoding of the raw binary PKCS#12 `.pfx`/`.p12` file. The file must contain the publisher's code-signing certificate, its private key, and any needed intermediate certificates. It is not a PEM certificate, a public `.cer` file, or Base64 text with `-----BEGIN` markers.
- `WATERLINE_SIGNING_PFX_PASSWORD`: the nonempty password protecting that same PKCS#12 file.

The certificate must be currently valid, have the Code Signing extended key usage (`1.3.6.1.5.5.7.3.3`), and chain to a Windows-trusted public certificate authority for the final signature check to pass on the GitHub Windows runner. On Windows, create the Base64 value from an existing PFX with `[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\path\publisher.pfx'))`; paste the resulting single line into the secret. Keep the PFX and password out of the repository.

When both secrets are absent, local and CI release-candidate builds remain unsigned, and the tag workflow can produce an unsigned installer. If only one secret is present, signing fails and the workflow stops before publication. With both configured, the workflow signs the generated installer with SHA-256 and a timestamp, verifies the final file's Authenticode signature, timestamp, and signing-certificate thumbprint, then computes the SHA-256 sidecar from that signed file. A signing or verification failure stops the release job before `gh release create`.

See [Third-party notices](THIRD-PARTY-NOTICES.md) for packaged dependency licenses.
