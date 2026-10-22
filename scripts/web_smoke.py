"""Fetch actual appearance HTML and every linked script/stylesheet, without a browser."""
from html.parser import HTMLParser
import urllib.parse
import urllib.request


class Assets(HTMLParser):
    def __init__(self):
        super().__init__()
        self.paths = []

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag == 'script' and values.get('src'):
            self.paths.append((values['src'], 'javascript'))
        if tag == 'link' and values.get('rel') == 'stylesheet':
            self.paths.append((values['href'], 'css'))


def check_web_assets(base):
    with urllib.request.urlopen(base + 'settings/appearance', timeout=5) as response:
        html = response.read().decode()
    assert 'data-appearance-settings' in html and 'Account sync is not connected' in html
    for scheme in ['standard', 'lounge', 'pride', 'trans', 'union', 'violet', 'coral', 'eucalyptus']:
        assert f'value="{scheme}"' in html
    assets = Assets()
    assets.feed(html)
    assert len(assets.paths) >= 7, 'Appearance asset links missing'
    for path, kind in assets.paths:
        target = urllib.parse.urljoin(base, path)
        assert urllib.parse.urlsplit(target).netloc == urllib.parse.urlsplit(base).netloc
        with urllib.request.urlopen(target, timeout=5) as response:
            assert kind in response.headers.get('Content-Type', ''), (path, 'wrong asset type')
            assert response.read(), (path, 'empty asset')
