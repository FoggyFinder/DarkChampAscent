// nav.js - toggle collapsed sidebar and submenu behavior (fixes: initial sync & submenu collapsing)
// plus: position collapsed submenu overlays vertically aligned to parent item
(function () {
  // Initialize only after DOM is ready so we don't miss the nav element
  function init() {
    const nav = document.querySelector('.site-nav');
    if (!nav) return;

    const body = document.body;

    // Helper: update classes on <body> so CSS can shift main content
    function updateBodyNavState() {
      const isCollapsed = nav.classList.contains('collapsed');
      const wide = window.innerWidth > 720; // match CSS breakpoint
      // On wide screens: set open/collapsed classes accordingly
      body.classList.toggle('nav-side-open', !isCollapsed && wide);
      body.classList.toggle('nav-side-collapsed', isCollapsed && wide);
      // On narrow screens, remove any desktop nav classes (nav overlays)
      if (!wide) {
        body.classList.remove('nav-side-open', 'nav-side-collapsed');
      }
    }

    // Position a submenu overlay so it lines up vertically with its parent li when nav is collapsed
    function positionSubmenuFor(li) {
      const submenu = li.querySelector('.submenu');
      if (!submenu) return;

      // Only position when collapsed (collapsed overlay sits to the right)
      if (!nav.classList.contains('collapsed')) {
        submenu.style.top = '';
        return;
      }

      // If submenu is currently display:none, temporarily show it invisibly to measure height
      const computed = getComputedStyle(submenu);
      let needTempShow = computed.display === 'none';
      const prevDisplay = submenu.style.display || '';
      if (needTempShow) {
        submenu.style.display = 'flex';
        submenu.style.visibility = 'hidden';
        submenu.style.pointerEvents = 'none';
      }

      const navRect = nav.getBoundingClientRect();
      const liRect = li.getBoundingClientRect();

      // Compute top relative to nav
      let top = liRect.top - navRect.top;

      // Constrain so submenu stays within nav vertical bounds
      const navHeight = nav.clientHeight || navRect.height;
      const submenuHeight = submenu.offsetHeight || 0;
      const maxTop = Math.max(0, navHeight - submenuHeight);
      top = Math.min(Math.max(0, top), maxTop);

      submenu.style.top = top + 'px';

      // restore temporary styles if set
      if (needTempShow) {
        submenu.style.display = prevDisplay;
        submenu.style.visibility = '';
        submenu.style.pointerEvents = '';
      }
    }

    // Ensure each submenu is in sync with its aria-expanded (close if unspecified)
    function syncSubmenusFromAria() {
      nav.querySelectorAll('.has-submenu').forEach(function (li) {
        const parentLink = li.querySelector('.parent-link');
        const expanded = parentLink && parentLink.getAttribute('aria-expanded') === 'true';
        if (expanded) {
          li.classList.add('open');
          if (parentLink) parentLink.setAttribute('aria-expanded', 'true');
          // position overlay if needed
          positionSubmenuFor(li);
        } else {
          li.classList.remove('open');
          if (parentLink) parentLink.setAttribute('aria-expanded', 'false');
          // clear positioning
          const submenu = li.querySelector('.submenu');
          if (submenu) submenu.style.top = '';
        }
      });
    }

    // initial sync
    updateBodyNavState();
    syncSubmenusFromAria();

    const toggle = document.getElementById('nav-toggle');
    if (toggle) {
      toggle.addEventListener('click', () => {
        const isNowCollapsed = nav.classList.toggle('collapsed');
        const pressed = toggle.getAttribute('aria-pressed') === 'true';
        toggle.setAttribute('aria-pressed', String(!pressed));
        // When collapsing, close any open submenus so collapsed overlay won't leak them on some pages
        if (isNowCollapsed) {
          nav.querySelectorAll('.has-submenu.open').forEach(el => {
            el.classList.remove('open');
            const parentLink = el.querySelector('.parent-link');
            if (parentLink) parentLink.setAttribute('aria-expanded', 'false');
            const submenu = el.querySelector('.submenu');
            if (submenu) submenu.style.top = '';
          });
        } else {
          // if we just expanded the full nav, clear inline tops so CSS controls layout
          nav.querySelectorAll('.has-submenu .submenu').forEach(s => {
            s.style.top = '';
          });
        }
        // update body classes so CSS can respond
        updateBodyNavState();
      });
    }

    // Submenu toggle on click (also supports collapsed overlay)
    nav.querySelectorAll('.has-submenu').forEach(function (li) {
      const parent = li.querySelector('.parent-link');
      if (!parent) return;

      parent.addEventListener('click', function (e) {
        // if the parent link has a real href that should navigate (not "#"), allow navigation
        const href = parent.getAttribute('href') || '';
        const isHash = href === '#' || href.trim() === '';
        if (!isHash) {
          // allow navigation (don't intercept)
          return;
        }

        // prevent page jump when href="#" or toggling submenu
        e.preventDefault();

        // toggle open state on the li
        const isOpen = li.classList.toggle('open');
        parent.setAttribute('aria-expanded', String(isOpen));

        // Ensure only this submenu is open (close siblings) for predictability.
        if (isOpen) {
          nav.querySelectorAll('.has-submenu.open').forEach(other => {
            if (other !== li) {
              other.classList.remove('open');
              const pl = other.querySelector('.parent-link');
              if (pl) pl.setAttribute('aria-expanded', 'false');
              const submenu = other.querySelector('.submenu');
              if (submenu) submenu.style.top = '';
            }
          });
          // position the opened submenu
          positionSubmenuFor(li);
        } else {
          // closing, clear top
          const submenu = li.querySelector('.submenu');
          if (submenu) submenu.style.top = '';
        }
      });

      // When nav is collapsed, reposition overlay on mouseenter (keep alignment if user hovers)
      li.addEventListener('mouseenter', function () {
        if (nav.classList.contains('collapsed') && li.classList.contains('open')) {
          positionSubmenuFor(li);
        }
      });
    });

    // Close submenus when clicking outside nav
    document.addEventListener('click', function (e) {
      if (!nav.contains(e.target)) {
        nav.querySelectorAll('.has-submenu.open').forEach(el => {
          el.classList.remove('open');
          const parentLink = el.querySelector('.parent-link');
          if (parentLink) parentLink.setAttribute('aria-expanded', 'false');
          const submenu = el.querySelector('.submenu');
          if (submenu) submenu.style.top = '';
        });
      }
    });

    // keyboard accessibility: toggle with Enter/Space on parent-link when href="#" or no navigation
    nav.addEventListener('keydown', function (e) {
      const target = e.target;
      if ((e.key === 'Enter' || e.key === ' ') && target.classList.contains('parent-link')) {
        // only intercept if it's not a navigational link
        const href = target.getAttribute('href') || '';
        const isHash = href === '#' || href.trim() === '';
        if (isHash) {
          e.preventDefault();
          target.click();
        }
      }
      // Escape closes submenus
      if (e.key === 'Escape') {
        nav.querySelectorAll('.has-submenu.open').forEach(el => {
          el.classList.remove('open');
          const parentLink = el.querySelector('.parent-link');
          if (parentLink) parentLink.setAttribute('aria-expanded', 'false');
          const submenu = el.querySelector('.submenu');
          if (submenu) submenu.style.top = '';
        });
      }
    });

    // keep body classes in sync on resize (so mobile/desktop behavior matches)
    window.addEventListener('resize', function () {
      updateBodyNavState();
      // reposition any open overlays
      nav.querySelectorAll('.has-submenu.open').forEach(positionSubmenuFor);
    });

    // reposition on scroll (in case nav remains fixed and page scrolls)
    window.addEventListener('scroll', function () {
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