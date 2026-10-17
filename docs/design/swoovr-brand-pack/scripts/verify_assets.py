#!/usr/bin/env python3
"""Decode every delivered raster/container and validate every SVG and font."""
from pathlib import Path
from PIL import Image
import argparse,json,xml.etree.ElementTree as ET
from fontTools.ttLib import TTFont
p=argparse.ArgumentParser();p.add_argument('folder',type=Path,nargs='?',default=Path(__file__).resolve().parents[1]);a=p.parse_args()
errors=[];counts={}
for f in a.folder.rglob('*'):
 if not f.is_file():continue
 ext=f.suffix.lower();counts[ext]=counts.get(ext,0)+1
 try:
  if ext in ('.png','.ico','.icns'):
   with Image.open(f) as im:
    im.load()
    if ext=='.ico':
     for size in im.ico.sizes():im.ico.getimage(size).load()
    if ext=='.icns':
     for size in im.icns.itersizes():im.icns.getimage(size).load()
  elif ext=='.svg':
   tree=ET.parse(f)
   if tree.find('.//{http://www.w3.org/2000/svg}image') is not None:raise ValueError('Embedded bitmap found')
   ids=[n.attrib['id'] for n in tree.iter() if 'id' in n.attrib]
   if len(ids)!=len(set(ids)):raise ValueError('Duplicate SVG ids')
  elif ext=='.ttf':TTFont(f).getBestCmap()
  elif ext in ('.json','.webmanifest'):json.loads(f.read_text())
 except Exception as e:errors.append(f'{f}: {e}')
print(json.dumps({'counts':counts,'errors':errors},indent=2))
raise SystemExit(bool(errors))
