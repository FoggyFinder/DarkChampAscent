// custom-select.js - Simple TomSelect initialization (no positioning hacks)
document.addEventListener('DOMContentLoaded', function () {
    if (typeof TomSelect === 'undefined') return;

    document.querySelectorAll('select.tom-select').forEach(el => {
        if (el.dataset.tomselect === 'false') return;
        if (el.tomselect) return; // Already initialized

        try {
            new TomSelect(el, {
                maxItems: el.multiple ? null : 1,
                allowEmptyOption: false,
                hideSelected: true,
                render: {
                    option: (data, escape) => `<div class="ts-option">${escape(data.text)}</div>`,
                    item: (data, escape) => `<div class="ts-item">${escape(data.text)}</div>`
                }
            });
        } catch (e) {
            console.warn('TomSelect init failed:', el, e);
        }
    });
});
