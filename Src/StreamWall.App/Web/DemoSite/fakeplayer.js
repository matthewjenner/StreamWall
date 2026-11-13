// Demo-only stand-in for a typical HTML5 player library. The picture is a canvas animation
// captured as a MediaStream (no media files, no audio), so --demo needs nothing downloaded.
// It reproduces what matters to the injected scripts: autoplay muted, a wrapper with a transform, inline
// styles on the <video>, a control bar and big play button over it, a saved volume it restores
// after loading, and (optionally) re-creating its <video> element.
window.FakePlayer = {
  mount(host, opts) {
    opts = opts || {};
    const name = opts.name || 'Demo stream';
    const hue = opts.hue == null ? 210 : opts.hue;
    const wrap = document.createElement('div');
    wrap.className = 'fp-player';
    wrap.style.cssText = 'position:relative;width:' + (opts.width || 640) + 'px;height:' +
      (opts.height || 360) + 'px;background:#111;transform:translateZ(0);overflow:hidden;';
    host.appendChild(wrap);

    const canvas = document.createElement('canvas');
    canvas.width = 640;
    canvas.height = 360;
    const g = canvas.getContext('2d');
    const started = performance.now();
    const draw = () => {
      const t = (performance.now() - started) / 1000;
      g.fillStyle = 'hsl(' + hue + ',45%,22%)';
      g.fillRect(0, 0, 640, 360);
      g.fillStyle = 'hsl(' + hue + ',70%,60%)';
      g.fillRect(((t * 120) % 760) - 120, 0, 120, 360);
      g.fillStyle = '#fff';
      g.font = 'bold 44px sans-serif';
      g.fillText(name, 32, 80);
      g.font = '28px monospace';
      g.fillText(t.toFixed(1) + ' s', 32, 320);
      requestAnimationFrame(draw);
    };
    draw();
    const stream = canvas.captureStream(30);

    const make = () => {
      const v = document.createElement('video');
      v.className = 'fp-tech';
      v.setAttribute('playsinline', '');
      v.setAttribute('data-testid', 'video');
      v.autoplay = true;
      // Like real sites: autoplay starts muted (StreamWall must unmute it; found on a real site).
      v.muted = true;
      v.srcObject = stream;
      v.style.cssText = 'position:relative;margin:0;padding:0;width:100%;height:100%;' +
        'object-fit:contain;background-color:rgba(0,0,0,0);display:inline;';
      wrap.insertBefore(v, wrap.firstChild);
      v.addEventListener('loadedmetadata', () => { setTimeout(() => { v.volume = 1.0; }, 500); });
      v.play().catch(() => {});
      return v;
    };
    let video = make();

    const bar = document.createElement('div');
    bar.style.cssText = 'position:absolute;left:0;right:0;bottom:0;height:36px;' +
      'background:rgba(30,30,30,0.9);color:#ddd;font:13px sans-serif;padding:8px;';
    bar.textContent = 'PLAY  |  LIVE  |  VOL  |  HD  |  FULL';
    wrap.appendChild(bar);
    const big = document.createElement('div');
    big.style.cssText = 'position:absolute;left:45%;top:40%;width:60px;height:40px;' +
      'background:rgba(200,30,30,0.85);border-radius:6px;';
    wrap.appendChild(big);

    return {
      get video() { return video; },
      reinit() { video.remove(); video = make(); },
    };
  },
};
