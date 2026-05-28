'use strict';

// ---------- helpers ----------
const $ = (s, r = document) => r.querySelector(s);
const $$ = (s, r = document) => [...r.querySelectorAll(s)];
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

async function api(path, opts = {}) {
  const res = await fetch(path, {
    headers: opts.body ? { 'Content-Type': 'application/json' } : undefined,
    ...opts,
  });
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!res.ok) {
    const msg = (data && data.error) || (typeof data === 'string' && data) || `HTTP ${res.status}`;
    throw new Error(msg);
  }
  return data;
}

let toastTimer;
function notify(msg, isErr = false) {
  let t = $('#toast');
  if (!t) {
    t = document.createElement('div');
    t.id = 'toast';
    t.style.cssText = 'position:fixed;bottom:18px;right:18px;padding:10px 16px;border-radius:6px;z-index:200;font-size:13px;max-width:380px;';
    document.body.appendChild(t);
  }
  t.style.background = isErr ? '#d9534f' : '#2d3a45';
  t.style.color = '#fff';
  t.style.border = isErr ? '1px solid #ff8a87' : '1px solid #4a90d9';
  t.textContent = msg;
  t.style.opacity = '1';
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .4s'; }, 3500);
}

async function run(fn) { try { await fn(); } catch (e) { notify(e.message, true); } }

// ---------- tabs ----------
$$('#tabs button').forEach(b => b.addEventListener('click', () => {
  $$('#tabs button').forEach(x => x.classList.remove('active'));
  $$('.tab').forEach(x => x.classList.remove('active'));
  b.classList.add('active');
  $('#tab-' + b.dataset.tab).classList.add('active');
  onTabShow(b.dataset.tab);
}));

function onTabShow(tab) {
  if (tab === 'players') loadPlayers();
  if (tab === 'bans') loadBans();
  if (tab === 'map') loadMap();
  if (tab === 'database') loadCharacters();
  if (tab === 'logs') loadLogs();
}

// ---------- status ----------
async function loadStatus() {
  let s;
  try { s = await api('/api/status'); }
  catch { setPill('#st-rcon', 'RCon offline', false); return; }

  setPill('#st-rcon', s.rcon.connected ? 'RCon up' : (s.rconConfigured ? 'RCon down' : 'RCon n/c'), s.rcon.connected);
  setPill('#st-hive', s.hiveOnline ? 'Hive up' : (s.hiveConfigured ? 'Hive down' : 'Hive n/c'), s.hiveOnline);
  setPill('#st-players', `Players ${s.rcon.playerCount}`, s.rcon.connected);

  const r = $('#st-restart');
  if (s.restart && s.restart.armed) {
    r.classList.remove('hidden');
    const m = Math.ceil((s.restart.secondsLeft || 0) / 60);
    r.textContent = `Restart in ${m}m`;
    r.className = 'pill bad';
  } else r.classList.add('hidden');

  $('#dash-rcon').textContent = s.rcon.connected ? 'Connected' : (s.rconConfigured ? 'Disconnected' : 'Not configured');
  $('#dash-players').textContent = s.rcon.playerCount;
  $('#dash-hive').textContent = s.hiveOnline ? 'Online' : (s.hiveConfigured ? 'Offline' : 'Not configured');
  $('#sched-status').textContent = (s.restart && s.restart.armed) ? `armed, ${Math.ceil((s.restart.secondsLeft || 0) / 60)}m left` : 'none armed';
}
function setPill(sel, text, ok) {
  const el = $(sel);
  el.textContent = text;
  el.className = 'pill ' + (ok ? 'ok' : 'bad');
}

async function loadChat() {
  try {
    const lines = await api('/api/chat?lines=120');
    $('#dash-chat').textContent = lines.map(l => `[${new Date(l.time).toLocaleTimeString()}] ${l.text}`).join('\n');
  } catch { /* ignore */ }
}

// ---------- players ----------
async function loadPlayers() {
  run(async () => {
    const list = await api('/api/players');
    const tb = $('#players-table tbody');
    tb.innerHTML = list.map(p => `
      <tr>
        <td>${p.id}</td>
        <td>${esc(p.name)}${p.inLobby ? ' <span class="muted">(lobby)</span>' : ''}</td>
        <td>${p.ping}</td>
        <td class="muted" title="${esc(p.guid)}">${esc(p.guid.slice(0, 10))}…</td>
        <td>${p.verified ? '✓' : '?'}</td>
        <td class="actions">
          <button class="small" data-act="say" data-id="${p.id}">Msg</button>
          <button class="small warn" data-act="kick" data-id="${p.id}">Kick</button>
          <button class="small danger" data-act="ban" data-id="${p.id}" data-guid="${esc(p.guid)}">Ban</button>
        </td>
      </tr>`).join('');
    $('#players-count').textContent = `${list.length} online`;
  });
}

