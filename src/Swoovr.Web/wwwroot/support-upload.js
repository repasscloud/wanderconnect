// Issue 220 (D330): support files go from the browser straight to the BFF as multipart, so large photos, videos and
// documents never pass through WebAssembly memory as base64. The page keeps only names and sizes; the File objects stay
// here until sent, and a resend after an uncertain outcome sends the same files with the same operation key.
// Downloads are fetched and saved here for the same reason. Nothing is sent anywhere but this site's /bff/ routes.
window.swoovrSupport = (() => {
  'use strict';
  const picked = new Map();
  const list = id => (picked.get(id) || []).map(f => ({ name: f.name, size: f.size }));
  const headers = version => ({ 'X-Swoovr-Bff': '1', 'Swoovr-Version': version });
  return {
    // Adds the input's newly chosen files to the message's list and clears the input, so more can be chosen.
    take(id) {
      const input = document.getElementById(id);
      const files = picked.get(id) || [];
      for (const file of input?.files || []) files.push(file);
      picked.set(id, files);
      if (input) input.value = '';
      return list(id);
    },
    remove(id, index) {
      const files = picked.get(id) || [];
      if (index >= 0 && index < files.length) files.splice(index, 1);
      return list(id);
    },
    clear(id) { picked.delete(id); return []; },
    async send(url, fields, id, version) {
      const form = new FormData();
      for (const [key, value] of Object.entries(fields)) if (value !== null && value !== undefined) form.append(key, value);
      for (const file of picked.get(id) || []) form.append('files', file, file.name);
      try {
        const response = await fetch(url, { method: 'POST', body: form, credentials: 'same-origin', headers: headers(version) });
        return { status: response.status, body: await response.text() };
      } catch {
        return { status: 0, body: '' };
      }
    },
    async save(url, version) {
      try {
        const response = await fetch(url, { credentials: 'same-origin', headers: headers(version) });
        if (!response.ok) return response.status;
        const file = await response.json();
        const bytes = Uint8Array.from(atob(file.data), c => c.charCodeAt(0));
        const link = document.createElement('a');
        link.href = URL.createObjectURL(new Blob([bytes], { type: file.contentType || 'application/octet-stream' }));
        link.download = file.filename || 'attachment';
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
        return 200;
      } catch {
        return 0;
      }
    },
  };
})();
