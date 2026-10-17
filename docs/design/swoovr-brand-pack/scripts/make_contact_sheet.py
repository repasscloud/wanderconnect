#!/usr/bin/env python3
"""Discover built-in and arbitrary custom artwork and regenerate an overview."""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[1]

def discover(roots):
    entries=[]
    for root in roots:
        if not root.is_dir():continue
        for image in sorted(root.rglob('app-rounded-1024.png')):
            if any(p.startswith('.') for p in image.relative_to(root).parts):continue
            entries.append((f'{root.name}/{image.parent.relative_to(root)}',image))
    return entries

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--input',type=Path,action='append',help='Scan this output tree recursively; repeat to combine folders')
    p.add_argument('--output',type=Path,default=ROOT/'OVERVIEW.png')
    a=p.parse_args();entries=discover(a.input if a.input is not None else [ROOT/'assets',ROOT/'custom-assets'])
    if not entries:p.error('No app-rounded-1024.png files found. Build raster artwork first.')
    try:font=ImageFont.truetype('DejaVuSans.ttf',26);small=ImageFont.truetype('DejaVuSans.ttf',19)
    except OSError:
        try:font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',26);small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',19)
        except OSError:font=small=ImageFont.load_default()
    height=450+((len(entries)+3)//4)*320
    out=Image.new('RGB',(1600,height),'#F5F2F8');d=ImageDraw.Draw(out)
    d.text((65,32),f'SWOOVR / {len(entries)} DESIGNS',font=font,fill='#23162F')
    header=ROOT/'assets/primary/horizontal-1024.png'
    if header.exists():
        with Image.open(header) as im:
            im.thumbnail((880,260));out.paste(im,((1600-im.width)//2,95),im)
    for i,(label,file) in enumerate(entries):
        x=65+(i%4)*385;y=400+(i//4)*320
        with Image.open(file) as im:
            im=im.convert('RGBA').resize((240,240),Image.Resampling.LANCZOS);out.paste(im,(x+50,y),im)
        # Split long labels rather than letting them run into the next card.
        words=label.split('/',1)
        for line,word in enumerate(words[:2]):d.text((x+12,y+250+line*23),word,font=small,fill='#23162F')
    d.text((65,height-40),'Generated from artwork folders; run this script again after custom builds.',font=small,fill='#6E607D')
    a.output.parent.mkdir(parents=True,exist_ok=True);out.save(a.output)
    print(f'{a.output}: {len(entries)} designs')
if __name__=='__main__':main()
