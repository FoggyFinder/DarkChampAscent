document.addEventListener('DOMContentLoaded', function () {
  // Only run if TomSelect is present
  if (typeof TomSelect === 'undefined') return;

  // Initialize only selects explicitly marked with class="tom-select"
  const selects = document.querySelectorAll('select.tom-select');

  selects.forEach((el) => {
    if (el.dataset.tomselect === 'false') return;

    // Defensive destroy of any previous instance
    if (el.tomselect && typeof el.tomselect.destroy === 'function') {
      try { el.tomselect.destroy(); } catch (err) { /* ignore */ }
    }

    const maxItems = el.multiple ? null : 1;

    try {
      const ts = new TomSelect(el, {
        maxItems: maxItems,
        allowEmptyOption: false,
        hideSelected: true,
        dropdownDirection: 'auto',
        render: {
          option: function (data, escape) {
            return '<div class="ts-option-nightshade">' + escape(data.text) + '</div>';
          },
          item: function (data, escape) {
            return '<div class="ts-item-nightshade">' + escape(data.text) + '</div>';
          }
        }
      });

      // Add a helper class to the produced wrapper/control so CSS can target it reliably
      try {
        if (ts.wrapper) ts.wrapper.classList.add('ts-nightshade');
        if (ts.control) ts.control.classList.add('ts-nightshade');
      } catch (e) {
        // ignore if internals differ
      }

      // Make the nearest .block allow visible overflow while this select exists (prevents clipping)
      const block = el.closest('.block');
      if (block) block.classList.add('ts-overflow');

      // ----- Append dropdown to body so it escapes stacking contexts -----
      try {
        // Find the dropdown element produced by TomSelect
        const dropdown = ts.wrapper && ts.wrapper.querySelector && ts.wrapper.querySelector('.ts-dropdown');
        if (dropdown) {
          // Move dropdown to body
          document.body.appendChild(dropdown);
          dropdown.classList.add('ts-nightshade-dropdown-body');

          // make absolutely positioned and high z-index
          dropdown.style.position = 'absolute';
          dropdown.style.zIndex = '2147483647';
          dropdown.style.boxSizing = 'border-box';

          // Positioning helper: align dropdown under/above the control when opened and on scroll/resize
          const positionDropdown = function () {
            try {
              const controlEl = (ts.control || ts.wrapper || el);
              const rect = controlEl.getBoundingClientRect();
              // Use the control width — force dropdown to match control width (avoid full-page stretch)
              const width = Math.max(rect.width, 160);
              dropdown.style.width = width + 'px';
              dropdown.style.minWidth = width + 'px';
              dropdown.style.left = rect.left + window.scrollX + 'px';

              // Measure dropdown height (may be zero if not rendered yet), use max-height fallback
              const estimatedHeight = dropdown.offsetHeight || Math.min(360, dropdown.scrollHeight || 320);

              // Flip above when it would overflow bottom of viewport
              const bottomSpace = window.innerHeight - rect.bottom;
              if (bottomSpace < estimatedHeight && rect.top > estimatedHeight) {
                // place above control
                dropdown.style.top = (rect.top + window.scrollY - estimatedHeight) + 'px';
              } else {
                // place below control
                dropdown.style.top = rect.bottom + window.scrollY + 'px';
              }

              // ensure box-sizing so width behaves predictably
              dropdown.style.boxSizing = 'border-box';
            } catch (err) {
              // ignore transient errors
            }
          };

          // Position whenever the dropdown opens
          if (typeof ts.on === 'function') {
            ts.on('dropdown_open', positionDropdown);
            ts.on('dropdown_close', function () {
              // TomSelect manages visibility classes — we leave the element in body
            });
          } else {
            // fallback
            dropdown.addEventListener('mouseenter', positionDropdown);
          }

          // Reposition on resize/scroll
          const repositionOnEvent = () => positionDropdown();
          window.addEventListener('resize', repositionOnEvent);
          window.addEventListener('scroll', repositionOnEvent, true);

          // If dropdown already visible, position it now
          positionDropdown();
        }
      } catch (err) {
        console.warn('Dropdown append-to-body failed (falling back to in-place):', err);
      }

      // mark initialized
      el.dataset.tomselectInitialized = 'true';
    } catch (e) {
      console.warn('TomSelect init failed for select:', el, e);
    }
  });
});