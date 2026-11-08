"""Issue 254 (D342): every fixed route the member web client calls must be on the web host's BFF allow-list.

The BFF checks its allow-list before sign-in, so an anonymous request answers 401 for an allowed path and 404 for one
that is missing (a missing path made /api/me/contact silently fail in PR #255). Routes built at run time (with ids) are
not listed here; their own suites cover them.
"""
import re
import urllib.error
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parents[1]
# Fixed routes the shared member client sends ("api/..." literals without interpolation), query string removed.
# A literal ending in "/" is a prefix the client appends an id to, so it is not a route by itself.
ROUTE = re.compile(r'(?<![$\w])"(api/[a-z0-9/_-]*[a-z0-9_-])(?:\?[^"]*)?"')
# Member client routes the web app deliberately never sends through the BFF; keep this list short and give the reason.
NATIVE_ONLY = {
    'api/me/devices',  # push notification device registration: the native app only (the web has no push).
}


def member_client_routes():
    text = (root / 'src/Swoovr.Client/MemberClient.cs').read_text(encoding='utf-8')
    return sorted({m.group(1) for m in ROUTE.finditer(text)} - NATIVE_ONLY)


def check_bff_routes(web):
    missing = []
    for route in member_client_routes():
        request = urllib.request.Request(web + 'bff/' + route, headers={'X-Swoovr-Bff': '1'})
        try:
            urllib.request.urlopen(request).close()
            status = 200
        except urllib.error.HTTPError as error:
            status = error.code
        if status == 404:
            missing.append(route)
        elif status != 401:
            raise AssertionError(f'/bff/{route}: expected 401 without a sign-in, got {status}')
    assert not missing, 'member client routes missing from the BFF allow-list (Bff.cs): ' + ', '.join(missing)
    return len(member_client_routes())


if __name__ == '__main__':
    print('\n'.join(member_client_routes()))
