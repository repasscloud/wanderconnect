"""D362 refresh: emptying the UAT R2 photo bucket, against an in-memory fake S3 API (no network)."""
import datetime
import io
import os
from pathlib import Path
import sys
import unittest
import urllib.error
import urllib.parse
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/uat'))
import r2_empty  # noqa: E402

NS = 'http://s3.amazonaws.com/doc/2006-03-01/'


class FakeR2:
    """Answers ListObjectsV2 (pages of 2) and DeleteObjects; records every request."""

    def __init__(self, keys, fail_delete=False):
        self.keys, self.requests, self.fail_delete = sorted(keys), [], fail_delete

    def open(self, request, timeout=None):
        url = urllib.parse.urlsplit(request.full_url)
        query = dict(urllib.parse.parse_qsl(url.query, keep_blank_values=True))
        self.requests.append((request.get_method(), url.path, query, dict(request.header_items())))
        assert request.get_header('Authorization').startswith('AWS4-HMAC-SHA256 Credential=AKID/20261112/auto/s3/aws4_request')
        if request.get_method() == 'GET':
            start = int(query.get('continuation-token', '0'))
            page = self.keys[start:start + 2]
            truncated = start + 2 < len(self.keys)
            body = f'<ListBucketResult xmlns="{NS}">' + ''.join(f'<Contents><Key>{k}</Key></Contents>' for k in page) + \
                   f'<IsTruncated>{str(truncated).lower()}</IsTruncated>' + \
                   (f'<NextContinuationToken>{start + 2}</NextContinuationToken>' if truncated else '') + '</ListBucketResult>'
        else:
            if self.fail_delete:
                raise urllib.error.HTTPError(request.full_url, 403, 'Forbidden', {}, io.BytesIO(b''))
            removed = [e.text for e in ET.fromstring(request.data).iter('Key')]
            self.keys = [k for k in self.keys if k not in removed]
            body = f'<DeleteResult xmlns="{NS}"></DeleteResult>'
        return io.BytesIO(body.encode())


def client(fake):
    now = lambda: datetime.datetime(2026, 11, 12, 1, 2, 3, tzinfo=datetime.timezone.utc)
    return r2_empty.R2('https://acct.r2.cloudflarestorage.com', 'swoovr-uat-media', 'AKID', 'SECRET', fake, now)


class R2EmptyChecks(unittest.TestCase):
    def test_deletes_every_object_across_pages_and_keeps_the_bucket(self):
        fake = FakeR2([f'photos/{i}.webp' for i in range(5)])
        self.assertEqual(r2_empty.empty(client(fake)), 5)
        self.assertEqual(fake.keys, [])
        posts = [r for r in fake.requests if r[0] == 'POST']
        self.assertTrue(posts and all(r[1] == '/swoovr-uat-media' and r[2] == {'delete': ''} for r in posts))
        self.assertTrue(all('Content-md5' in r[3] for r in posts))
        self.assertFalse(any(r[0] == 'DELETE' for r in fake.requests), 'the bucket itself is never deleted')

    def test_empty_bucket_is_a_no_op(self):
        fake = FakeR2([])
        self.assertEqual(r2_empty.empty(client(fake)), 0)
        self.assertEqual([r[0] for r in fake.requests], ['GET'])

    def test_failure_is_reported_without_details(self):
        with self.assertRaisesRegex(ValueError, 'HTTP 403'):
            r2_empty.empty(client(FakeR2(['a'], fail_delete=True)))

    def test_bucket_name_must_match_configuration(self):
        env = {'Media__R2__Bucket': 'swoovr-uat-media', 'Media__R2__Endpoint': 'https://acct.r2.cloudflarestorage.com',
               'Media__R2__AccessKeyId': 'AKID', 'Media__R2__SecretAccessKey': 'SECRET'}
        old = dict(os.environ)
        try:
            os.environ.update(env)
            with self.assertRaisesRegex(ValueError, 'must equal the configured'):
                r2_empty.main(['--bucket', 'swoovr-prod-media'])
            with self.assertRaisesRegex(ValueError, 'Usage'):
                r2_empty.main([])
        finally:
            os.environ.clear()
            os.environ.update(old)

    def test_endpoint_must_be_https_origin(self):
        for bad in ['http://acct.r2.cloudflarestorage.com', 'https://acct.r2.cloudflarestorage.com/bucket', '']:
            with self.assertRaises(ValueError):
                r2_empty.R2(bad, 'swoovr-uat-media', 'a', 'b')


if __name__ == '__main__':
    unittest.main()
