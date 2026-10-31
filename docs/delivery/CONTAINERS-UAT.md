# UAT container images

31 October2026. D282 DEFAULT implements D221’s reversible Container Apps preparation; Q90/[issue124](https://github.com/repasscloud/wanderconnect/issues/124) remains OPEN. Images are built locally only. No registry push, Azure resource, deployment, merge or Actions activation.

## Build

Use the repository root, Docker/BuildKit and access to the pinned registries/NuGet. Azure Container Apps requires **linux/amd64**. The .NET build stage runs on BUILDPLATFORM and publishes the explicit TARGETARCH using Microsoft’s supported cross-publish pattern; runtime images match the selected target. Keycloak’s pinned Java21 build works on the target platform.

```sh
cd /Users/danijeljw/Developer/wanderconnect-brief/.claude/worktrees/phase-zero-uat
docker build --platform linux/amd64 -f containers/uat/Dockerfile --target api -t swoovr-uat-api:phase-zero-check .
docker build --platform linux/amd64 -f containers/uat/Dockerfile --target web -t swoovr-uat-web:phase-zero-check .
docker build --platform linux/amd64 -f containers/uat/Dockerfile --target admin -t swoovr-uat-admin:phase-zero-check .
docker build --platform linux/amd64 -f containers/uat/Keycloak.Dockerfile --target keycloak -t swoovr-uat-keycloak:phase-zero-check .
```

No secrets are build arguments. Deny-by-default `.dockerignore` allows the server/shared source, linked Hosting source, existing Checks source only for the optional validation target, public theme/provider code and guarded Keycloak entrypoint. It excludes mobile, bin/obj, development/environment appsettings, launch profiles, realm files, `.local`, env files and common private key/database/archive formats. Public baseline appsettings remains at `/app`. Source changes still require secret review; filename exclusions alone cannot establish absence of private material.

## Runtime boundaries

Api/Web/Admin contain complete framework-dependent Release publishes, with pinned SDK10.0.400 and ASP.NET10 base digests. Web includes Web.Client WASM, endpoint manifests and static assets. Working/content root is `/app`, UID1654, port8080, default environment Production (deployment may explicitly select Uat). Hosted guards reject unsafe/incomplete configuration. No automatic migration runs on serving startup. Api preserves the existing one-shot command dispatcher.

Keycloak uses exact26.8.1 and pinned Temurin21, UID1000, selected HTTP email SPI, canonical theme assets and image-only UAT policy links/channel. There are no imported realms/users/bootstrap credentials/database/hostname/TLS values baked into the image. Management health/metrics9000 must stay private. [Email](../architecture/KEYCLOAK-EMAIL.md) and [disabled realm/import boundary](KEYCLOAK-UAT.md) describe configuration and unverified provider/browser gates. An internal SPI/JPA deprecation warning remains; pinning is not upgrade compatibility evidence.

The current BFF ticket store and SignalR audience/publish state are in memory. DEFAULT UAT uses one replica per Api/Web/Admin, and coordinated maintenance for rollout/rollback; restarting portals invalidates local BFF sessions and requires sign-in again. Do not increase replica counts or promise transparent rolling sessions without distributed ticket/key/backplane work. Persistent Keycloak state/signing keys live in its PostgreSQL database. Database backups and fenced restore remain separate operator work.

## Local artifact checks

```sh
python3 tests/uat/app_images.py
python3 tests/uat/keycloak_image.py --image swoovr-uat-keycloak:phase-zero-check
# Optional validation image uses the existing Checks project, never a serving image.
docker build --platform linux/amd64 -f containers/uat/Dockerfile --target validation -t swoovr-uat-validation:phase-zero-check .
docker run --rm --platform linux/amd64 --network none swoovr-uat-validation:phase-zero-check
```

The application fixture runs uniquely named disposable PostgreSQL/app/probe containers on an internal-only network. Fake provider settings and generated temporary keys are private runtime files, never copied into an image. It checks architecture/nonroot/unsafe startup, actual migrations, empty-database purge/founders and missing-root-subject refusal; liveness, database readiness, portal503 for an unreachable fake API, SSR/static WASM/CSS and private logs. The proxy scheme is synthetic and there is no external egress: this does not prove real TLS/ingress/authentication/providers or a complete UAT member journey. Cleanup removes only owned containers including their anonymous volumes, network and private fixture. No MAIN hosts/DB/config are changed.

The validation target compares the native Skia library and processor assembly with the serving publish before running existing synthetic media decode/orientation/EXIF removal/resizing and quarantine checks on the same runtime base. This supplements platform builds; it does not verify live screening or R2. Actual commands/results/failures are in VALIDATION.md before slice completion. Passing liveness alone never means readiness or provider delivery.

## Remaining deployment preparation

Parent IaC must supply private PostgreSQL/application and Keycloak databases, separate least-privilege logins, managed identity/Key Vault references, registry pulls, public TLS/Cloudflare origins and exact trusted proxy network, readiness probes, private screening and backups. Dedicated service-client/bootstrap/root/migration/purge/founder jobs are explicit operator work; no command should blindly run at every restart. Current host images do not contain Python, pg_dump, age or Azure transport and are not backup operator images. [Backup/restore](BACKUP-RESTORE.md) requires the separate tooling/scheduler and quarantined paired restore/deletion reconciliation.

The owner’s existing image-moderation-api Dockerfile is a separate source/image. Its local image was inspected as arm64/~2.18GB; that is not an Azure artifact. Prepare an explicit amd64 build/registry reference, bearer API_KEY and both ARACHNID_API_USERNAME/PASSWORD via Key Vault, private ingress and actual readiness/engine checks. Swoovr must retain RequireKnownContentCheck=true; missing/failed known-content checks quarantine and never become a safety claim. No model/provider credential or external image/hash submission was performed here.

Release gates remain owner subscription/billing/hosting answer, provider/domain/DNS/Entra setup and credentials, APNs/FCM device evidence, legal57 and governance119 operations. Parent IaC/operator/deployment/base-URL smoke are still required software preparation; these image artifacts alone are not a ready-to-deploy claim.

## Primary references

- [Container Apps linux/amd64 requirement](https://learn.microsoft.com/azure/container-apps/containers#limitations).
- [Microsoft cross-platform TARGETARCH publishing](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-8/containers#container-images).
- [Non-root .NET container users and ports](https://learn.microsoft.com/dotnet/core/docker/introduction#building-container-images).
- [Keycloak optimized images/provider timestamps](https://www.keycloak.org/server/containers).

D286 also builds the separately owned image-moderation source for linux/amd64 as local swoovr-uat-screening:phase-zero-check. Its fixed existing Dockerfile copies application/models only; no source edit or real provider credential. Owned no-network runtime checks prove non-root packaging, missing-credential refusal and local model health under2CPU/4Gi. Arachnid ready means configured adapter, not authenticated Shield acceptance or a scan verdict. Source revision/build metadata and exact evidence are in VALIDATION.md; owner still reviews the separate source and real service acceptance before registry publication/admission.

PR125 D287 changes the Web import-map CSP source after the D282/D286 local image checks. Rebuild deployment images from the final reviewed commit using DEPLOYMENT-UAT.md; the old local phase-zero-check Web tag does not prove this review correction is packaged. This follow-up validates compiled Development/Uat hosts and real local browser flows, not a new published container or hosted deployment.
