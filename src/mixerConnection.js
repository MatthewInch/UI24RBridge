'use strict';

const { EventEmitter } = require('events');
const net = require('net');

/**
 * MixerConnection
 *
 * Plain TCP client for the Soundcraft UI24R.
 *
 * UI24R protocol overview:
 *  - Connect to TCP port 80 of the mixer IP.
 *  - Send "GET /raw HTTP1.1\n\n" — the server replies with an HTTP response
 *    header and then the socket becomes a plain newline-delimited text stream.
 *  - Send "INIT\n" once to receive all current mixer state.
 *  - Send "ALIVE\n" every second — if the mixer doesn't receive it for ~5s
 *    it stops sending updates.
 *  - All messages (both directions) are newline-terminated plain text.
 *  - Set a numeric value:  "SETD^<path>^<value>\n"
 *  - Set a string value:   "SETS^<path>^<value>\n"
 *  - State updates arrive: "SETD^<path>^<value>"
 *
 * Events emitted:
 *  'connected'
 *  'disconnected'
 *  'failed'          – gave up after maxRetries
 *  'channelLevel'    (path, level)
 *  'channelMute'     (path, muted)
 *  'channelSolo'     (path, soloed)
 *  'channelName'     (path, name)
 *  'channelGain'     (path, gain)
 *  'auxLevel'        (chPath, auxIdx, level)
 *  'fxLevel'         (chPath, fxIdx, level)
 *  'meter'           (data)
 *  'phantom'         (path, on)
 *  'recArm'          (path, armed)
 *  'muteGroup'       (groupIdx, active)
 *  'raw'             (msg)
 */
class MixerConnection extends EventEmitter {
  constructor(url, debug = false) {
    super();
    this.url     = url;
    this.debug   = debug;

    this._socket          = null;
    this._connected       = false;
    this._reconnectTimer  = null;
    this._aliveTimer      = null;
    this._reconnectDelay  = 3000;
    this._maxReconnectDelay = 30000;
    this._maxRetries      = 10;
    this._retryCount      = 0;
    this._shouldReconnect = true;

    this._buf      = '';    // raw incoming data buffer
    this._httpDone = false; // true once HTTP response header has been consumed

    this._state = {};       // cached mixer state: path → value
  }

  // ─── Lifecycle ──────────────────────────────────────────────────────────────

  connect() {
    this._shouldReconnect = true;
    this._retryCount = 0;
    this._doConnect();
  }

  disconnect() {
    this._shouldReconnect = false;
    clearTimeout(this._reconnectTimer);
    this._stopAlive();
    if (this._socket) {
      this._socket.destroy();
      this._socket = null;
    }
    this._connected = false;
  }

  get connected() { return this._connected; }

  // ─── Send ────────────────────────────────────────────────────────────────────

  /** Set a numeric parameter */
  setParam(path, value) {
    // Update local state and dispatch immediately so the controller LED and
    // state cache reflect the change without waiting for the mixer's echo.
    const strVal = String(value);
    this._state[path] = strVal;
    this._dispatchStateUpdate(path, strVal);
    this._send(`SETD^${path}^${value}`);
  }

  /** Set a string parameter */
  setString(path, value) {
    const strVal = String(value);
    this._state[path] = strVal;
    this._dispatchStateUpdate(path, strVal);
    this._send(`SETS^${path}^${value}`);
  }