$('#players-refresh').addEventListener('click', () => run(async () => { await api('/api/players/refresh', { method: 'POST' }); loadPlayers(); }));

$('#players-table').addEventListener('click', e => {
  const btn = e.target.closest('button[data-act]'); if (!btn) return;
  const id = btn.dataset.id, act = btn.dataset.act;
  if (act === 'kick') {
    const reason = prompt('Kick reason?', 'Kicked by admin'); if (reason === null) return;
    run(async () => { await api(`/api/players/${id}/kick`, { method: 'POST', body: JSON.stringify({ reason }) }); notify('Kicked'); setTimeout(loadPlayers, 800); });
  } else if (act === 'ban') {
    const reason = prompt('Ban reason?', 'Banned by admin'); if (reason === null) return;
    const mins = parseInt(prompt('Minutes (0 = permanent)?', '0') || '0', 10);
    run(async () => { await api(`/api/players/${id}/ban`, { method: 'POST', body: JSON.stringify({ minutes: mins, reason }) }); notify('Banned'); setTimeout(loadPlayers, 800); });
  } else if (act === 'say') {
    const message = prompt('Message to player?'); if (!message) return;
    run(async () => { await api(`/api/players/${id}/say`, { method: 'POST', body: JSON.stringify({ message }) }); notify('Sent'); });
  }
});

// ---------- bans ----------
async function loadBans() {
  run(async () => {
    const list = await api('/api/bans');
    $('#bans-table tbody').innerHTML = list.map(b => `
      <tr>
        <td>${b.index}</td><td>${b.type}</td><td class="muted">${esc(b.target)}</td>
        <td>${esc(b.minutesLeft)}</td><td>${esc(b.reason)}</td>
        <td class="actions"><button class="small danger" data-rm="${b.index}">Remove</button></td>
      </tr>`).join('');
  });
}
$('#bans-refresh').addEventListener('click', loadBans);
$('#ban-add').addEventListener('click', () => run(async () => {
  const guid = $('#ban-guid').value.trim(); if (!guid) return notify('GUID required', true);
  await api('/api/bans/add', { method: 'POST', body: JSON.stringify({ guid, minutes: parseInt($('#ban-mins').value || '0', 10), reason: $('#ban-reason').value }) });
  notify('Ban added'); loadBans();
}));
$('#bans-table').addEventListener('click', e => {
  const btn = e.target.closest('button[data-rm]'); if (!btn) return;
  if (!confirm('Remove ban #' + btn.dataset.rm + '?')) return;
  run(async () => { await api('/api/bans/' + btn.dataset.rm, { method: 'DELETE' }); notify('Removed'); loadBans(); });
});

// ---------- server ----------
$('#say-send').addEventListener('click', () => run(async () => {
  const message = $('#say-msg').value.trim(); if (!message) return;
  await api('/api/server/say', { method: 'POST', body: JSON.stringify({ message }) }); $('#say-msg').value = ''; notify('Broadcast sent');
}));
$('#srv-lock').addEventListener('click', () => srv('lock', 'Locked'));
$('#srv-unlock').addEventListener('click', () => srv('unlock', 'Unlocked'));
$('#srv-restart').addEventListener('click', () => { if (confirm('#restart the mission now?')) srv('restart', 'Restart sent'); });
$('#srv-shutdown').addEventListener('click', () => { if (confirm('#shutdown the server? Watchdog will relaunch it in ~30s.')) srv('shutdown', 'Shutdown sent'); });
function srv(action, ok) { run(async () => { await api('/api/server/' + action, { method: 'POST' }); notify(ok); }); }

$('#sched-arm').addEventListener('click', () => run(async () => {
  const m = parseInt($('#sched-mins').value || '0', 10); if (m <= 0) return notify('Minutes must be positive', true);
  await api('/api/server/schedule-restart', { method: 'POST', body: JSON.stringify({ inMinutes: m, warnMinutes: null }) });
  notify('Restart armed'); loadStatus();
}));
$('#sched-cancel').addEventListener('click', () => run(async () => { await api('/api/server/schedule-restart/cancel', { method: 'POST' }); notify('Cancelled'); loadStatus(); }));

