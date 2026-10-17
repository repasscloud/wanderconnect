"""Flag-aware paints. Area balancing uses the filled bird, never its empty corners."""
import json, math, re
from functools import lru_cache
from pathlib import Path
from PIL import Image, ImageDraw
ROOT = Path(__file__).resolve().parents[1]

@lru_cache(maxsize=1)
def bird_samples():
    g = json.loads((ROOT/'source/geometry.json').read_text())['bird']
    tokens = re.findall(r'[MCZ]|-?\d+(?:\.\d+)?', g['path'])
    mask = Image.new('1', (192, 162)); draw = ImageDraw.Draw(mask)
    i = 0; polygon = []; current = (0, 0)
    while i < len(tokens):
        command = tokens[i]; i += 1
        if command == 'M':
            current = tuple(map(float, tokens[i:i+2])); i += 2; polygon = [current]
        elif command == 'C':
            c = list(map(float, tokens[i:i+6])); i += 6
            p0 = current; p1 = c[:2]; p2 = c[2:4]; p3 = c[4:]
            for step in range(1, 33):
                t = step/32; u = 1-t
                polygon.append(tuple(u**3*p0[k]+3*u*u*t*p1[k]+3*u*t*t*p2[k]+t**3*p3[k] for k in (0,1)))
            current = p3
        elif command == 'Z':
            draw.polygon([(x/2,y/2) for x,y in polygon], fill=1)
        else:
            raise ValueError('Bird sampling expects absolute M/C/Z geometry.')
    return [((x+.5)/192, (y+.5)/162) for y in range(162) for x in range(192) if mask.getpixel((x,y))]

def boundaries(values, angle, weights=None, balance='area', samples=None):
    weights = weights or [1]*len(values)
    if len(weights) != len(values) or any(w <= 0 for w in weights):
        raise ValueError('Each flag colour needs a positive weight.')
    cumulative = []; total = sum(weights); acc = 0
    for w in weights[:-1]:
        acc += w; cumulative.append(acc/total)
    if balance == 'linear': return cumulative
    r = math.radians(angle); dx = math.cos(r); dy = math.sin(r)
    projections = sorted(.5+(x-.5)*dx+(y-.5)*dy for x,y in (samples or bird_samples()))
    return [projections[round(c*(len(projections)-1))] for c in cumulative]

def stops(values, angle, style='blend', weights=None, balance='area', samples=None):
    if style == 'blend': return [(i/(len(values)-1), c) for i,c in enumerate(values)]
    edges = [0]+boundaries(values, angle, weights, balance, samples)+[1]
    result = []
    for i,c in enumerate(values):
        # Short transitions retain each named colour instead of muddying entire bands.
        left = min(edges[i]-edges[i-1] if i else 1, edges[i+1]-edges[i])*.12 if style == 'soft' else 0
        right = min(edges[i+1]-edges[i], edges[i+2]-edges[i+1] if i+2<len(edges) else 1)*.12 if style == 'soft' else 0
        result.extend([(edges[i]+(left if i else 0), c), (edges[i+1]-(right if i<len(values)-1 else 0), c)])
    return result

def union_jack(name, values, angle):
    pink, white, field = values
    # Large, non-repeating content keeps rotated corners filled; the bird clips the paint.
    content = f'<rect x="-600" y="-600" width="1800" height="1600" fill="{field}"/>'
    for width, colour in [(90,white),(34,pink)]:
        content += f'<path d="M-600 -400L1200 800M-600 800L1200 -400" fill="none" stroke="{colour}" stroke-width="{width}"/>'
    for width, colour in [(130,white),(76,pink)]:
        content += f'<path d="M300 -800V1200M-900 200H1500" fill="none" stroke="{colour}" stroke-width="{width}"/>'
    return f'<pattern id="{name}" width="1" height="1" viewBox="0 0 600 400" preserveAspectRatio="none"><g transform="rotate({angle} 300 200)">{content}</g></pattern>'

def paint(name, values, angle, style='blend', weights=None, balance='area'):
    if len(values)==1: return '',values[0]
    if style=='union-jack': return union_jack(name,values,angle),f'url(#{name})'
    r=math.radians(angle);dx=50*math.cos(r);dy=50*math.sin(r)
    s=''.join(f'<stop offset="{p:.6f}" stop-color="{c}"/>' for p,c in stops(values,angle,style,weights,balance))
    return f'<linearGradient id="{name}" x1="{50-dx}%" y1="{50-dy}%" x2="{50+dx}%" y2="{50+dy}%">{s}</linearGradient>',f'url(#{name})'
