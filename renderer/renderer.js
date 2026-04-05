'use strict';
const api = window.bridge;
let cfg = {};
let running = false;

// ── Init ────────────────────────────────────────────────────────────────────
async function init() {
  cfg = await api.loadConfig();
  await refreshPorts();
  applyConfig(cfg);
  buildGrid();
  bindEvents();
  listenBridge();

  running = await api.isRunning();
  document.getElementById('btn-start').disabled = running;
  document.getElementById('btn-stop').disabled  = !running;

  log('UI24R Bridge ready', 'info');
}

// ── Config ──────────────────────────────────────────────────────────────────
function applyConfig(c) {
  set('cfg-url',   c['UI24R-Url'] || '');
  set('s-rec',     c['DefaultChannelRecButton'] || 'rec');
  set('s-main-rec',c['DefaultRecButton'] || '2TrackAndMTK');
  set('s-aux',     c['AuxButtonBehavior'] || 'Release');
  set('s-startbank', c['StartBank'] || '0');
  set('s-talkback',  c['TalkBack'] || '');
  set('s-rta',     c['RtaOnWhenSelect'] || 'false');
  set('s-pstart',  c['PrimaryChannelStart'] || '0');
  set('s-sstart',  c['SecondaryChannelStart'] || '1');
  set('s-debug',   c['DebugMessages'] || 'false');
  set('s-syncid',  c['SyncID'] || '');
  setSel('cfg-in',  c['MIDI-Input-Name']);
  setSel('cfg-out', c['MIDI-Output-Name']);
  setSel('cfg-in2', c['MIDI-Input-Name-Second'] || '');
  setSel('cfg-out2',c['MIDI-Output-Name-Second'] || '');
  set('cfg-ext2',  c['SecondaryIsExtender'] || 'true');
}

function collectConfig() {
  return Object.assign({}, cfg, {
    'UI24R-Url':               val('cfg-url'),
    'MIDI-Input-Name':         val('cfg-in'),
    'MIDI-Output-Name':        val('cfg-out'),
    'MIDI-Input-Name-Second':  val('cfg-in2'),
    'MIDI-Output-Name-Second': val('cfg-out2'),
    'SecondaryIsExtender':     val('cfg-ext2'),
    'DefaultChannelRecButton': val('s-rec'),
    'DefaultRecButton':        val('s-main-rec'),
    'AuxButtonBehavior':       val('s-aux'),
    'StartBank':               val('s-startbank'),
    'TalkBack':                val('s-talkback'),
    'RtaOnWhenSelect':         val('s-rta'),
    'PrimaryChannelStart':     val('s-pstart'),
    'SecondaryChannelStart':   val('s-sstart'),
    'DebugMessages':           val('s-debug'),
    'SyncID':                  val('s-syncid'),
  });
}

// ── MIDI ports ───────────────────────────────────────────────────────────────
async function refreshPorts() {
  const { inputs, outputs } = await api.listMidi();
  const inp = inputs,  out = outputs;
  fillSel('cfg-in',  inp, true);
  fillSel('cfg-out', out, true);
  fillSel('cfg-in2', inp, true, true);
  fillSel('cfg-out2',out, true, true);
  setSel('cfg-in',  cfg['MIDI-Input-Name']);
  setSel('cfg-out', cfg['MIDI-Output-Name']);
  setSel('cfg-in2', cfg['MIDI-Input-Name-Second'] || '');
  setSel('cfg-out2',cfg['MIDI-Output-Name-Second'] || '');
}

function fillSel(id, items, clear=true, withNone=false) {
  const el = document.getElementById(id);
  if (clear) el.innerHTML = '';
  if (withNone) {
    const o = document.createElement('option');
    o.value = ''; o.textContent = '— none —'; el.appendChild(o);
  }
  items.forEach(name => {
    const o = document.createElement('option');
    o.value = name; o.textContent = name; el.appendChild(o);
  });
}

// ── Channel grid ─────────────────────────────────────────────────────────────
function buildGrid() {
  const g = document.getElementById('ch-grid');
  g.innerHTML = '';
  for (let i = 0; i < 8; i++) {
    g.innerHTML += `
    <div class="strip" id="strip-${i}">
      <div class="strip-num">${i + 1}</div>
      <div class="strip-name" id="sn-${i}">—</div>
      <div class="strip-path" id="sp-${i}"></div>
      <div class="strip-bar"><div class="strip-fill" id="sf-${i}" style="width:0%"></div></div>
      <div class="strip-icons" id="si-${i}"></div>
    </div>`;
  }
}

// ── Bridge events ─────────────────────────────────────────────────────────────
function listenBridge() {
  api.onStatus(s => {
    setDot('d-midi',  s.midi  === 'connected' ? 'ok' : 'err');
    setDot('d-mixer', s.mixer === 'connected' ? 'ok' : 'err');
    if (s.midi2) {
      document.getElementById('pill-midi2').style.display = '';
      setDot('d-midi2', s.midi2 === 'connected' ? 'ok' : 'err');
    }
  });

  api.onLog(msg => log(msg, 'info'));

  api.onStopped(() => {
    running = false;
    document.getElementById('btn-start').disabled = false;
    document.getElementById('btn-stop').disabled  = true;
    setDot('d-midi',  'err');
    setDot('d-midi2', 'err');
    setDot('d-mixer', 'err');
    log('Bridge stopped — mixer unreachable', 'warn');
  });

  api.onState(state => {
    updateBankBar(state);
    updateModeBar(state);
    updateGrid(state);
    const bk = state.bank;
    const ly = state.layer + 1;
    document.getElementById('pill-bank').textContent = `Bank ${bk} · L${ly}`;
  });
}

