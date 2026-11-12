#!/usr/bin/env python3
"""Empty the UAT photo bucket on refresh (owner, 12 November 2026; D362). Runs in the operator image.

Deletes every object in Media__R2__Bucket through R2's S3-compatible API (path-style, region `auto`, AWS SigV4, the same
scheme as Swoovr.Infrastructure R2MediaStore). Refuses unless the bucket named on the command line equals the configured
one, so a wrong env file can't empty another bucket. The bucket itself is kept. Prints counts only; never keys or secrets.

  r2_empty.py --bucket NAME
"""
import base64
import datetime
import hashlib
import hmac
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

NS = '{http://s3.amazonaws.com/doc/2006-03-01/}'


def quote(value):
    return urllib.parse.quote(value, safe='-_.~')


class R2:
    def __init__(self, endpoint, bucket, access_key, secret_key, opener=None, now=None):
        url = urllib.parse.urlsplit(endpoint)
        if url.scheme != 'https' or not url.hostname or url.path not in ('', '/') or url.query or url.username:
            raise ValueError('Media__R2__Endpoint must be an https origin.')
        if not re.fullmatch(r'[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]', bucket):
            raise ValueError('Invalid bucket name.')
        self.host, self.base, self.bucket = url.netloc, f'https://{url.netloc}', bucket
        self.access_key, self.secret_key = access_key, secret_key
        self.opener = opener or urllib.request.build_opener(urllib.request.ProxyHandler({}))
        self.now = now or (lambda: datetime.datetime.now(datetime.timezone.utc))

    def send(self, method, query, body=b'', extra=None):
        path = '/' + quote(self.bucket)
        canonical_query = '&'.join(f'{quote(k)}={quote(v)}' for k, v in sorted(query.items()))
        payload = hashlib.sha256(body).hexdigest()
        stamp = self.now().strftime('%Y%m%dT%H%M%SZ')
        headers = {'host': self.host, 'x-amz-content-sha256': payload, 'x-amz-date': stamp, **(extra or {})}
        names = sorted(headers)
        canonical = '\n'.join([method, path, canonical_query, ''.join(f'{n}:{headers[n]}\n' for n in names),
                               ';'.join(names), payload])
        scope = f'{stamp[:8]}/auto/s3/aws4_request'
        to_sign = '\n'.join(['AWS4-HMAC-SHA256', stamp, scope, hashlib.sha256(canonical.encode()).hexdigest()])
        key = ('AWS4' + self.secret_key).encode()
        for part in (stamp[:8], 'auto', 's3', 'aws4_request'):
            key = hmac.new(key, part.encode(), hashlib.sha256).digest()
        signature = hmac.new(key, to_sign.encode(), hashlib.sha256).hexdigest()
        headers['authorization'] = (f'AWS4-HMAC-SHA256 Credential={self.access_key}/{scope}, '
                                    f'SignedHeaders={";".join(names)}, Signature={signature}')
        url = self.base + path + ('?' + canonical_query if canonical_query else '')
        request = urllib.request.Request(url, data=body if method != 'GET' else None, method=method,
                                         headers={k: v for k, v in headers.items() if k != 'host'})
        try:
            with self.opener.open(request, timeout=60) as response:
                return ET.fromstring(response.read() or b'<x/>')
        except urllib.error.HTTPError as error:
            raise ValueError(f'R2 {method} answered HTTP {error.code}.') from None

    def keys(self):
        token = None
        while True:
            query = {'list-type': '2', 'max-keys': '1000'}
            if token:
                query['continuation-token'] = token
            page = self.send('GET', query)
            yield [item.findtext(NS + 'Key') for item in page.iter(NS + 'Contents')]
            if page.findtext(NS + 'IsTruncated') != 'true':
                return
            token = page.findtext(NS + 'NextContinuationToken')

    def delete(self, keys):
        root = ET.Element('Delete')
        ET.SubElement(root, 'Quiet').text = 'true'
        for key in keys:
            ET.SubElement(ET.SubElement(root, 'Object'), 'Key').text = key
        body = ET.tostring(root, encoding='utf-8', xml_declaration=True)
        result = self.send('POST', {'delete': ''}, body,
                           {'content-md5': base64.b64encode(hashlib.md5(body).digest()).decode(),
                            'content-type': 'application/xml'})
        errors = list(result.iter(NS + 'Error'))
        if errors:
            raise ValueError(f'R2 refused to delete {len(errors)} object(s).')


def empty(client):
    deleted = 0
    while True:  # list again after each pass: deleting while paginating would skip objects
        batch = next(client.keys())
        if not batch:
            return deleted
        client.delete(batch)
        deleted += len(batch)


def main(argv):
    if len(argv) != 2 or argv[0] != '--bucket':
        raise ValueError('Usage: r2-empty --bucket NAME')
    env = os.environ
    bucket = env.get('Media__R2__Bucket', '')
    if not bucket or argv[1] != bucket:
        raise ValueError('The bucket to empty must equal the configured Media__R2__Bucket.')
    client = R2(env.get('Media__R2__Endpoint', ''), bucket, env.get('Media__R2__AccessKeyId', ''),
                env.get('Media__R2__SecretAccessKey', ''))
    count = empty(client)
    print(f'R2 bucket emptied: {count} object(s) deleted; the bucket itself is kept.')
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main(sys.argv[1:]))
    except (ValueError, OSError, ET.ParseError) as error:
        raise SystemExit(f'R2 empty failed: {error}')
