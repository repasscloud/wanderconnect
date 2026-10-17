import json,sys,tempfile,unittest,subprocess
from pathlib import Path
from types import SimpleNamespace
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'scripts'))
import configs,build
from make_contact_sheet import discover

class CustomConfigTests(unittest.TestCase):
    def setUp(self):self.themes=json.loads((ROOT/'source/themes.json').read_text())
    def test_swap_moves_complete_flag_and_independent_angle(self):
        t=configs.resolve({'base':'pride-bi','swap':'bird-background','bird':['#0D0F12'],'background_angle':138},self.themes)
        self.assertEqual(t['background'],self.themes['pride-bi']['bird'])
        self.assertEqual(configs.settings(t,'background'),dict(style='soft',weights=[2,1,2],balance='linear',angle=138))
        self.assertEqual(configs.settings(t,'bird')['style'],'blend')
        self.assertEqual(t['bird'],['#0D0F12'])
        self.assertEqual(self.themes['pride-bi']['bird'][0],'#D60270')
    def test_explicit_weights_do_not_depend_on_json_key_order(self):
        t=configs.resolve({'bird_weights':[2,1],'bird':['#FFFFFF','#000000']},self.themes)
        self.assertEqual(configs.settings(t,'bird')['weights'],[2,1])
    def test_unknown_fields_rejected(self):
        with self.assertRaises(ValueError):configs.resolve({'backgound':['#FFFFFF']},self.themes)
    def test_custom_examples_render_and_builtin_paths_unchanged(self):
        examples=json.loads((ROOT/'source/custom-examples.json').read_text())
        for config in examples.values():self.assertIn('<svg',build.svg('app-rounded',configs.resolve(config,self.themes)))
        for name,t in self.themes.items():self.assertEqual(build.svg('bird',t),(ROOT/'assets'/name/'bird.svg').read_text())
    def test_build_metadata_never_claims_unrelated_files(self):
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp)/'variant';folder.mkdir();(folder/'notes.txt').write_text('keep');(folder/'personal.png').write_text('keep')
            build.build('variant',self.themes['primary'],Path(temp),SimpleNamespace(svg_only=True,core_only=True,angle=None))
            meta=json.loads((folder/'build.json').read_text());self.assertNotIn('personal.png',meta['files']);self.assertNotIn('notes.txt',meta['files'])
            self.assertEqual((folder/'personal.png').read_text(),'keep')
    def test_default_selection_excludes_custom_configs_and_preserves_other_folders(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);custom=root/'my-other-variant';custom.mkdir();(custom/'keep.txt').write_text('keep')
            subprocess.run([sys.executable,str(ROOT/'scripts/build.py'),'--svg-only','--output',temp],check=True,capture_output=True)
            names={p.name for p in root.iterdir() if p.is_dir()}
            self.assertEqual(names,set(self.themes)|{'my-other-variant'})
            self.assertEqual((custom/'keep.txt').read_text(),'keep')
    def test_named_cli_swap_and_colour_override(self):
        with tempfile.TemporaryDirectory() as temp:
            subprocess.run([sys.executable,str(ROOT/'scripts/build.py'),'--theme','pride-trans-5','--name','my-carbon','--swap','bird-background','--bird','#0D0F12','--background-angle','138','--svg-only','--output',temp],check=True,capture_output=True)
            meta=json.loads((Path(temp)/'my-carbon/build.json').read_text())
            self.assertEqual(meta['theme']['bird'],['#0D0F12'])
            self.assertEqual(meta['theme']['background_style'],'soft')
            self.assertEqual(meta['theme']['background_angle'],138)

    def test_reddit_banners_fit_the_shallow_canvas(self):
        import xml.etree.ElementTree as E,re
        for kind,width in [('reddit-banner',1072),('reddit-mobile-banner',1080)]:
            root=E.fromstring(build.svg(kind,self.themes['primary']))
            self.assertEqual(root.get('viewBox'),f'0 0 {width} 128')
            group=root.find('{http://www.w3.org/2000/svg}g')
            x,y,scale=map(float,re.findall(r'-?\d+(?:\.\d+)?',group.get('transform')))
            self.assertGreaterEqual(y,12)
            self.assertLessEqual(y+360*scale,116.00001)
            self.assertGreaterEqual(x,0)
            self.assertLessEqual(x+1160*scale,width)
        with tempfile.TemporaryDirectory() as temp:
            build.build('reddit-test',self.themes['primary'],Path(temp),SimpleNamespace(svg_only=True,core_only=False,angle=None))
            for kind in ['reddit-banner','reddit-mobile-banner']:self.assertTrue((Path(temp)/'reddit-test'/f'{kind}.svg').exists())

    def test_overview_discovers_arbitrary_nested_outputs(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            for name in ['mine','campaign/another']:
                f=root/name/'app-rounded-1024.png';f.parent.mkdir(parents=True);f.write_bytes(b'test')
            self.assertEqual(len(discover([root])),2)

if __name__=='__main__':unittest.main()
