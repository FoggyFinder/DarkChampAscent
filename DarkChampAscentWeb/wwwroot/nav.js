// nav.js - Sidebar navigation toggle and submenu handling
(function () {
    function init() {
        const nav = document.querySelector('.site-nav');
        if (!nav) return;

        // Position submenu overlay vertically aligned to parent when collapsed
        function positionSubmenuFor(li) {
            const submenu = li.querySelector('.submenu');
            if (!submenu) return;

            // Only position when collapsed (overlay to the right)
            if (!nav.classList.contains('collapsed')) {
                submenu.style.top = '';
                return;
            }

            // Temporarily show to measure if hidden
            const computed = getComputedStyle(submenu);
            const wasHidden = computed.display === 'none';
            if (wasHidden) {
                submenu.style.display = 'flex';
                submenu.style.visibility = 'hidden';
                submenu.style.pointerEvents = 'none';
            }

            const navRect = nav.getBoundingClientRect();
            const liRect = li.getBoundingClientRect();

            // Compute top relative to nav
            let top = liRect.top - navRect.top;

            // Constrain within nav bounds
            const navHeight = nav.clientHeight || navRect.height;
            const submenuHeight = submenu.offsetHeight || 0;
            const maxTop = Math.max(0, navHeight - submenuHeight);
            top = Math.min(Math.max(0, top), maxTop);

            submenu.style.top = top + 'px';

            // Restore if was hidden
            if (wasHidden) {
                submenu.style.display = '';
                submenu.style.visibility = '';
                submenu.style.pointerEvents = '';
            }
        }

        // Close all submenus
        function closeAllSubmenus() {
            nav.querySelectorAll('.has-submenu.open').forEach(li => {
                li.classList.remove('open');
                const link = li.querySelector('.parent-link');
                if (link) link.setAttribute('aria-expanded', 'false');
                const submenu = li.querySelector('.submenu');
                if (submenu) submenu.style.top = '';
            });
        }

        // Toggle button
        const toggle = document.getElementById('nav-toggle');
        if (toggle) {
            toggle.addEventListener('click', () => {
                const isCollapsed = nav.classList.toggle('collapsed');
                const pressed = toggle.getAttribute('aria-pressed') === 'true';
                toggle.setAttribute('aria-pressed', String(!pressed));

                if (isCollapsed) {
                    closeAllSubmenus();
                } else {
                    // Clear inline tops when expanding
                    nav.querySelectorAll('.has-submenu .submenu').forEach(s => {
                        s.style.top = '';
                    });
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
                            const pl = other.querySelector('.parent-link');
                            if (pl) pl.setAttribute('aria-expanded', 'false');
                            const submenu = other.querySelector('.submenu');
                            if (submenu) submenu.style.top = '';
                        }
                    });

                    // Toggle this submenu
                    const isOpen = li.classList.toggle('open');
                    parentLink.setAttribute('aria-expanded', String(isOpen));

                    if (isOpen) {
                        positionSubmenuFor(li);
                    } else {
                        const submenu = li.querySelector('.submenu');
                        if (submenu) submenu.style.top = '';
                    }
                }
            });

            // Reposition on hover when collapsed
            li.addEventListener('mouseenter', () => {
                if (nav.classList.contains('collapsed') && li.classList.contains('open')) {
                    positionSubmenuFor(li);
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

        // Reposition on resize/scroll for collapsed mode
        window.addEventListener('resize', () => {
            nav.querySelectorAll('.has-submenu.open').forEach(positionSubmenuFor);
        });

        window.addEventListener('scroll', () => {
            if (nav.classList.contains('collapsed')) {
                nav.querySelectorAll('.has-submenu.open').forEach(positionSubmenuFor);
            }
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
