"""Regression checks for rotated flag coverage and Python/studio parity."""
import json, math, subprocess, sys, unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts'))
import build, paints

class FlagPaintTests(unittest.TestCase):
    def setUp(self): self.themes=json.loads((ROOT/'source/themes.json').read_text())
    def test_trans_shares_at_requested_rotation(self):
        t=self.themes['pride-trans-5'];angle=138
        edges=paints.boundaries(t['bird'],angle,t['weights']);r=math.radians(angle)
        p=[.5+(x-.5)*math.cos(r)+(y-.5)*math.sin(r) for x,y in paints.bird_samples()]
        shares=[sum(lo<=v<hi for v in p)/len(p) for lo,hi in zip([0]+edges,edges+[1])]
        for share in shares:self.assertAlmostEqual(share,.2,delta=.012)
    def test_historical_palettes(self):
        self.assertEqual(len(self.themes['pride-9']['bird']),9)
        self.assertEqual(self.themes['pride-7']['bird'],self.themes['pride-8']['bird'][1:])
        self.assertEqual(self.themes['pride-9']['bird'][1:],self.themes['pride-8']['bird'])
        self.assertEqual(self.themes['pride-bi']['weights'],[2,1,2])
    def test_studio_parity_and_monotonic_offsets(self):
        cases=[]
        for t in self.themes.values():
            if t.get('style')!='soft':continue
            for angle in [0,45,90,138,180,270,360]:
                for style in ['soft','bands','blend']:
                    cases.append([t['bird'],angle,style,t['weights'],'area'])
        js='const DATA='+json.dumps({'samples':paints.bird_samples()})+';\n'+(ROOT/'scripts/paints.js').read_text()+'\nprocess.stdout.write(JSON.stringify('+json.dumps(cases)+'.map(c=>flagStops(...c))));'
        actual=json.loads(subprocess.run(['node','-e',js],check=True,capture_output=True,text=True).stdout)
        for case,got in zip(cases,actual):
            expected=paints.stops(*case)
            self.assertEqual(len(expected),len(got))
            for (p,c),(q,d) in zip(expected,got):self.assertAlmostEqual(p,q,places=10);self.assertEqual(c,d)
            self.assertTrue(all(0<=p<=1 for p,c in got))
            self.assertEqual(sorted(p for p,c in got),[p for p,c in got])
    def test_maskable_and_pattern_paints_resolve(self):
        import xml.etree.ElementTree as E
        t=self.themes['pink-union-jack'];svg=E.fromstring(build.svg('bird',t,138))
        ns='{http://www.w3.org/2000/svg}'
        self.assertIsNotNone(svg.find('.//'+ns+'pattern'))
        self.assertEqual(svg.find(ns+'path').get('fill'),'url(#bird)')
    def test_inner_back_join_is_tangent_continuous(self):
        import re
        geometry=json.loads((ROOT/'source/geometry.json').read_text())['bird']['path']
        curves=[];start=None
        for command,numbers in re.findall(r'([MCZ])([^MCZ]*)',geometry.split('Z')[0]):
            v=list(map(float,re.findall(r'-?\d+(?:\.\d+)?',numbers)))
            if command=='M':start=v
            elif command=='C':
                curves.append([start,v[:2],v[2:4],v[4:]])
                start=v[4:]
        fillet=next(i for i,c in enumerate(curves) if c[3]==[239.85,218.65])
        for i in [fillet-1,fillet]:
            a,b=curves[i],curves[i+1]
            incoming=[a[3][k]-a[2][k] for k in [0,1]]
            outgoing=[b[1][k]-b[0][k] for k in [0,1]]
            cosine=sum(x*y for x,y in zip(incoming,outgoing))/(math.hypot(*incoming)*math.hypot(*outgoing))
            self.assertGreater(cosine,.999999)
        self.assertEqual(geometry.count('Z'),2)

    def test_up_to_nine_custom_colours(self):
        build.colours(['#FFFFFF']*9)
        with self.assertRaises(ValueError):build.colours(['#FFFFFF']*10)

if __name__=='__main__': unittest.main()
