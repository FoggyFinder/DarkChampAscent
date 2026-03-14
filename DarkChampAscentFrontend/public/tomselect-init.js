function initTomSelects() {
  if (typeof TomSelect === 'undefined') return;

  const selects = document.querySelectorAll('select.tom-select:not([data-tomselect-initialized])');

  selects.forEach((el) => {
    if (el.dataset.tomselect === 'false') return;

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
        controlInput: null,
        render: {
          option: function (data, escape) {
            return '<div class="ts-option-nightshade">' + escape(data.text) + '</div>';
          },
          item: function (data, escape) {
            return '<div class="ts-item-nightshade">' + escape(data.text) + '</div>';
          }
        }
      });

      try {
        if (ts.wrapper) ts.wrapper.classList.add('ts-nightshade');
        if (ts.control) ts.control.classList.add('ts-nightshade');
      } catch (e) { /* ignore */ }

      const block = el.closest('.block');
      if (block) block.classList.add('ts-overflow');

      try {
        const dropdown = ts.wrapper && ts.wrapper.querySelector && ts.wrapper.querySelector('.ts-dropdown');
        if (dropdown) {
          document.body.appendChild(dropdown);
          dropdown.classList.add('ts-nightshade-dropdown-body');

          dropdown.style.position = 'absolute';
          dropdown.style.zIndex = '2147483647';
          dropdown.style.boxSizing = 'border-box';

          const positionDropdown = function () {
            try {
              const controlEl = (ts.control || ts.wrapper || el);
              const rect = controlEl.getBoundingClientRect();
              const width = Math.max(rect.width, 160);
              dropdown.style.width = width + 'px';
              dropdown.style.minWidth = width + 'px';
              dropdown.style.left = rect.left + window.scrollX + 'px';

              const estimatedHeight = dropdown.offsetHeight || Math.min(360, dropdown.scrollHeight || 320);
              const bottomSpace = window.innerHeight - rect.bottom;
              if (bottomSpace < estimatedHeight && rect.top > estimatedHeight) {
                dropdown.style.top = (rect.top + window.scrollY - estimatedHeight) + 'px';
              } else {
                dropdown.style.top = rect.bottom + window.scrollY + 'px';
              }

              dropdown.style.boxSizing = 'border-box';
            } catch (err) { /* ignore */ }
          };

          if (typeof ts.on === 'function') {
            ts.on('dropdown_open', positionDropdown);
          } else {
            dropdown.addEventListener('mouseenter', positionDropdown);
          }

          window.addEventListener('resize', positionDropdown);
          window.addEventListener('scroll', positionDropdown, true);

          positionDropdown();
        }
      } catch (err) {
        console.warn('Dropdown append-to-body failed:', err);
      }

      el.dataset.tomselectInitialized = 'true';
    } catch (e) {
      console.warn('TomSelect init failed for select:', el, e);
    }
  });
}

// Watch for React adding new selects to the DOM
const observer = new MutationObserver(() => initTomSelects());
observer.observe(document.body, { childList: true, subtree: true });

// Also run immediately in case anything already exists
initTomSelects();