// nav.js - Simple navigation toggle (no positioning hacks)
(function () {
    function init() {
        const nav = document.querySelector('.site-nav');
        if (!nav) return;

        // Toggle collapsed state
        const toggle = document.getElementById('nav-toggle');
        if (toggle) {
            toggle.addEventListener('click', () => {
                nav.classList.toggle('collapsed');
                const pressed = toggle.getAttribute('aria-pressed') === 'true';
                toggle.setAttribute('aria-pressed', String(!pressed));

                // Close open submenus when collapsing
                if (nav.classList.contains('collapsed')) {
                    closeAllSubmenus();
                }
            });
        }

        // Submenu toggle
        nav.querySelectorAll('.has-submenu').forEach(li => {
            const parentLink = li.querySelector('.parent-link');
            if (!parentLink) return;

            parentLink.addEventListener('click', e => {
                const href = parentLink.getAttribute('href') || '';
                if (href === '#' || href.trim() === '') {
                    e.preventDefault();

                    // Close other submenus
                    nav.querySelectorAll('.has-submenu.open').forEach(other => {
                        if (other !== li) {
                            other.classList.remove('open');
                            const otherLink = other.querySelector('.parent-link');
                            if (otherLink) otherLink.setAttribute('aria-expanded', 'false');
                        }
                    });

                    // Toggle this submenu
                    const isOpen = li.classList.toggle('open');
                    parentLink.setAttribute('aria-expanded', String(isOpen));
                }
            });
        });

        // Close submenus when clicking outside
        document.addEventListener('click', e => {
            if (!nav.contains(e.target)) {
                closeAllSubmenus();
            }
        });

        // Keyboard: Escape closes submenus
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') {
                closeAllSubmenus();
            }
        });

        function closeAllSubmenus() {
            nav.querySelectorAll('.has-submenu.open').forEach(li => {
                li.classList.remove('open');
                const link = li.querySelector('.parent-link');
                if (link) link.setAttribute('aria-expanded', 'false');
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
