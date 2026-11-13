// StreamWall video-only + audio script. Runs in every frame at document creation.
// Picks the target (selector override, else the largest video, else in the top frame the
// largest iframe), leaves it where it is in the DOM, hides everything else, and pins the
// target to the viewport. Re-applies when the page swaps elements. Never reports URLs or text.
(() => {
  if (window.__swVideoOnly) return;
  window.__swVideoOnly = true;
  const cfg = window.__sw || {};
  const isTop = window === window.top;
  const MIN_AREA = 160 * 90;
  let target = null;
  let interactive = false;
  let reapplies = 0;
  const watched = new WeakSet();

  const post = (m) => {
    try { window.chrome.webview.postMessage(JSON.stringify(m)); } catch (e) { /* no bridge */ }
  };

  const CSS =
    'html.__sw-on, html.__sw-on body { background:#000 !important; overflow:hidden !important; }' +
    'html.__sw-on body * { visibility:hidden !important; }' +
    'html.__sw-on .__sw-anc { transform:none !important; filter:none !important; perspective:none !important;' +
    ' contain:none !important; will-change:auto !important; }' +
    'html.__sw-on .__sw-target { visibility:visible !important; position:fixed !important;' +
    ' left:0 !important; top:0 !important; right:auto !important; bottom:auto !important;' +
    ' width:100vw !important; height:100vh !important; max-width:none !important; max-height:none !important;' +
    ' margin:0 !important; padding:0 !important; border:0 !important; object-fit:contain !important;' +
    ' background:#000 !important; z-index:2147483647 !important; transform:none !important; }';

  function ensureStyle() {
    if (document.getElementById('__sw_style')) return;
    const s = document.createElement('style');
    s.id = '__sw_style';
    s.textContent = CSS;
    (document.head || document.documentElement).appendChild(s);
  }

  function area(el) {
    const r = el.getBoundingClientRect();
    return r.width * r.height;
  }

  // Measure the page as the site laid it out: with the takeover styling off, the current target
  // would otherwise measure as the whole viewport and lose to anything larger than the cell.
  function pick() {
    const html = document.documentElement;
    const on = html.classList.contains('__sw-on');
    if (on) html.classList.remove('__sw-on');
    try { return pickInLayout(); } finally { if (on) html.classList.add('__sw-on'); }
  }

  function pickInLayout() {
    if (cfg.selector) {
      const el = document.querySelector(cfg.selector);
      if (el) return el;
    }
    let best = null, bestArea = MIN_AREA - 1;
    for (const v of document.querySelectorAll('video')) {
      const a = area(v);
      if (a > bestArea) { best = v; bestArea = a; }
    }
    if (best || !isTop) return best;
    for (const f of document.querySelectorAll('iframe')) {
      const a = area(f);
      if (a > bestArea) { best = f; bestArea = a; }
    }
    return best;
  }

  function clearMarks() {
    document.querySelectorAll('.__sw-target, .__sw-anc').forEach((el) => {
      el.classList.remove('__sw-target', '__sw-anc');
    });
  }

  function applyAudio(v) {
    if (typeof cfg.volume === 'number' && Math.abs(v.volume - cfg.volume) > 0.001) {
      if (reapplies++ < 50) {
        v.volume = cfg.volume;
        post({ k: 'event', name: 'volume-reapplied', top: isTop });
      }
    }
  }

  function setTarget(el) {
    clearMarks();
    target = el;
    window.__swTarget = el && el.tagName === 'VIDEO' ? el : null;
    if (!el) return;
    el.classList.add('__sw-target');
    for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) p.classList.add('__sw-anc');
    if (el.tagName === 'VIDEO') {
      applyAudio(el);
      if (!watched.has(el)) {
        watched.add(el);
        el.addEventListener('volumechange', () => setTimeout(() => applyAudio(el), 0));
      }
      if (el.paused) el.play().catch(() => post({ k: 'event', name: 'autoplay-blocked', top: isTop }));
    }
    post({ k: 'event', name: 'target-picked', tag: el.tagName, top: isTop });
  }

  function apply() {
    if (interactive) return;
    ensureStyle();
    if (!target || !target.isConnected) {
      const el = pick();
      if (el !== target) setTarget(el);
    }
    // Only take over the frame when there is something to show.
    document.documentElement.classList.toggle('__sw-on', !!target);
  }

  window.__swSetInteractive = (on) => {
    interactive = !!on;
    if (interactive) document.documentElement.classList.remove('__sw-on');
    else { target = null; apply(); }
  };

  // Live changes from the host (posted to the page and to every frame): volume, interactive mode.
  try {
    window.chrome.webview.addEventListener('message', (e) => {
      const m = e.data;
      if (!m || typeof m !== 'object') return;
      if (m.k === 'volume' && typeof m.v === 'number') {
        cfg.volume = m.v;
        reapplies = 0;
        if (window.__swTarget) applyAudio(window.__swTarget);
      } else if (m.k === 'interactive') {
        window.__swSetInteractive(!!m.on);
      }
    });
  } catch (e) { /* no bridge in this frame */ }

  let scheduled = false;
  const schedule = () => {
    if (scheduled) return;
    scheduled = true;
    setTimeout(() => { scheduled = false; apply(); }, 50);
  };

  const start = () => {
    new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true });
    apply();
    // Layout-dependent picks (sizes) can change after load.
    setTimeout(() => { target = null; apply(); }, 1500);
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
