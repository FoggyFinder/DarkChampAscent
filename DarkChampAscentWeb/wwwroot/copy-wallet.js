// Simple copy-to-clipboard behavior.
// Include this file in your page (e.g. <script src=\"/js/copy-wallet.js\"></script>).
document.addEventListener('click', function (e) {
  // find a button or element that either has .copy-btn or data-copy attr
  const btn = e.target.closest('.copy-btn, [data-copy]');
  if (!btn) return;

  // prefer explicit data-copy attribute, fall back to a nearby data-wallet
  const value =
    btn.getAttribute('data-copy') ||
    (btn.closest('[data-wallet]') && btn.closest('[data-wallet]').getAttribute('data-wallet')) ||
    btn.textContent;

  if (!value) return;

  // navigator.clipboard is preferred; fallback to prompt
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(value).then(function () {
      // visual confirmation: swap icon/content for a tick briefly
      const original = btn.innerHTML;
      btn.innerHTML = '✓';
      setTimeout(function () { btn.innerHTML = original; }, 1200);
    }).catch(function () {
      window.prompt('Copy to clipboard: Ctrl+C, Enter', value);
    });
  } else {
    window.prompt('Copy to clipboard: Ctrl+C, Enter', value);
  }
});