  setFaderLevel(channelPath, level) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.mix`, level);
  }

  setMute(channelPath, muted) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.mute`, muted ? 1 : 0);
  }

  toggleMute(channelPath) {
    const current = this.getState(`${channelPath}.mute`);
    this.setMute(channelPath, !current);
  }

  setSolo(channelPath, soloed) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.solo`, soloed ? 1 : 0);
  }

  toggleSolo(channelPath) {
    const current = this.getState(`${channelPath}.solo`);
    this.setSolo(channelPath, !current);
  }

  setGain(channelPath, value) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.gain`, value);
  }

  setAuxSend(channelPath, auxIdx, level) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.aux.${auxIdx}.value`, level);
  }

  setAuxSendOn(channelPath, auxIdx, on) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.aux.${auxIdx}.mute`, on ? 0 : 1);
  }

  setFxSend(channelPath, fxIdx, level) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.fx.${fxIdx}.value`, level);
  }

  setPhantom(channelPath, on) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.phantom`, on ? 1 : 0);
  }

  togglePhantom(channelPath) {
    const current = this.getState(`${channelPath}.phantom`);
    this.setPhantom(channelPath, !current);
  }

  setRecArm(channelPath, armed) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.mtkarm`, armed ? 1 : 0);
  }

  toggleRecArm(channelPath) {
    const current = this.getState(`${channelPath}.mtkarm`);
    this.setRecArm(channelPath, !current);
  }

  setMtkRecord(recording)    { this.setParam('mtk.rec',    recording ? 1 : 0); }
  set2TrackRecord(recording) { this.setParam('2tr.rec',    recording ? 1 : 0); }

  mediaPlay()    { this._send('MEDIA_PLAY'); }
  mediaStop()    { this._send('MEDIA_STOP'); }
  mediaRewind()  { this._send('MEDIA_PREV'); }
  mediaForward() { this._send('MEDIA_NEXT'); }

  tapTempo()   { this.setParam('var.tap', 1); }
  clearSolo()  { this.setParam('var.clearsolo', 1); }
  muteAll()    { this.setParam('var.muteall', 1); }
  clearMute()  { this.setParam('var.clearmute', 1); }
  muteFx()     { this.setParam('var.mutefx', 1); }

  setMuteGroup(groupIdx, active) {
    this.setParam(`mg.${groupIdx}.mute`, active ? 1 : 0);
  }

  toggleMuteGroup(groupIdx) {
    const current = this.getState(`mg.${groupIdx}.mute`);
    this.setMuteGroup(groupIdx, !current);
  }

  setRta(channelPath, on) {
    if (!channelPath) return;
    this.setParam(`${channelPath}.rta`, on ? 1 : 0);
  }

  // ─── State cache ─────────────────────────────────────────────────────────────

  getState(path) {
    const v = this._state[path];
    if (v === undefined) return 0;
    return parseFloat(v) || 0;
  }

  getStateRaw(path) {
    return this._state[path];
  }

  // ─── Internal ────────────────────────────────────────────────────────────────

  _parseUrl() {
    // Accept ws://host, ws://host:port, http://host, or bare host/ip
    try {
      const u = new URL(this.url.replace(/^ws/, 'http'));
      return { host: u.hostname, port: parseInt(u.port) || 80 };
    } catch {
      return { host: this.url, port: 80 };
    }
  }

  _doConnect() {
    const { host, port } = this._parseUrl();
    this._log(`Connecting to ${host}:${port}...`);

    this._buf      = '';
    this._httpDone = false;

    try {
      const socket = net.createConnection({ host, port });
      this._socket = socket;

      socket.setTimeout(10000);

      socket.on('connect', () => {
        socket.setTimeout(0);
        // Protocol: send plain HTTP GET — server switches to raw socket mode
        socket.write('GET /raw HTTP1.1\n\n');
      });

      socket.on('data', (chunk) => {
        this._buf += chunk.toString();

        if (!this._httpDone) {
          // Consume HTTP response header (ends with \r\n\r\n or \n\n)
          const end = this._findHeaderEnd(this._buf);
          if (end === -1) return; // header not complete yet
          this._buf      = this._buf.slice(end);
          this._httpDone = true;

          this._connected = true;
          this._retryCount = 0;
          socket.setKeepAlive(true, 3000);
          this._log('Connected to mixer');
          this.emit('connected');
          this._startAlive();
          this._send('INIT');
        }

        this._processBuffer();
      });

      socket.on('close', () => {
        this._connected = false;
        this._socket    = null;
        this._stopAlive();
        this._log('Disconnected from mixer');
        this.emit('disconnected');
        this._scheduleReconnect();
      });

      socket.on('timeout', () => {
        this._log('Connection timed out');
        socket.destroy();
      });

      socket.on('error', (err) => {
        this._log('Socket error:', err.message);
        // 'close' event follows automatically
      });

    } catch (err) {
      this._log('Connection error:', err.message);
      this._scheduleReconnect();
    }
  }

  /** Find the byte offset immediately after the HTTP response header. */
  _findHeaderEnd(buf) {
    // Standard HTTP uses \r\n\r\n; mixer may use \n\n
    const crLf = buf.indexOf('\r\n\r\n');
    if (crLf !== -1) return crLf + 4;
    const lf = buf.indexOf('\n\n');
    if (lf !== -1) return lf + 2;
    return -1;
  }

  _processBuffer() {
    let nl;
    while ((nl = this._buf.indexOf('\n')) !== -1) {
      const line = this._buf.slice(0, nl).replace(/\r$/, '').trim();
      this._buf = this._buf.slice(nl + 1);
      if (line) this._handleMessage(line);
    }
  }

  _startAlive() {
    this._stopAlive();
    this._aliveTimer = setInterval(() => {
      if (this._connected) this._send('ALIVE');
    }, 1000);
  }

  _stopAlive() {
    clearInterval(this._aliveTimer);
    this._aliveTimer = null;
  }

  _send(msg) {
    if (this._socket && this._connected) {
      if (this.debug) this._log('TX:', msg);
      this._socket.write(msg + '\n');
    }
  }

  _scheduleReconnect() {
    if (!this._shouldReconnect) return;

    this._retryCount++;
    if (this._retryCount > this._maxRetries) {
      this._shouldReconnect = false;
      this._log(`Mixer unreachable after ${this._maxRetries} attempts — giving up`);
      this.emit('failed');
      return;
    }

    const delay = Math.min(
      this._reconnectDelay * Math.pow(2, this._retryCount - 1),
      this._maxReconnectDelay
    );
    this._log(`Reconnecting in ${Math.round(delay / 1000)}s (attempt ${this._retryCount}/${this._maxRetries})...`);
    this._reconnectTimer = setTimeout(() => this._doConnect(), delay);
  }

  _handleMessage(msg) {
    // Mixer echoes ALIVE back — already handled by our send interval
    if (msg === 'ALIVE') return;

    if (this.debug && !msg.startsWith('VU2') && !msg.startsWith('RTA^')) this._log('RX:', msg);

    // State updates: "SETD^path^value" or "SETS^path^value"
    if (msg.startsWith('SETD^') || msg.startsWith('SETS^')) {
      const parts = msg.split('^');
      if (parts.length >= 3) {
        const path  = parts[1];
        const value = parts[2];
        this._state[path] = value;
        this._dispatchStateUpdate(path, value);
      }
      return;
    }

    // Media player state
    if (msg === 'MEDIA_PLAY') { this.emit('mediaState', 'play');  return; }
    if (msg === 'MEDIA_PAUSE') { this.emit('mediaState', 'pause'); return; }
    if (msg === 'MEDIA_STOP') { this.emit('mediaState', 'stop');  return; }

    // RTA metering data
    if (msg.startsWith('RTA^')) {
      this.emit('meter', msg.slice(4));
      return;
    }

    // VU metering — decode base64 binary and emit per-channel levels
    if (msg.startsWith('VU2^')) {
      const levels = this._decodeVu(msg.slice(4));
      if (levels) this.emit('vu', levels);
      return;
    }

    this.emit('raw', msg);
  }

  _dispatchStateUpdate(path, value) {
    const num = parseFloat(value);

    if (path.endsWith('.mix')) {
      this.emit('channelLevel', path.slice(0, -4), num);
      return;
    }
    if (path.endsWith('.mute')) {
      this.emit('channelMute', path.slice(0, -5), num !== 0);
      return;
    }
    if (path.endsWith('.solo')) {
      this.emit('channelSolo', path.slice(0, -5), num !== 0);
      return;
    }
    if (path.endsWith('.gain')) {
      this.emit('channelGain', path.slice(0, -5), num);
      return;
    }
    if (path.endsWith('.pan')) {
      this.emit('channelPan', path.slice(0, -4), num);
      return;
    }
    if (path.endsWith('.name')) {
      this.emit('channelName', path.slice(0, -5), value);
      return;
    }

    const auxMatch = path.match(/^(.+)\.aux\.(\d+)\.value$/);
    if (auxMatch) {
      this.emit('auxLevel', auxMatch[1], parseInt(auxMatch[2], 10), num);
      return;
    }

    const fxMatch = path.match(/^(.+)\.fx\.(\d+)\.value$/);
    if (fxMatch) {
      this.emit('fxLevel', fxMatch[1], parseInt(fxMatch[2], 10), num);
      return;
    }

    if (path.endsWith('.phantom')) {
      this.emit('phantom', path.slice(0, -8), num !== 0);
      return;
    }
    if (path.endsWith('.mtkarm')) {
      this.emit('recArm', path.slice(0, -7), num !== 0);
      return;
    }

    const mgMatch = path.match(/^mg\.(\d+)\.mute$/);
    if (mgMatch) {
      this.emit('muteGroup', parseInt(mgMatch[1], 10), num !== 0);
      return;
    }

    // View groups: "vg.0" → array of channel index strings e.g. "[1,2,3,4,]"
    const vgMatch = path.match(/^vg\.(\d+)$/);
    if (vgMatch) {
      const groupIdx = parseInt(vgMatch[1], 10);
      // Parse the trailing-comma-tolerant JSON array of channel indices
      try {
        const channels = JSON.parse(value.replace(/,\s*\]/, ']'));
        this.emit('viewGroup', groupIdx, channels);
      } catch { /* malformed — ignore */ }
      return;
    }
  }

  _decodeVu(data) {
    try {
      const buf = Buffer.from(data, 'base64');
      if (buf.length < 8) return null;

      const nInputs    = buf[0];
      const nMedia     = buf[1];
      const nSubgroups = buf[2];
      const nFx        = buf[3];
      const nAux       = buf[4];
      const nMasters   = buf[5];
      const nLineIn    = buf[6];

      const levels = {};
      let off = 8;

      // Inputs: 6 bytes — vuPre, vuPost, vuPostFader, vuGateIn, vuCompOut, vuCompMeter
      for (let i = 0; i < nInputs; i++, off += 6)
        levels[`i.${i}`] = buf[off + 1] / 255;

      // Media/Player: 6 bytes same layout
      for (let i = 0; i < nMedia; i++, off += 6)
        levels[`p.${i}`] = buf[off + 1] / 255;

      // Subgroups (VCA): 7 bytes — vuPostL, vuPostR, vuPostFaderL, vuPostFaderR, vuGateIn, vuCompOut, vuCompMeter
      for (let i = 0; i < nSubgroups; i++, off += 7)
        levels[`v.${i}`] = buf[off] / 255;

      // FX returns: 7 bytes same layout as subgroups
      for (let i = 0; i < nFx; i++, off += 7)
        levels[`f.${i}`] = buf[off] / 255;

      // AUX masters: 5 bytes — vuPost, vuPostFader, vuGateIn, vuCompOut, vuCompMeter
      for (let i = 0; i < nAux; i++, off += 5)
        levels[`a.${i}`] = buf[off] / 255;

      // Masters: 5 bytes same layout — first master is main mix
      for (let i = 0; i < nMasters; i++, off += 5) {
        if (i === 0) levels['m'] = buf[off] / 255;
      }

      // Line inputs: 6 bytes same layout as inputs
      for (let i = 0; i < nLineIn; i++, off += 6)
        levels[`l.${i}`] = buf[off + 1] / 255;

      return levels;
    } catch {
      return null;
    }
  }

  _log(...args) {
    console.log('[Mixer]', ...args);
  }
}

module.exports = { MixerConnection };
