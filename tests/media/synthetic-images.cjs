// Synthetic test images for the photo suites (D203): solid colours, gradients, noise and text drawn in a headless
// browser canvas. Never real people, never explicit or abuse imagery. `withExif` adds an APP1 Exif segment carrying an
// orientation and a GPS IFD with a marker string, so the suites can prove both are gone after upload.

/** PNG or JPEG bytes from a canvas: kind is solid, gradient, noise or text. */
async function synthetic(page, { kind = 'gradient', width = 640, height = 480, type = 'image/png', seed = 1, label = 'SWOOVR TEST' } = {}) {
  const dataUrl = await page.evaluate(({ kind, width, height, type, seed, label }) => {
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    const g = canvas.getContext('2d');
    let s = seed;
    const random = () => (s = (s * 1103515245 + 12345) % 2147483648) / 2147483648;
    if (kind === 'solid') { g.fillStyle = `hsl(${(seed * 47) % 360} 60% 50%)`; g.fillRect(0, 0, width, height); }
    else if (kind === 'noise') {
      const image = g.createImageData(width, height);
      for (let i = 0; i < image.data.length; i += 4) { image.data[i] = random() * 255; image.data[i + 1] = random() * 255; image.data[i + 2] = random() * 255; image.data[i + 3] = 255; }
      g.putImageData(image, 0, 0);
    } else {
      const gradient = g.createLinearGradient(0, 0, width, height);
      gradient.addColorStop(0, `hsl(${(seed * 47) % 360} 70% 45%)`); gradient.addColorStop(1, `hsl(${(seed * 47 + 160) % 360} 70% 60%)`);
      g.fillStyle = gradient; g.fillRect(0, 0, width, height);
    }
    if (kind === 'text' || kind === 'gradient') { g.fillStyle = '#000'; g.font = '32px sans-serif'; g.fillText(`${label} ${seed}`, 20, 60); }
    return canvas.toDataURL(type, 0.9);
  }, { kind, width, height, type, seed, label });
  return Buffer.from(dataUrl.split(',')[1], 'base64');
}

/** Inserts an Exif segment (orientation + GPS marker "SWOOVRGPS") after the JPEG's start-of-image marker. */
function withExif(jpeg, orientation = 6) {
  const t = [];
  const u16 = v => t.push(v & 255, (v >> 8) & 255);
  const u32 = v => { u16(v & 0xffff); u16((v >>> 16) & 0xffff); };
  t.push(0x49, 0x49); u16(42); u32(8);
  u16(2); u16(0x0112); u16(3); u32(1); u16(orientation); u16(0);
  const gps = 8 + 2 + 24 + 4;
  u16(0x8825); u16(4); u32(1); u32(gps); u32(0);
  const marker = Buffer.from('SWOOVRGPS\0', 'ascii');
  u16(2); u16(0x0001); u16(2); u32(2); t.push(0x53, 0); u16(0);
  u16(0x0012); u16(2); u32(marker.length); u32(gps + 2 + 24 + 4); u32(0);
  const payload = Buffer.concat([Buffer.from('Exif\0\0', 'binary'), Buffer.from(t), marker]);
  const header = Buffer.from([0xff, 0xe1, (payload.length + 2) >> 8, (payload.length + 2) & 255]);
  return Buffer.concat([jpeg.subarray(0, 2), header, payload, jpeg.subarray(2)]);
}

/** Width and height of a baseline or progressive JPEG (SOF0/SOF2). */
function jpegSize(jpeg) {
  for (let i = 2; i < jpeg.length;) {
    if (jpeg[i] !== 0xff) return null;
    const marker = jpeg[i + 1], length = jpeg.readUInt16BE(i + 2);
    if (marker === 0xc0 || marker === 0xc2) return { height: jpeg.readUInt16BE(i + 5), width: jpeg.readUInt16BE(i + 7) };
    i += 2 + length;
  }
  return null;
}

/** Uploads bytes as multipart to a URL with a bearer token (API) or extra headers (BFF). */
async function upload(url, bytes, { token, headers = {}, name = 'photo.jpg', type = 'image/jpeg', replaces } = {}) {
  const form = new FormData();
  form.append('file', new Blob([bytes], { type }), name);
  if (replaces) form.append('replaces', replaces);
  const response = await fetch(url, { method: 'POST', body: form, headers: { ...(token ? { Authorization: 'Bearer ' + token } : {}), ...headers } });
  const text = await response.text();
  let body = null; try { body = text ? JSON.parse(text) : null; } catch { body = text; }
  return { status: response.status, body };
}

module.exports = { synthetic, withExif, jpegSize, upload };
