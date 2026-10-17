#!/usr/bin/env python3
from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
# Use any installed TrueType font only for specimen labels, never for the logo.
try:
 font=ImageFont.truetype('DejaVuSans.ttf',26);small=ImageFont.truetype('DejaVuSans.ttf',19)
except OSError:font=small=ImageFont.load_default()
out=Image.new('RGB',(1600,1410),'#F5F2F8');d=ImageDraw.Draw(out)
d.text((65,32),'SWOOVR / BRAND & CAMPAIGN PACK',font=font,fill='#23162F')
with Image.open(ROOT/'assets/primary/horizontal-1024.png') as im:
 im.thumbnail((880,260));out.paste(im,((1600-im.width)//2,95),im)
themes=json.loads((ROOT/'source/themes.json').read_text())
for i,name in enumerate(themes):
 x=65+(i%4)*385;y=400+(i//4)*320
 with Image.open(ROOT/f'assets/{name}/app-rounded-1024.png') as im:
  im=im.resize((240,240),Image.Resampling.LANCZOS);out.paste(im,(x+50,y),im)
 d.text((x+40,y+258),name,font=small,fill='#23162F')
d.text((65,1370),'Separate vectors / 4096px logos / ICO + ICNS / Social canvases / Offline colour studio',font=small,fill='#6E607D')
out.save(ROOT/'OVERVIEW.png')
