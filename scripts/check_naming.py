#!/usr/bin/env python3
"""Reject legacy product identifiers in source, paths and current documentation."""
from pathlib import Path
import re
import subprocess

root = Path(__file__).resolve().parents[1]
legacy = re.compile(r'wander' + r'connect|dating' + r'app', re.IGNORECASE)
paths = subprocess.check_output(
    ['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=root
).decode().split('\0')
failures = []
for relative in sorted(set(filter(None, paths))):
    path = root / relative
    if not path.is_file():  # A tracked old path can be deleted during a rename.
        continue
    if legacy.search(relative):
        failures.append(f'{relative}: legacy filename')
    try:
        content = path.read_text(encoding='utf-8')
    except UnicodeDecodeError:  # Historical screenshots/fonts are not editable identifiers.
        continue
    if relative == 'docs/delivery/SWOOVR-RENAME-2026-10-22.md':
        continue  # Explicit before/after mapping and compatibility commands.
    if relative.startswith('docs/'):
        # These are actual external resources and historical local locations, not names to invent.
        content = re.sub(r'https://github\.com/repasscloud/' + r'wander' + r'connect\b[^\s)<>`]*', '', content)
        content = re.sub(r'/Users/[^\s)`<>]+', '', content)
        # The actual host checkout directory is a real location, not a product identifier.
        content = re.sub(r'\b' + r'wander' + r'connect-brief\b', '', content)
    for number, line in enumerate(content.splitlines(), 1):
        if legacy.search(line):
            failures.append(f'{relative}:{number}: legacy identifier')
assert not failures, '\n'.join(failures)
assert (root / 'Swoovr.sln').is_file() and (root / 'Swoovr.Local.sln').is_file()
# Nine application projects: Web.Client was added for Interactive WebAssembly (Q03, D59) and Admin for the separate
# administration app (Q35, D136).
expected = {'Api', 'Web', 'Web.Client', 'Admin', 'Mobile', 'Contracts', 'Client', 'Domain', 'Infrastructure'}
actual = {p.parent.name.removeprefix('Swoovr.') for p in (root / 'src').glob('*/*.csproj')}
assert actual == expected, f'Unexpected application project set: {actual}'
# UI projects reach the server only through Client (docs/architecture/README.md): never Domain or Infrastructure.
allowed = {'Web': {'Client', 'Web.Client'}, 'Web.Client': {'Client'}, 'Mobile': {'Client'}, 'Admin': {'Client'}}
# The member app never carries admin screens (D136): Web and Web.Client must not reference Admin, nor Admin them.
for project in ['Web', 'Web.Client']:
    assert 'Swoovr.Admin' not in (root / 'src' / f'Swoovr.{project}' / f'Swoovr.{project}.csproj').read_text(), f'Swoovr.{project} references the admin app'
# The member BFF forwards member routes only, never admin ones.
assert 'admin' not in re.search(r'GeneratedRegex\(@"(.*?)"\)', (root / 'src/Swoovr.Web/Auth/Bff.cs').read_text(), re.S).group(1)
for project, permitted in allowed.items():
    text = (root / 'src' / f'Swoovr.{project}' / f'Swoovr.{project}.csproj').read_text()
    references = set(re.findall(r'Include="\.\.\\Swoovr\.([A-Za-z.]+)\\', text))
    assert references <= permitted, f'Swoovr.{project} references {sorted(references - permitted)}'
print('PASS: Swoovr source/path naming, documented historical exceptions, nine application projects, UI project references and no admin routes in the member app.')
