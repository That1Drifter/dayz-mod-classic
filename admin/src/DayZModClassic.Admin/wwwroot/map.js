// Self-contained canvas map for Chernarus (no external tiles). World coords run
// 0..worldSize on both axes; Arma Y is north, so canvas Y is flipped.
const DayzMap = (() => {
  let canvas, ctx, tooltip;
  let world = 15360;
  let markers = []; // {cx, cy, label, sub}
  let last = { players: [], vehicles: [] };
  let opts = { vehicles: true, dead: false };

  function init(canvasEl, tooltipEl) {
    canvas = canvasEl;
    ctx = canvas.getContext('2d');
    tooltip = tooltipEl;
    canvas.addEventListener('mousemove', onMove);
    canvas.addEventListener('mouseleave', () => tooltip.classList.add('hidden'));
  }

  function setOptions(o) { opts = { ...opts, ...o }; draw(); }

  function update(data) {
    world = data.worldSize || 15360;
    last.players = data.players || [];
    last.vehicles = data.vehicles || [];
    draw();
  }

  function toCanvas(x, y) {
    const W = canvas.width, H = canvas.height;
    return [x / world * W, H - (y / world * H)];
  }

  function draw() {
    if (!ctx) return;
    const W = canvas.width, H = canvas.height;
    ctx.clearRect(0, 0, W, H);
    ctx.fillStyle = '#10141a';
    ctx.fillRect(0, 0, W, H);

    // grid every ~2.56 km
    ctx.strokeStyle = '#1e2630';
    ctx.fillStyle = '#3a4654';
    ctx.font = '10px monospace';
    const step = 2560;
    for (let g = 0; g <= world; g += step) {
      const [gx] = toCanvas(g, 0);
      const [, gy] = toCanvas(0, g);
      ctx.beginPath(); ctx.moveTo(gx, 0); ctx.lineTo(gx, H); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(0, gy); ctx.lineTo(W, gy); ctx.stroke();
      ctx.fillText((g / 1000).toFixed(0) + 'k', gx + 2, 11);
      ctx.fillText((g / 1000).toFixed(0) + 'k', 2, gy - 2);
    }

    markers = [];

    if (opts.vehicles) {
      for (const v of last.vehicles) {
        const [cx, cy] = toCanvas(v.x, v.y);
        ctx.fillStyle = '#4a90d9';
        ctx.fillRect(cx - 3, cy - 3, 6, 6);
        markers.push({ cx, cy, label: v.classname || 'object', sub: `#${v.objectId} dmg ${v.damage ?? '?'}` });
      }
    }

    for (const p of last.players) {
      if (!p.alive && !opts.dead) continue;
      const [cx, cy] = toCanvas(p.x, p.y);
      ctx.beginPath();
      ctx.fillStyle = p.alive ? '#6ab04c' : '#d9534f';
      ctx.arc(cx, cy, 5, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = '#d8dee6';
      ctx.fillText(p.name, cx + 7, cy + 3);
      markers.push({ cx, cy, label: p.name, sub: `${Math.round(p.x)}, ${Math.round(p.y)}${p.alive ? '' : ' (dead)'}` });
    }
  }

  function onMove(e) {
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width, scaleY = canvas.height / rect.height;
    const mx = (e.clientX - rect.left) * scaleX, my = (e.clientY - rect.top) * scaleY;
    let hit = null;
    for (const m of markers) {
      if (Math.hypot(m.cx - mx, m.cy - my) < 8) { hit = m; break; }
    }
    if (hit) {
      tooltip.classList.remove('hidden');
      tooltip.style.left = (e.clientX + 12) + 'px';
      tooltip.style.top = (e.clientY + 12) + 'px';
      tooltip.innerHTML = `<b>${escapeHtml(hit.label)}</b><br>${escapeHtml(hit.sub)}`;
    } else {
      tooltip.classList.add('hidden');
    }
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }

  return { init, update, setOptions };
})();