$('#raw-send').addEventListener('click', () => run(async () => {
  const command = $('#raw-cmd').value.trim(); if (!command) return;
  const r = await api('/api/server/raw', { method: 'POST', body: JSON.stringify({ command }) });
  $('#raw-out').textContent = r.response || '(empty response)';
}));

// ---------- map ----------
DayzMap.init($('#map-canvas'), $('#map-tooltip'));
DayzMap.setBackground('sat');
$('#map-bg').addEventListener('change', e => DayzMap.setBackground(e.target.value));
async function loadMap() {
  run(async () => {
    const data = await api('/api/map');
    DayzMap.update(data);
    const age = data.snapshotTime ? Math.round((Date.now() - new Date(data.snapshotTime)) / 1000) : null;
    $('#map-status').textContent = `${data.players.length} players, ${data.vehicles.length} objects` + (age !== null ? ` · feed ${age}s old` : ' · no live feed');
  });
}
$('#map-refresh').addEventListener('click', loadMap);
$('#map-show-vehicles').addEventListener('change', e => DayzMap.setOptions({ vehicles: e.target.checked }));
$('#map-show-dead').addEventListener('change', e => DayzMap.setOptions({ dead: e.target.checked }));

// ---------- database ----------
$$('.subtabs button').forEach(b => b.addEventListener('click', () => {
  $$('.subtabs button').forEach(x => x.classList.remove('active'));
  $$('.db-pane').forEach(x => x.classList.remove('active'));
  b.classList.add('active');
  $('#db-' + b.dataset.db).classList.add('active');
  if (b.dataset.db === 'characters') loadCharacters(); else loadObjects();
}));

async function loadCharacters() {
  run(async () => {
    const q = new URLSearchParams();
    if ($('#char-search').value.trim()) q.set('search', $('#char-search').value.trim());
    if ($('#char-alive').checked) q.set('aliveOnly', 'true');
    const list = await api('/api/hive/characters?' + q.toString());
    $('#char-table tbody').innerHTML = list.map(c => `
      <tr>
        <td>${c.characterId}</td>
        <td>${esc(c.playerName || '')}</td>
        <td class="muted" title="${esc(c.playerUid)}">${esc((c.playerUid || '').slice(0, 12))}</td>
        <td>${c.alive ? '✓' : '✗'}</td>
        <td>${c.humanity}</td>
        <td>${c.hasPosition ? `${Math.round(c.x)}, ${Math.round(c.y)}` : '-'}</td>
        <td class="muted">${c.lastLogin ? new Date(c.lastLogin).toLocaleString() : ''}</td>
        <td class="actions"><button class="small" data-edit-char="${c.characterId}">Edit</button></td>
      </tr>`).join('');
  });
}
$('#char-refresh').addEventListener('click', loadCharacters);
$('#char-search').addEventListener('keydown', e => { if (e.key === 'Enter') loadCharacters(); });
$('#char-alive').addEventListener('change', loadCharacters);

$('#char-table').addEventListener('click', e => {
  const btn = e.target.closest('button[data-edit-char]'); if (!btn) return;
  run(async () => {
    const c = await api('/api/hive/characters/' + btn.dataset.editChar);
    openModal(`Character #${c.characterId} - ${esc(c.playerName || c.playerUid)}`, [
      field('alive', 'Alive', 'checkbox', c.alive),
      field('humanity', 'Humanity', 'number', c.humanity),
      field('model', 'Model', 'text', c.model),
      field('worldspace', 'Worldspace [dir,[x,y,z]]', 'text', c.worldspace),
      field('inventory', 'Inventory', 'textarea', c.inventory),
      field('backpack', 'Backpack', 'textarea', c.backpack),
      field('medical', 'Medical', 'textarea', c.medical),
    ], async () => {
      const body = {
        alive: $('#f-alive').checked,
        humanity: parseInt($('#f-humanity').value || '0', 10),
        model: $('#f-model').value,
        worldspace: $('#f-worldspace').value,
        inventory: $('#f-inventory').value,
        backpack: $('#f-backpack').value,
        medical: $('#f-medical').value,
      };
      await api('/api/hive/characters/' + c.characterId, { method: 'PUT', body: JSON.stringify(body) });
      notify('Character saved'); closeModal(); loadCharacters();
    });
  });
});

