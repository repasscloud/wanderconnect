# ClamAV image supply-chain record (D336, #221)

Retained release evidence for the owner-approved scanner image (#221 §8). GitHub Actions minutes are off, so these were generated locally and committed in place of build artifacts. Regenerate them whenever the pinned digest changes.

| Item | Value |
| --- | --- |
| Image (index digest, pinned in `compose.local.yaml`) | `clamav/clamav@sha256:c21aa18cc0a358678255a3dfb343a4555b58f71b97d7b869df8fdf7ea0a5b4b1` |
| Source tag | `clamav/clamav:stable-debian13-slim` |
| Engine (from `clamd --version`) | ClamAV 1.5.4 |
| Platform manifests | linux/amd64 `sha256:6a5eb703fc04…`, linux/arm64 `sha256:9118103d533f…`, linux/ppc64le, plus attestation manifests ([`index-manifest.json`](index-manifest.json)) |
| Generated | 6 November 2026, syft (SBOMs), grype (vulnerabilities), docker buildx imagetools (manifest, provenance) |

## Files

- [`index-manifest.json`](index-manifest.json): the registry's OCI image index for the pinned digest.
- [`provenance-slsa.json`](provenance-slsa.json): the publisher's SLSA build provenance, attached to the image as attestations. The publisher's sigstore signatures weren't checked here because cosign isn't installed (gap below).
- `sbom-amd64.cdx.json`, `sbom-arm64.cdx.json`: CycloneDX SBOMs.
- `sbom-amd64.spdx.json`, `sbom-arm64.spdx.json`: SPDX SBOMs.
- [`licences-amd64.json`](licences-amd64.json): licence inventory for the 112 detected packages, grouped by licence. None are unknown.
- `vulnerabilities-amd64.json`, `vulnerabilities-arm64.json`: grype known-vulnerability matches.

## Findings needing review

**Not in the SBOMs.** ClamAV is built from source in this image, so the SBOM tools don't detect it. Recorded here by hand:
- **ClamAV 1.5.4:** GPL-2.0. Commercial use is permitted. The image is used unmodified as an internal service, and nothing is redistributed.
- **UnRAR library** (`/usr/lib/libclamunrar.so.12`, `libclamunrar_iface`): licensed under the separate UnRAR licence, not the GPL. It may be used and redistributed unmodified, but not used to re-create the RAR compression algorithm. **#221 asks for this to be reviewed.** It's raised as Q138 ([#227](https://github.com/repasscloud/wanderconnect/issues/227)).

**Known vulnerabilities in the Debian 13 base packages, same on both platforms:**

| Severity | Count |
| --- | --- |
| Critical | 9 |
| High | 101 |
| Medium | 74 |
| Low | 12 |
| Negligible | 90 |

- The Critical matches are in `libcurl4t64` 8.14.1-2+deb13u7 (8) and `libxml2` 2.12.7+dfsg+really2.9.14-2.1+deb13u3 (1). Debian marks every one of them `wont-fix`, so no fixed package exists for this base.
- clamd uses libcurl only through freshclam, to fetch signature updates from the ClamAV mirror over HTTPS. The scanning port is never published.
- Whether to accept this until a newer pinned digest is reviewed is an owner decision: Q138 ([#227](https://github.com/repasscloud/wanderconnect/issues/227)).

**Signatures.** The publisher's image signatures weren't verified, because cosign isn't installed on this machine. Run `cosign verify` (or `docker trust inspect`) for this digest before production.

## Runtime configuration (unchanged image)

Limits are set only through the image's own `CLAMD_CONF_*` variables:
- `StreamMaxLength 160M`, `MaxFileSize 160M` and `MaxScanSize 400M`. These cover the largest allowed file (video, 100 MB). Anything over a limit is reported as unsupported and never cleared (`AlertExceedsMax yes`).
- `ConcurrentDatabaseReload no`.

freshclam inside the container updates the signature database automatically, into a persistent volume. Memory: allow 4 GB per scanner. An 8 GB Docker VM is tight for the whole local stack.
