// Server validation remains authoritative. Focus its first invalid field after the new document is ready.
const invalid = document.querySelector('input[aria-invalid="true"], select[aria-invalid="true"], textarea[aria-invalid="true"]');
if (invalid) {
  const container = invalid.closest('.pf-v5-c-form__group');
  const error = container?.querySelector('[id^="input-error-"]');
  if (error?.textContent.trim()) {
    const descriptions = new Set((invalid.getAttribute('aria-describedby') || '').split(/\s+/).filter(Boolean));
    descriptions.add(error.id);
    invalid.setAttribute('aria-describedby', [...descriptions].join(' '));
  }
  invalid.focus();
}

// D300: the server enforces the same surname/one-name condition; this only explains native validity.
(() => {
  const toggle = document.getElementById('swoovr-mononym');
  const surname = document.querySelector('#kc-register-form [name="lastName"]');
  if (!toggle || !surname) return;
  const update = () => { surname.required = !toggle.checked; surname.disabled = toggle.checked; };
  toggle.addEventListener('change', update); update();
})();
