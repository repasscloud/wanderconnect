#!/usr/bin/env python3
"""issue 177: Swoovr has one version. Directory.Build.props <SwoovrVersion> is the source; the Keycloak theme, the mobile
app and the web/API assemblies must show exactly that, and no member-facing source may carry a development label."""
import re
import sys
from pathlib import Path

root = Path(__file__).resolve().parent.parent
version = re.search(r"<SwoovrVersion>([^<]+)</SwoovrVersion>", (root / "Directory.Build.props").read_text()).group(1)
problems = []
if not re.fullmatch(r"\d+\.\d+\.\d+", version):
    problems.append(f"SwoovrVersion {version!r} is not MAJOR.MINOR.PATCH")
if "<Version>$(SwoovrVersion)</Version>" not in (root / "Directory.Build.props").read_text():
    problems.append("Directory.Build.props must set <Version>$(SwoovrVersion)</Version>")
theme = (root / "containers/keycloak/themes/swoovr/login/theme.properties").read_text()
if f"\nswoovrVersion={version}\n" not in "\n" + theme + "\n":
    problems.append(f"Keycloak theme swoovrVersion must be {version}")
if "swoovrVersion" in (root / "containers/uat/Keycloak.Dockerfile").read_text():
    problems.append("UAT Keycloak image must not override swoovrVersion")
if "<ApplicationDisplayVersion>$(SwoovrVersion)</ApplicationDisplayVersion>" not in (root / "src/Swoovr.Mobile/Swoovr.Mobile.csproj").read_text():
    problems.append("Mobile ApplicationDisplayVersion must be $(SwoovrVersion)")
# No development framing on member-facing surfaces (docs/design is a read-only reference and is excluded).
surfaces = [*root.glob("containers/keycloak/themes/swoovr/**/*.ftl"), *root.glob("containers/keycloak/themes/swoovr/**/*.properties"),
            *root.glob("src/Swoovr.Web/Components/**/*.razor"), *root.glob("src/Swoovr.Web.Client/**/*.razor"),
            *root.glob("src/Swoovr.Mobile/**/*.xaml"), *root.glob("src/Swoovr.Web/wwwroot/launch/*.html")]
for path in surfaces:
    text = path.read_text(errors="ignore")
    for phrase in ("In development", "in development", "-dev\n", "Starting in Adelaide"):
        if phrase in text:
            problems.append(f"{path.relative_to(root)} contains {phrase.strip()!r}")
if problems:
    print("FAIL: version/dev-wording check\n  " + "\n  ".join(problems))
    sys.exit(1)
print(f"PASS: single Swoovr version {version} (assemblies, mobile, Keycloak theme); no development wording on member surfaces.")
