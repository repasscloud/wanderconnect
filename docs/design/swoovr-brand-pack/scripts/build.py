#!/usr/bin/env python3
"""Build Swoovr vector, raster, platform and campaign assets from geometry.json."""
import argparse, hashlib, json, math, re, shutil, subprocess, sys, tempfile, os
from pathlib import Path
from xml.sax.saxutils import escape
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
G=json.loads((ROOT/'source/geometry.json').read_text())
HEX=re.compile(r'^#[0-9a-fA-F]{6}$')
def colours(v):
    values=v.split(',') if isinstance(v,str) else v
    if not 1<=len(values)<=7 or not all(HEX.fullmatch(c) for c in values):
        raise ValueError('Use 1–7 comma-separated #RRGGBB colours.')
    return values

def paint(name,values,angle):
    values=colours(values)
    if len(values)==1:return '',values[0]
    r=math.radians(angle); dx=50*math.cos(r);dy=50*math.sin(r)
    stops=''.join(f'<stop offset="{i/(len(values)-1):.6f}" stop-color="{c}"/>' for i,c in enumerate(values))
    return f'<linearGradient id="{name}" x1="{50-dx}%" y1="{50-dy}%" x2="{50+dx}%" y2="{50+dy}%">{stops}</linearGradient>',f'url(#{name})'

def shape(kind,x,y,w,h,fill):
    g=G[kind]; bx,by,bw,bh=g['viewBox'];s=min(w/bw,h/bh)
    tx=x+(w-bw*s)/2-bx*s;ty=y+(h-bh*s)/2-by*s
    return f'<path fill="{fill}" fill-rule="evenodd" transform="translate({tx:.5f} {ty:.5f}) scale({s:.6f})" d="{g["path"]}"/>'

def svg(kind,t,angle=0):
    layouts={'bird':(384,324),'wordmark':(620,112),'horizontal':(1160,360),'stacked':(700,680),'app-square':(1024,1024),'app-rounded':(1024,1024),'adaptive-foreground':(1080,1080),'adaptive-background':(1080,1080),'avatar':(1024,1024),'social-square':(1080,1080),'social-portrait':(1080,1350),'story':(1080,1920),'open-graph':(1200,630),'x-header':(1500,500),'linkedin-cover':(1584,396),'youtube-banner':(2560,1440),'wallpaper':(3840,2160)}
    w,h=layouts[kind];defs=[];body=[]
    for name,key in [('bird','bird'),('word','word'),('background','background')]:
        if t.get(key) is not None:
            d,f=paint(name,t[key],angle);defs.append(d)
            if name=='bird':bf=f
            elif name=='word':wf=f
            else:bg=f
    transparent=kind in ('bird','wordmark','horizontal','stacked','adaptive-foreground')
    if not transparent:
        radius=224 if kind=='app-rounded' else (512 if kind=='avatar' else 0)
        body.append(f'<rect width="{w}" height="{h}" rx="{radius}" fill="{bg}"/>')
    if kind=='bird':body.append(shape('bird',0,0,w,h,bf))
    elif kind=='wordmark':body.append(shape('wordmark',0,0,w,h,wf))
    elif kind=='horizontal':
        body += [shape('bird',20,18,384,324,bf),shape('wordmark',450,163,690,125,wf)]
    elif kind=='stacked':
        body += [shape('bird',145,35,410,346,bf),shape('wordmark',40,480,620,112,wf)]
    elif kind=='adaptive-background':pass
    elif kind=='adaptive-foreground':body.append(shape('bird',300,330,480,405,bf))
    elif kind in ('app-square','app-rounded','avatar'):body.append(shape('bird',145,200,734,620,bf))
    else:
        # Keep all banner content in central crop-safe region, including YouTube's centre strip.
        if kind in ('x-header','linkedin-cover','youtube-banner','open-graph'):
            lw=min(w*.68,1200);lh=lw*360/1160;x=(w-lw)/2;y=(h-lh)/2
            body.append(f'<g transform="translate({x} {y}) scale({lw/1160})">'+shape('bird',20,18,384,324,bf)+shape('wordmark',450,163,690,125,wf)+'</g>')
        else:
            bw=w*.48;bh=bw*324/384
            body += [shape('bird',(w-bw)/2,h*.42-bh/2,bw,bh,bf),shape('wordmark',w*.18,h*.65,w*.64,w*.64*112/620,wf)]
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" role="img"><title>Swoovr {escape(kind)}</title><defs>{"".join(defs)}</defs>{"".join(body)}</svg>'

def save_image(im,dst,**options):
    # Write complete images outside the output tree before atomically publishing them.
    with tempfile.TemporaryDirectory(prefix='.swoovr-',dir=dst.parent) as tmp:
        temp=Path(tmp)/dst.name
        im.save(temp,**options)
        with Image.open(temp) as check: check.load()
        os.replace(temp,dst)

