#!/usr/bin/env python3
"""Q129 / D320 (and issue 177): Swoovr has one product version, Major.Minor.Build, defined once in Directory.Build.props.
Every project, the Keycloak theme and the mobile package consume it; no member-facing source hard-codes a version or
carries a development label; the native store build number stays separate."""
import re
import sys
from pathlib import Path

root = Path(__file__).resolve().parent.parent
props = (root / "Directory.Build.props").read_text()
problems = []
part = lambda name: re.search(rf"<{name}(?: [^>]*)?>(\d+)</{name}>", props)
major, minor, build = (part(n) for n in ("SwoovrVersionMajor", "SwoovrVersionMinor", "SwoovrVersionBuild"))
if not (major and minor and build):
    problems.append("Directory.Build.props must define SwoovrVersionMajor, SwoovrVersionMinor and SwoovrVersionBuild")
    version = "?"
else:
    version = f"{major.group(1)}.{minor.group(1)}.{build.group(1)}"
for required in ("<SwoovrVersion>$(SwoovrVersionMajor).$(SwoovrVersionMinor).$(SwoovrVersionBuild)</SwoovrVersion>",
                 "<Version>$(SwoovrVersion)</Version>", "<SwoovrVersionBuild Condition=\"'$(SwoovrVersionBuild)' == ''\">",
                 "<SwoovrNativeBuild Condition=\"'$(SwoovrNativeBuild)' == ''\">"):
    if required not in props:
        problems.append(f"Directory.Build.props must contain {required}")
theme = (root / "containers/keycloak/themes/swoovr/login/theme.properties").read_text()
if f"\nswoovrVersion={version}\n" not in "\n" + theme + "\n":
    problems.append(f"Keycloak theme swoovrVersion must be {version} (scripts/release_version.py bump keeps it in step)")
dockerfile = (root / "containers/uat/Keycloak.Dockerfile").read_text()
if "ARG SWOOVR_VERSION" not in dockerfile or re.search(r"swoovrVersion=\d", dockerfile):
    problems.append("UAT Keycloak image may set swoovrVersion only from ARG SWOOVR_VERSION")
mobile = (root / "src/Swoovr.Mobile/Swoovr.Mobile.csproj").read_text()
if "<ApplicationDisplayVersion>$(SwoovrVersion)</ApplicationDisplayVersion>" not in mobile:
    problems.append("Mobile ApplicationDisplayVersion must be $(SwoovrVersion)")
if "<ApplicationVersion>$(SwoovrNativeBuild)</ApplicationVersion>" not in mobile:
    problems.append("Mobile ApplicationVersion (store build) must be $(SwoovrNativeBuild)")
if "/version" not in (root / "src/Swoovr.Api/Program.cs").read_text():
    problems.append("API must map GET /version")
# Displays go through Swoovr.Contracts.AppVersion; no other source parses assembly versions or hard-codes one.
sources = [p for p in [*root.glob("src/**/*.cs"), *root.glob("src/**/*.razor"), *root.glob("src/**/*.xaml")] if not {"bin", "obj", "Migrations"} & set(p.parts)]
for path in sources:
    text = path.read_text(errors="ignore")
    if path.name != "AppVersion.cs" and "AssemblyInformationalVersionAttribute" in text:
        problems.append(f"{path.relative_to(root)} reads the assembly version itself; use AppVersion")
    if path.name != "AppVersion.cs" and re.search(r"\"Version \d+\.\d+", text):
        problems.append(f"{path.relative_to(root)} hard-codes a product version")
for layout in ("src/Swoovr.Web/Components/Layout/MainLayout.razor", "src/Swoovr.Admin/Components/Layout/AdminLayout.razor"):
    if "AppVersion.Current.DisplayVersion" not in (root / layout).read_text():
        problems.append(f"{layout} must show AppVersion.Current.DisplayVersion")
if "pageId == \"register\"" in (root / "containers/keycloak/themes/swoovr/login/footer.ftl").read_text():
    problems.append("Keycloak footer must show the version on registration too (Q129 B)")
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
    print("FAIL: version check\n  " + "\n  ".join(problems))
    sys.exit(1)
print(f"PASS: single Swoovr version {version} (Major.Minor.Build; assemblies, API /version, web/admin/Keycloak footers, mobile display + separate store build); no development wording.")
