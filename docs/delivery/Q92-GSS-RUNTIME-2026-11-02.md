# Q92 — missing GSS runtime library

Investigation and local source change, 2 November 2026. Tracking: [Q92](QUESTIONS.md), [#132](https://github.com/repasscloud/wanderconnect/issues/132).

CONFIRMED scope: remove the native missing-library diagnostic without filtering stderr, weakening authentication, changing image pins or changing the non-root serving user. DEFAULT implementation: install Ubuntu's `libgssapi-krb5-2` in both shared runtime stages. This supplies the actual missing SONAME and leaves Npgsql's configured negotiation behavior intact.

## Native consumer and owning images

Read-only disposable containers showed:

| Existing image | Platform | Finding |
| --- | --- | --- |
| `swoovr-platform-api:latest`, `sha256:7530cab85dd881c61b1752c9b796d28aa9f09bf1533a14ce9ff9135c806b2d85` | Linux ARM64; Ubuntu 24.04.5; .NET 10.0.13 | Package absent; native consumer present |
| `swoovr-uat-api:phase-zero-check`, `sha256:6b246043444ef712fd2c00c9ce5f461377af66f0d8e092e8f6010a1ee4b35f1c` | Linux AMD64; .NET 10.0.13 | Package absent; native consumer present |
| `swoovr-uat-operator:phase-zero-check` | Linux AMD64 | Package already present through PostgreSQL client dependencies |

The native consumer is `/usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.13/libSystem.Net.Security.Native.so`. Searching the binaries found both `libgssapi_krb5.so.2` and the exact `Cannot load library` diagnostic in this library. The [.NET native implementation](https://github.com/dotnet/runtime/blob/v10.0.1/src/native/libs/System.Net.Security.Native/pal_gssapi.c) documents the `dlopen` and stderr path; the observed binary supplies evidence for the installed patch version. [Npgsql 10.0.3](https://github.com/npgsql/npgsql/blob/v10.0.3/src/Npgsql/Internal/NpgsqlConnector.cs) negotiates GSS through its integrated security handler before falling back when GSS is preferred.

`compose.local.yaml` builds `containers/uat/Dockerfile`; `compose.yaml` builds `containers/Dockerfile`. Both use the same immutable ASP.NET runtime pin, so both runtime stages receive the package. No project, issuer/session policy, connection string or authentication mechanism changes.

The operator image's `dpkg-query -S /usr/lib/x86_64-linux-gnu/libgssapi_krb5.so.2` identified `libgssapi-krb5-2:amd64` version `1.20.1-6ubuntu2.10`; `ldd` resolved all dependencies, and `ctypes.CDLL("libgssapi_krb5.so.2")` passed. This establishes library availability, not a live Kerberos integration.

## Verification and remaining acceptance

`scripts/check-gss-runtime.sh IMAGE` starts a disposable read-only container with no network, capabilities or mounted volumes. It requires the installed package, resolves its SONAME and checks native dependencies. It failed against the existing platform API image with `no packages found matching libgssapi-krb5-2`, and passed against the existing operator image. `bash -n scripts/check-gss-runtime.sh` passed. No services were restarted or databases changed during this investigation.

The edited runtime stages have **not yet been built or exercised**. To verify the new runtime layers without building application publish stages or replacing service images:

```sh
docker build --target runtime -f containers/Dockerfile -t swoovr-q92-local-runtime:check .
bash scripts/check-gss-runtime.sh swoovr-q92-local-runtime:check
docker build --target runtime -f containers/uat/Dockerfile -t swoovr-q92-uat-runtime:check .
bash scripts/check-gss-runtime.sh swoovr-q92-uat-runtime:check
./scripts/check.sh
```

OPEN acceptance: actual fresh/reuse launcher startup, migrations, GeoNames import, beta enrolment, root-admin grant, real password+OTP admin smoke and an intentional command failure must still be exercised against rebuilt application images in an isolated sandbox. Do not run fresh/reset or replace the owner's currently serving stack to collect this evidence. Confirm absence of the diagnostic while preserving nonzero failure propagation. Native library resolution alone does not close this broader acceptance gap. AMD64 application images and any intended hosted platform require their own validation; no deployment or live Kerberos claim is made.

Fresh runtime targets built successfully from the updated UAT Dockerfile for both linux/arm64 and linux/amd64, using task-owned tags `swoovr-q92-runtime:arm64` and `:amd64`. The disposable no-network/read-only library check passed for both: package present, SONAME readable, all ldd dependencies resolved. No running container, database, volume or host was replaced. Docker context denies docs/private data by default.

Pre-commit `check.sh` q92-check passed naming, backups, design/link/disabled-workflow, realm, Node, full Release build (zero warnings/errors) and portable suites; PostgreSQL migration then refused the in-progress Q114/Q115 model/snapshot (`PendingModelChangesWarning`). Persistence and later HTTP smoke are not claimed passed. Full integrated gate and fresh/reuse operator/Admin acceptance remain pending; this is runtime dependency verification, not live Kerberos authentication or deployment.
