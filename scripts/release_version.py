#!/usr/bin/env python3
"""Q129 / D320: Swoovr product version releases (Major.Minor.Build).

Directory.Build.props holds Major and Minor (changed only by an intentional `bump`) and the default Build used by untagged
development/UAT builds. Production releases get the next Build from git tags `vX.Y.Z` on the current Major.Minor:

  show                 committed version (untagged builds report this)
  next                 the next production version (X.Y.<highest tagged + 1>, or X.Y.0 after a Major/Minor change)
  allocate             create the annotated tag for `next` on HEAD; refuses if the tag exists (push it with --atomic so
                       two concurrent releases can never share a version)
  for-head             the version HEAD was released as (its vX.Y.Z tag), else the committed version: rebuilding or
                       redeploying a release keeps its version
  msbuild-args [X.Y.Z] MSBuild properties for that version (default: for-head) plus the commit for diagnostics
  bump major|minor     intentional release decision: Major+1 (Minor, Build -> 0) or Minor+1 (Build -> 0)
"""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROPS = ROOT / "Directory.Build.props"
TAG = re.compile(r"^v(\d+)\.(\d+)\.(\d+)$")


def git(*args, check=True):
    return subprocess.run(["git", "-C", str(ROOT), *args], check=check, capture_output=True, text=True).stdout.strip()


def props():
    text = PROPS.read_text()
    value = lambda name: int(re.search(rf"<{name}(?: [^>]*)?>(\d+)</{name}>", text).group(1))
    return value("SwoovrVersionMajor"), value("SwoovrVersionMinor"), value("SwoovrVersionBuild")


def committed():
    major, minor, build = props()
    return f"{major}.{minor}.{build}"


def released_builds(major, minor):
    builds = []
    for tag in git("tag", "--list", f"v{major}.{minor}.*").splitlines():
        match = TAG.match(tag.strip())
        if match and int(match.group(1)) == major and int(match.group(2)) == minor:
            builds.append(int(match.group(3)))
    return builds


def next_version():
    major, minor, _ = props()
    builds = released_builds(major, minor)
    return f"{major}.{minor}.{max(builds) + 1 if builds else 0}"


def for_head():
    for tag in git("tag", "--points-at", "HEAD").splitlines():
        if TAG.match(tag.strip()):
            return tag.strip()[1:]
    return committed()


def msbuild_args(version):
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", version)
    major, minor, _ = props()
    if not match or (int(match.group(1)), int(match.group(2))) != (major, minor):
        sys.exit(f"{version} does not match Directory.Build.props {major}.{minor}.x")
    sha = git("rev-parse", "HEAD", check=False) or "unknown"
    return f"-p:SwoovrVersionBuild={match.group(3)} -p:SourceRevisionId={sha}"


def bump(part):
    text = PROPS.read_text()
    major, minor, _ = props()
    major, minor = (major + 1, 0) if part == "major" else (major, minor + 1)
    text = re.sub(r"<SwoovrVersionMajor>\d+</SwoovrVersionMajor>", f"<SwoovrVersionMajor>{major}</SwoovrVersionMajor>", text)
    text = re.sub(r"<SwoovrVersionMinor>\d+</SwoovrVersionMinor>", f"<SwoovrVersionMinor>{minor}</SwoovrVersionMinor>", text)
    text = re.sub(r"(<SwoovrVersionBuild Condition=\"[^\"]*\">)\d+(</SwoovrVersionBuild>)", r"\g<1>0\g<2>", text)
    PROPS.write_text(text)
    theme = ROOT / "containers/keycloak/themes/swoovr/login/theme.properties"
    theme.write_text(re.sub(r"(?m)^swoovrVersion=.*$", f"swoovrVersion={major}.{minor}.0", theme.read_text()))
    return f"{major}.{minor}.0"


def main(argv):
    command = argv[1] if len(argv) > 1 else "show"
    if command == "show": print(committed())
    elif command == "next": print(next_version())
    elif command == "for-head": print(for_head())
    elif command == "msbuild-args": print(msbuild_args(argv[2] if len(argv) > 2 else for_head()))
    elif command == "allocate":
        version = next_version()
        if git("tag", "--list", f"v{version}"):
            sys.exit(f"v{version} already exists")
        git("tag", "-a", f"v{version}", "-m", f"Swoovr {version}")
        print(version)
    elif command == "bump" and len(argv) > 2 and argv[2] in ("major", "minor"): print(bump(argv[2]))
    else: sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv)
