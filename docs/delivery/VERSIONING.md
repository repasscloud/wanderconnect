# Swoovr versioning

CONFIRMED Q129 / D320 (owner, 4 November 2026, [#189](https://github.com/repasscloud/wanderconnect/issues/189)).

## One product version: `Major.Minor.Build`

| Part | Meaning | How it changes |
| --- | --- | --- |
| Major (X) | Significant platform changes, major redesigns | `scripts/release_version.py bump major`: Minor and Build reset to 0 |
| Minor (Y) | New functionality, substantial enhancements | `scripts/release_version.py bump minor`: Build resets to 0 |
| Build (Z) | Fixes, security, performance, UI, routine releases | Allocated automatically for each production release |

The only definition is `Directory.Build.props` (`SwoovrVersionMajor`, `SwoovrVersionMinor`, and the default `SwoovrVersionBuild` for untagged builds). Every .NET project compiles with it. `Swoovr.Contracts.AppVersion` reads it back from the compiled assembly:

- `ProductVersion`: `1.4.28`
- `DisplayVersion`: `Version 1.4.28`. Public footers and app screens; never the commit.
- `DiagnosticVersion`: `1.4.28+<commit>`. Internal diagnostics only.

Where it shows:

- **Footers:** every web page (public, signed-in, invite-only), every admin page, and Keycloak sign-in and registration.
- **Mobile:** the app's welcome screen and Me area.
- **API:** `GET /version` (unauthenticated) returns `{ "version", "major", "minor", "build" }`.

None of these need a network call to know their own version.

Separate things that are **not** the product version:

- the native store build number (`SwoovrNativeBuild` → MAUI `ApplicationVersion`, monotonically increasing for Apple/Google);
- the API contract date (`Swoovr-Version` header);
- deployment ids;
- database migrations.

## Releasing

```sh
python3 scripts/release_version.py next            # what the next production release would be
python3 scripts/release_version.py allocate        # tag HEAD vX.Y.Z (refuses an existing tag)
git push --atomic origin vX.Y.Z                    # the remote refuses a duplicate: no two releases share a version
dotnet publish ... $(python3 scripts/release_version.py msbuild-args) -p:SwoovrNativeBuild=<store build>
docker build --build-arg SWOOVR_VERSION=$(python3 scripts/release_version.py for-head) -f containers/uat/Keycloak.Dockerfile .
```

- **Build numbering:** the first release on a new Major.Minor is `X.Y.0`; each later production release takes the highest tagged Build + 1.
- **Untagged builds:** development and UAT builds from untagged commits report the committed version and never advance the sequence.
- **Rebuilding a release:** rebuilding or redeploying a tagged commit (`for-head`) keeps its version, and an artifact keeps the version it was compiled with.
- **Bumping Major or Minor:** `bump` also updates the Keycloak theme's `swoovrVersion` and resets Build. `scripts/check_version.py` fails if anything drifts or hard-codes a version.

GitHub Actions stays disabled (AGENTS.md). [.github/workflow-templates/release.yml](../../.github/workflow-templates/release.yml) holds the fully commented release steps for when the owner enables minutes.
