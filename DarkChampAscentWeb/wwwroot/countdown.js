// countdown-ms.js
// Compact mm:ss countdown. Reloads page when it reaches zero.
// Place at /static/countdown-ms.js (or adjust the src in the view).

(function () {
  'use strict';

  function pad(n) {
    return String(n).padStart(2, '0');
  }

  // Parse an ISO-like timestamp, treating timezone-less strings as UTC.
  function parseTimestampAsUtcIfNeeded(ts) {
    if (!ts) return new Date(NaN);

    // If the string already contains 'Z' (UTC) or an explicit +/-hh:mm offset, use as-is.
    var hasZone = /(?:Z|[+\-]\d{2}:\d{2})$/i.test(ts);

    if (hasZone) {
      return new Date(ts);
    }

    // If it looks like an ISO date/time without a zone (e.g. "2026-01-04T12:34:56"),
    // assume it's UTC by appending 'Z'.
    // This avoids browser differences that may parse that form as local time.
    if (/^\d{4}-\d{2}-\d{2}T/.test(ts)) {
      // Helpful debug info for unexpected behavior
      if (console && console.warn) {
        console.warn('countdown-ms: timestamp missing timezone, assuming UTC:', ts);
      }
      return new Date(ts + 'Z');
    }

    // Fallback: let Date try to parse whatever it is
    return new Date(ts);
  }

  // Update one countdown element; returns true if time's up
  function updateOne(container) {
    var targetStr = container.dataset.target;
    if (!targetStr) return false;

    var target = parseTimestampAsUtcIfNeeded(targetStr);
    if (isNaN(target.getTime())) return false;

    var now = new Date();
    var diffMs = target.getTime() - now.getTime();

    if (diffMs <= 0) {
      // set final zero state for a short moment before reload
      var minsEl = container.querySelector('.countdown-unit.minutes');
      var secsEl = container.querySelector('.countdown-unit.seconds');
      if (minsEl) minsEl.textContent = '00';
      if (secsEl) secsEl.textContent = '00';
      return true;
    }

    var totalSec = Math.floor(diffMs / 1000);
    var minutes = Math.floor(totalSec / 60);
    var seconds = totalSec % 60;

    var minsEl = container.querySelector('.countdown-unit.minutes');
    var secsEl = container.querySelector('.countdown-unit.seconds');

    if (minsEl) minsEl.textContent = String(minutes).padStart(2, '0');
    if (secsEl) secsEl.textContent = pad(seconds);

    return false;
  }

  function init() {
    var containers = Array.from(document.querySelectorAll('.countdown-ms'));
    if (!containers.length) return;

    var ended = false;
    function tick() {
      var anyEnded = false;
      containers.forEach(function (c) {
        if (updateOne(c)) anyEnded = true;
      });

      if (anyEnded && !ended) {
        ended = true;
        // Small delay so UI shows 00:00 before reload
        setTimeout(function () {
          try {
            location.reload();
          } catch (e) {
            location.href = location.href;
          }
        }, 400);
      }
    }

    // initial render then interval
    tick();
    setInterval(tick, 500);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();