def render_batch(jobs):
    exe=shutil.which('inkscape')
    if not exe:raise RuntimeError('Install Inkscape and put inkscape on PATH, or use --svg-only.')
    # The CLI receives one process per asset for reliable cross-platform path handling.
    for src,dst,width in jobs:
        for attempt in range(3):
            with tempfile.TemporaryDirectory(prefix='.swoovr-',dir=dst.parent) as tmp:
                temp=Path(tmp)/'render.png'
                subprocess.run([exe,'--batch-process',str(src),'--export-type=png',f'--export-filename={temp}',f'--export-width={width}'],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
                with Image.open(temp) as check: check.load()
                os.replace(temp,dst)
            try:
                with Image.open(dst) as check: check.load()
                break
            except OSError:
                if attempt == 2: raise
                dst.unlink(missing_ok=True)

def build(theme,t,out,args):
    if not re.fullmatch(r'[a-zA-Z0-9_-]+',theme):raise ValueError('Theme name must contain letters, numbers, hyphens or underscores.')
    folder=out/theme;folder.mkdir(parents=True,exist_ok=True)
    kinds=['bird','wordmark','horizontal','stacked','app-square','app-rounded','adaptive-foreground','adaptive-background','avatar','social-square','social-portrait','story','open-graph','x-header','linkedin-cover','youtube-banner','wallpaper']
    if args.core_only:kinds=kinds[:9]
    jobs=[]
    for kind in kinds:
        src=folder/(kind+'.svg');src.write_text(svg(kind,t,args.angle))
        if not args.svg_only:
            width=4096 if kind in ('bird','wordmark','horizontal','stacked') else (1024 if kind.startswith('app-') else (1080 if kind.startswith('adaptive') else int(re.search(r'width="(\d+)"',src.read_text())[1])))
            jobs.append((src,folder/f'{kind}-{width}.png',width))
    if args.svg_only:return
    for job in jobs:
        render_batch([job])
        if job[0].stem in ('bird','wordmark','horizontal','stacked'):
            kind=job[0].stem
            with Image.open(job[1]) as im:
                im.load()
                for size in (512,1024,2048):
                    save_image(im.resize((size,round(im.height*size/im.width)),Image.Resampling.LANCZOS),folder/f'{kind}-{size}.png')
    with Image.open(folder/'app-square-1024.png') as im:
        # Opaque unrounded master: platform applies its own mask.
        im=im.convert('RGB')
        save_image(im,folder/'swoovr.ico',format='ICO',sizes=[(s,s) for s in (16,24,32,48,64,128,256)])
        save_image(im,folder/'swoovr.icns',format='ICNS')
        for size in (16,20,24,29,32,40,48,58,60,64,72,76,80,87,96,120,128,144,152,167,180,192,256,384,512,1024):
            save_image(im.resize((size,size),Image.Resampling.LANCZOS),folder/f'app-{size}.png')
        for name,size in [('apple-touch-icon',180),('pwa-192',192),('pwa-512',512)]:
            save_image(im.resize((size,size),Image.Resampling.LANCZOS),folder/f'{name}.png')
    # Maskable uses adaptive foreground geometry on opaque background (more generous safe area).
    src=folder/'pwa-maskable.svg'
    src.write_text(svg('adaptive-background',t,args.angle).replace('</svg>',shape('bird',270,310,540,456,paint('bird',t['bird'],args.angle)[1])+'</svg>'))
    render_batch([(src,folder/'pwa-maskable-512.png',512)])
    (folder/'manifest.webmanifest').write_text(json.dumps({'name':'Swoovr','short_name':'Swoovr','icons':[{'src':f'pwa-{s}.png','sizes':f'{s}x{s}','type':'image/png','purpose':'any'} for s in (192,512)]+[{'src':'pwa-maskable-512.png','sizes':'512x512','type':'image/png','purpose':'maskable'}]},indent=2))

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--theme',default='all',help='Preset name, all, or custom')
    p.add_argument('--bird',help='1–7 comma-separated #RRGGBB stops')
    p.add_argument('--word',help='Independent wordmark colour/gradient stops')
    p.add_argument('--background',help='Independent opaque background colour/gradient stops')
    p.add_argument('--angle',type=float,default=0,help='0=left to right, 90=top to bottom')
    p.add_argument('--output',type=Path,default=ROOT/'assets')
    p.add_argument('--svg-only',action='store_true')
    p.add_argument('--core-only',action='store_true')
    a=p.parse_args();themes=json.loads((ROOT/'source/themes.json').read_text())
    if a.theme=='all': chosen=themes
    elif a.theme=='custom':chosen={'custom':dict(themes['primary'])}
    elif a.theme in themes:chosen={a.theme:dict(themes[a.theme])}
    else:p.error('Unknown theme; choose '+', '.join(themes)+', custom, all')
    if a.theme=='all' and any((a.bird,a.word,a.background)):p.error('Choose one theme when overriding colours.')
    try:
        for name,t in chosen.items():
            for k in ('bird','word','background'):
                if getattr(a,k):t[k]=colours(getattr(a,k))
            print('Building',name,flush=True);build(name,t,a.output.resolve(),a)
        entries={str(f.relative_to(a.output)):hashlib.sha256(f.read_bytes()).hexdigest() for f in a.output.rglob('*') if f.is_file() and f.name!='checksums.json'}
        (a.output/'checksums.json').write_text(json.dumps(entries,indent=2))
    except (ValueError,RuntimeError,OSError,subprocess.CalledProcessError) as e:p.exit(1,f'Build failed: {e}\n')
if __name__=='__main__':main()