async function loadObjects() {
  run(async () => {
    const q = new URLSearchParams();
    if ($('#obj-filter').value.trim()) q.set('classname', $('#obj-filter').value.trim());
    const list = await api('/api/hive/objects?' + q.toString());
    $('#obj-table tbody').innerHTML = list.map(o => `
      <tr>
        <td>${o.objectId}</td>
        <td>${esc(o.classname || '')}</td>
        <td>${esc(o.damage ?? '')}</td>
        <td>${esc(o.fuel ?? '')}</td>
        <td>${o.hasPosition ? `${Math.round(o.x)}, ${Math.round(o.y)}` : '-'}</td>
        <td class="muted">${o.characterId ?? ''}</td>
        <td class="actions">
          <button class="small" data-edit-obj="${o.objectId}">Edit</button>
          <button class="small danger" data-del-obj="${o.objectId}">Del</button>
        </td>
      </tr>`).join('');
  });
}
$('#obj-refresh').addEventListener('click', loadObjects);
$('#obj-filter').addEventListener('keydown', e => { if (e.key === 'Enter') loadObjects(); });

$('#obj-table').addEventListener('click', e => {
  const ed = e.target.closest('button[data-edit-obj]');
  const del = e.target.closest('button[data-del-obj]');
  if (del) {
    if (!confirm('Delete object #' + del.dataset.delObj + '? (backed up first)')) return;
    return run(async () => { await api('/api/hive/objects/' + del.dataset.delObj, { method: 'DELETE' }); notify('Deleted'); loadObjects(); });
  }
  if (!ed) return;
  run(async () => {
    const o = await api('/api/hive/objects/' + ed.dataset.editObj);
    openModal(`Object #${o.objectId} - ${esc(o.classname || '')}`, [
      field('damage', 'Damage (0-1)', 'text', o.damage),
      field('fuel', 'Fuel (0-1)', 'text', o.fuel),
      field('worldspace', 'Worldspace', 'text', o.worldspace),
      field('inventory', 'Inventory', 'textarea', o.inventory),
    ], async () => {
      const body = { damage: $('#f-damage').value, fuel: $('#f-fuel').value, worldspace: $('#f-worldspace').value, inventory: $('#f-inventory').value };
      await api('/api/hive/objects/' + o.objectId, { method: 'PUT', body: JSON.stringify(body) });
      notify('Object saved'); closeModal(); loadObjects();
    });
  });
});

// ---------- modal ----------
function field(id, label, type, value) {
  if (type === 'checkbox') return `<div class="field"><label>${esc(label)}</label><input type="checkbox" id="f-${id}" ${value ? 'checked' : ''}></div>`;
  if (type === 'textarea') return `<div class="field"><label>${esc(label)}</label><textarea id="f-${id}">${esc(value ?? '')}</textarea></div>`;
  return `<div class="field"><label>${esc(label)}</label><input type="${type}" id="f-${id}" value="${esc(value ?? '')}"></div>`;
}
let modalSaveFn = null;
function openModal(title, fields, onSave) {
  $('#modal-title').innerHTML = title;
  $('#modal-body').innerHTML = fields.join('');
  modalSaveFn = onSave;
  $('#modal').classList.remove('hidden');
}
function closeModal() { $('#modal').classList.add('hidden'); modalSaveFn = null; }
$('#modal-save').addEventListener('click', () => modalSaveFn && run(modalSaveFn));
$('#modal-cancel').addEventListener('click', closeModal);

// ---------- logs ----------
async function loadLogs() {
  try {
    const n = parseInt($('#logs-lines').value || '300', 10);
    let lines = await api('/api/logs?lines=' + n);
    const f = $('#logs-filter').value.trim().toLowerCase();
    if (f) lines = lines.filter(l => l.toLowerCase().includes(f));
    const out = $('#logs-out');
    const atBottom = out.scrollTop + out.clientHeight >= out.scrollHeight - 30;
    out.textContent = lines.join('\n');
    if (atBottom) out.scrollTop = out.scrollHeight;
  } catch (e) { $('#logs-out').textContent = e.message; }
}
$('#logs-refresh').addEventListener('click', loadLogs);
$('#logs-filter').addEventListener('input', loadLogs);

// ---------- timers ----------
loadStatus(); loadChat();
setInterval(loadStatus, 5000);
setInterval(loadChat, 6000);
setInterval(() => {
  const active = $('#tabs button.active').dataset.tab;
  if (active === 'players') loadPlayers();
  if (active === 'map') loadMap();
  if (active === 'logs' && $('#logs-auto').checked) loadLogs();
}, 10000);
