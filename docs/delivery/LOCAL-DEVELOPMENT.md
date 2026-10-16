# Local development

Install .NET SDK matching global.json and Python 3. Run all commands from repository root.

```sh
./scripts/check.sh
dotnet run --project src/DatingApp.Api --no-launch-profile --urls http://localhost:5080
dotnet run --project src/DatingApp.Web --no-launch-profile --urls http://localhost:5081
```

Run API/web in separate terminals. `/health/live` is liveness only and does not certify database/provider readiness. `/api/v1/status` returns foundation metadata. Web is a static Blazor shell; there are no sign-in buttons claiming working authentication. Local HTTP is for loopback development; real authentication requires configured HTTPS callbacks and secure cookies.

`DatingApp.Local.sln` includes all non-mobile projects and executable checks. `DatingApp.sln` also includes MAUI. Do not run full-solution restore on a server-only environment and assume missing MAUI workloads are code defects. For mobile, install the supported MAUI Android/iOS workloads plus Android SDK/JDK and compatible Xcode; pin/record their versions before phase 1. Then:

```sh
dotnet build src/DatingApp.Mobile -f net10.0-android
dotnet build src/DatingApp.Mobile -f net10.0-ios
```

Simulator runtime/signing arguments depend on the local platform. Do not claim these builds passed unless actually run. No automatic heavyweight workload installation is part of local checks.

`./scripts/check.sh` also starts the built hosts briefly on loopback ports 5187/5188, verifies API JSON and web HTML, and stops them. Keep these ports free.

The current portable projects have no third-party NuGet packages; local restore uses SDK assets, disables NuGet audit network access, and tolerates unavailable sources. Future dependencies need an explicit cache/restore step and later security audit; offline checks do not replace dependency vulnerability review.

## Design and docs

Open docs/design/wanderconnect-colour-studio.html directly in a browser. It is self-contained. After editing themes.json, run `python3 scripts/check_design.py --write`, then `--check`. The generator preserves all other spec text and embedded portrait. Earlier colour-palettes.svg is historical, not a second theme authority.

## Optional Docker validation

No services or database are needed yet. To run the same portable checks using Docker (first image pull requires internet):

```sh
docker run --rm -v "$PWD:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0.400 bash -c 'dotnet restore DatingApp.Local.sln -p:NuGetAudit=false && dotnet build DatingApp.Local.sln --no-restore -c Release && dotnet run --project tests/DatingApp.Checks --no-build -c Release'
```

This checks .NET only; run the Python design check on the host. Linux containers cannot validate iOS/Xcode tooling. Add database/provider containers only when those integrations exist; do not call a detached unused container an integration test.
