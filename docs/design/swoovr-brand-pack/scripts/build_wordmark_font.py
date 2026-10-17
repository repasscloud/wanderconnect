#!/usr/bin/env python3
"""Build an exact wordmark-only font, NOT a full alphabet typeface."""
from pathlib import Path
import json
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.transformPen import TransformPen
from fontTools.svgLib.path import parse_path
ROOT=Path(__file__).resolve().parents[1]
g=json.loads((ROOT/'source/geometry.json').read_text())['wordmark']
fb=FontBuilder(1000,isTTF=True);fb.setupGlyphOrder(['.notdef','space','swoovr'])
glyphs={}
for name in ['.notdef','space','swoovr']:
 pen=TTGlyphPen(None)
 if name=='swoovr':
  bx,by,bw,bh=g['viewBox'];scale=800/bh
  parse_path(g['path'],TransformPen(Cu2QuPen(pen,1,reverse_direction=True),(scale,0,0,-scale,-bx*scale,800+by*scale)))
 glyphs[name]=pen.glyph()
fb.setupCharacterMap({32:'space',0xE000:'swoovr'})
fb.setupGlyf(glyphs);fb.setupHorizontalMetrics({'.notdef':(500,0),'space':(250,0),'swoovr':(round(620*800/112),0)})
fb.setupHorizontalHeader(ascent=900,descent=-100)
fb.setupNameTable({'familyName':'Swoovr Wordmark','styleName':'Regular','uniqueFontIdentifier':'SwoovrWordmark-1.0','fullName':'Swoovr Wordmark Regular','psName':'SwoovrWordmark-Regular','version':'Version 1.0'})
fb.setupOS2(sTypoAscender=900,sTypoDescender=-100,usWinAscent=900,usWinDescent=100)
fb.setupPost();fb.setupMaxp()
folder=ROOT/'fonts';folder.mkdir(exist_ok=True);fb.save(folder/'SwoovrWordmark-Regular.ttf')
print(folder/'SwoovrWordmark-Regular.ttf')
