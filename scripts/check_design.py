#!/usr/bin/env python3
"""Check/generate palette copies and verify the specified solid text pairs."""
import argparse
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--write', action='store_true')
parser.add_argument('--check', action='store_true')
args = parser.parse_args()
data = json.loads((root / 'docs/design/themes.json').read_text())
assert [p['id'] for p in data] == ['electric', 'raspberry', 'cobalt', 'coral', 'orchid', 'solar']
keys = ['bg', 'surface', 'text', 'muted', 'primary', 'onPrimary', 'accent', 'soft', 'softText', 'border']
pairs = [('text','bg'), ('text','surface'), ('muted','bg'), ('muted','surface'), ('onPrimary','primary'), ('softText','soft')]
def luminance(value):
    values = [int(value[i:i+2],16)/255 for i in (1,3,5)]
    linear = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in values]
    return sum(a*b for a,b in zip(linear,[.2126,.7152,.0722]))
count = 0
for p in data:
    for mode in ['light','dark']:
        t = p[mode]
        assert set(t) == set(keys), (p['id'], mode, 'token keys')
        assert all(re.fullmatch(r'#[0-9A-F]{6}',v) for v in t.values())
        for fg,bg in pairs:
            a,b = sorted([luminance(t[fg]), luminance(t[bg])])
            ratio = (b+.05)/(a+.05)
            assert ratio >= 4.5, (p['id'],mode,fg,bg,ratio)
            count += 1
html = root / 'docs/design/wanderconnect-colour-studio.html'
original = html.read_text()
updated = re.sub(r'const palettes=.*?;\nlet selected', lambda _: 'const palettes='+json.dumps(data,separators=(',',':'))+';\nlet selected',original,flags=re.S)
if args.write: html.write_text(updated)
else: assert original == updated, 'Studio tokens drifted; run --write'
doc = root / 'docs/design/USER-APPEARANCE-THEMES.md'
original = doc.read_text(); updated = original
for p in data:
    heading = f"#### {p['name']} — `{p['id']}`"
    start = updated.index(heading)
    table_start = updated.index('| Token | Light | Dark |',start)
    table_end = updated.index('\n\n',table_start)
    table = '| Token | Light | Dark |\n| --- | --- | --- |\n'+'\n'.join(f"| {k} | `{p['light'][k]}` | `{p['dark'][k]}` |" for k in keys)
    updated = updated[:table_start]+table+updated[table_end:]
if args.write: doc.write_text(updated)
else: assert original == updated, 'Specification tokens drifted; run --write'
# Reject missing relative Markdown destinations without opening remote links.
for path in [root/'README.md', root/'AGENTS.md', *root.glob('docs/**/*.md')]:
    for target in re.findall(r'\]\(([^\s)]+)\)',path.read_text()):
        if '://' in target or target.startswith('#'): continue
        assert (path.parent / target.split('#')[0]).exists(), (str(path),target)
for path in [*(root/'.github/workflows').glob('*'), *(root/'.github/workflow-templates').glob('*')]:
    assert all(not line.strip() or line.lstrip().startswith('#') for line in path.read_text().splitlines()), f'Active workflow: {path}'
print(f'PASS: {count} contrast pairs, canonical token copies, local Markdown links and disabled workflows.')
