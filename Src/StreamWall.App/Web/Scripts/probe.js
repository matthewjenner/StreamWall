// StreamWall probe. Once a second, reports structural facts about this frame:
// counts, booleans and playback numbers. Never URLs, titles or page text.
(() => {
  if (window.__swProbe) return;
  window.__swProbe = true;
  const isTop = window === window.top;
  let raf = 0;
  const tick = () => { raf++; requestAnimationFrame(tick); };
  requestAnimationFrame(tick);

  let lastT = null, lastEl = null, played = 0;

  setInterval(() => {
    const v = window.__swTarget || null;
    if (v) {
      const t = v.currentTime;
      if (v === lastEl && lastT !== null) {
        if (t >= lastT) played += t - lastT;
        else played += t; // looped
      }
      lastT = t;
      lastEl = v;
    }
    const q = v && v.getVideoPlaybackQuality ? v.getVideoPlaybackQuality() : null;
    const m = {
      k: 'probe',
      top: isTop,
      vis: document.visibilityState,
      raf,
      videos: document.querySelectorAll('video').length,
      iframes: document.querySelectorAll('iframe').length,
      hasTarget: !!v,
      played,
      paused: v ? v.paused : null,
      vol: v ? v.volume : null,
      muted: v ? v.muted : null,
      dropped: q ? q.droppedVideoFrames : null,
      frames: q ? q.totalVideoFrames : null,
      vw: v ? v.videoWidth : null,
    };
    raf = 0;
    try { window.chrome.webview.postMessage(JSON.stringify(m)); } catch (e) { /* no bridge */ }
  }, 1000);
})();
