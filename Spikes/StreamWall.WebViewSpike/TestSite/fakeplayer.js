// A stand-in for a typical HTML5 player library, reproducing the behaviors that matter to
// StreamWall's injected scripts:
// - wraps the <video> in a container that has a transform (breaks naive position:fixed)
// - writes inline styles on the video element
// - draws a control bar, poster and big play button over the video
// - restores its own saved volume shortly after loading (fights an injected volume)
// - can re-create the video element (player re-init)
window.FakePlayer = {
  mount(host, opts) {
    opts = opts || {};
    const wrap = document.createElement('div');
    wrap.className = 'fp-player';
    wrap.style.cssText = 'position:relative;width:' + (opts.width || 640) + 'px;height:' +
      (opts.height || 360) + 'px;background:#111;transform:translateZ(0);overflow:hidden;';
    host.appendChild(wrap);

    const make = () => {
      const v = document.createElement('video');
      v.className = 'fp-tech';
      v.setAttribute('playsinline', '');
      v.setAttribute('data-testid', 'video');
      v.autoplay = true;
      v.loop = true;
      v.muted = !!opts.muted;
      v.src = '/media/test-1080p.mp4';
      v.style.cssText = 'position:relative;margin:0;padding:0;width:100%;height:100%;' +
        'object-fit:contain;background-color:rgba(0,0,0,0);display:inline;';
      wrap.insertBefore(v, wrap.firstChild);
      // The library "restores" a saved volume after load.
      v.addEventListener('loadedmetadata', () => {
        setTimeout(() => { v.volume = 1.0; }, 500);
      });
      v.play().catch(() => {});
      return v;
    };

    let video = make();

    const bar = document.createElement('div');
    bar.className = 'fp-control-bar';
    bar.style.cssText = 'position:absolute;left:0;right:0;bottom:0;height:36px;' +
      'background:rgba(30,30,30,0.9);color:#ddd;font:13px sans-serif;padding:8px;';
    bar.textContent = 'PLAY  |  00:00  |  VOL  |  HD  |  FULL';
    wrap.appendChild(bar);

    const big = document.createElement('div');
    big.className = 'fp-big-play';
    big.style.cssText = 'position:absolute;left:45%;top:40%;width:60px;height:40px;' +
      'background:rgba(200,30,30,0.85);border-radius:6px;';
    wrap.appendChild(big);

    return {
      wrap,
      get video() { return video; },
      reinit() { video.remove(); video = make(); },
    };
  },
};