function setDot(id, cls) {
  const el = document.getElementById(id);
  if (el) el.className = `dot ${cls}`;
}

// ── State UI ─────────────────────────────────────────────────────────────────
function updateBankBar(state) {
  ['I','U','V'].forEach(b =>
    document.getElementById(`bk-${b}`).className = 'badge' + (state.bank === b ? ' on' : ''));
  for (let l = 0; l < 6; l++)
    document.getElementById(`ly-${l}`).className = 'badge' + (state.layer === l ? ' on' : '');
}

function updateModeBar(state) {
  const ca = document.getElementById('chip-aux');
  const cf = document.getElementById('chip-fx');
  const cp = document.getElementById('chip-pan');
  const cs = document.getElementById('chip-sel');

  ca.className = 'chip' + (state.auxMode !== null ? ' on' : '');
  ca.textContent = state.auxMode !== null ? `AUX ${state.auxMode + 1}` : 'AUX —';

  cf.className = 'chip' + (state.fxMode !== null ? ' on' : '');
  cf.textContent = state.fxMode !== null ? `FX ${state.fxMode + 1}` : 'FX —';

  cp.className = 'chip' + (state.panMode ? ' on' : '');

  cs.textContent = state.selected ? state.selected : '—';
}

function updateGrid(state) {
  const details = state.channelDetails || [];
  const channels = state.primaryChannels || [];

  for (let i = 0; i < 8; i++) {
    const strip  = document.getElementById(`strip-${i}`);
    const detail = details[i] || {};
    const chPath = channels[i] || '';

    // Classes
    let cls = 'strip';
    if (detail.mute)   cls += ' muted';
    if (detail.solo)   cls += ' soloed';
    if (chPath === state.selected) cls += ' sel';
    if (detail.recArm || detail.phantom) cls += ' armed';
    strip.className = cls;

    // Name / path
    document.getElementById(`sn-${i}`).textContent = detail.name || chPath || '—';
    document.getElementById(`sp-${i}`).textContent = chPath;

    // Fader level bar
    const level = detail.fader || 0;
    document.getElementById(`sf-${i}`).style.width = `${Math.round(level * 100)}%`;

    // Status icons
    const icons = [];
    if (detail.mute)    icons.push('<span class="icon m">M</span>');
    if (detail.solo)    icons.push('<span class="icon s">S</span>');
    if (detail.recArm)  icons.push('<span class="icon r">R</span>');
    if (detail.phantom) icons.push('<span class="icon p">48V</span>');
    document.getElementById(`si-${i}`).innerHTML = icons.join('');
  }
}

// ── Log ──────────────────────────────────────────────────────────────────────
function log(msg, level='info') {
  const ts = new Date().toLocaleTimeString('en', { hour12: false });
  const html = `<div class="le ${level}"><span class="ts">${ts}</span>${esc(msg)}</div>`;

  // Full log pane
  const full = document.getElementById('pane-log');
  full.insertAdjacentHTML('beforeend', html);
  while (full.children.length > 500) full.removeChild(full.firstChild);
  full.scrollTop = full.scrollHeight;

  // Live preview log
  const live = document.getElementById('pane-log-live');
  live.insertAdjacentHTML('beforeend', html.replace('class="le', 'class="le'));
  while (live.children.length > 40) live.removeChild(live.firstChild);
  live.scrollTop = live.scrollHeight;
}

function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ── Events ───────────────────────────────────────────────────────────────────
function bindEvents() {
  // Tabs
  document.querySelectorAll('.tab').forEach(tab => {
    tab.addEventListener('click', () => {
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.pane').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById(`pane-${tab.dataset.tab}`).classList.add('active');
    });
  });

  document.getElementById('btn-start').addEventListener('click', async () => {
    cfg = collectConfig();
    await api.saveConfig(cfg);
    const r = await api.start();
    if (r.ok) {
      running = true;
      document.getElementById('btn-start').disabled = true;
      document.getElementById('btn-stop').disabled  = false;
      log('Bridge started', 'info');
    }
  });

  document.getElementById('btn-stop').addEventListener('click', async () => {
    await api.stop();
    running = false;
    document.getElementById('btn-start').disabled = false;
    document.getElementById('btn-stop').disabled  = true;
    setDot('d-midi',  'err');
    setDot('d-midi2', 'err');
    setDot('d-mixer', 'err');
    log('Bridge stopped', 'warn');
  });

  document.getElementById('btn-refresh').addEventListener('click', async () => {
    await refreshPorts();
    log('MIDI ports refreshed');
  });

  document.getElementById('btn-save-settings').addEventListener('click', async () => {
    cfg = collectConfig();
    await api.saveConfig(cfg);
    log('Settings saved', 'info');
  });
}

// ── Helpers ──────────────────────────────────────────────────────────────────
const val = (id) => document.getElementById(id)?.value || '';
const set = (id, v) => { const el = document.getElementById(id); if (el) el.value = v; };
function setSel(id, v) {
  const el = document.getElementById(id);
  if (!el || !v) return;
  for (const o of el.options) if (o.value === v) { el.value = v; return; }
}

init().catch(e => log(e.message, 'err'));
