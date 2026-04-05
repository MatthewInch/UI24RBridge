'use strict';

const { EventEmitter } = require('events');
const JZZ = require('jzz');
const MC = require('./mcProtocol');

/**
 * MidiController
 *
 * Wraps a single JZZ MIDI in+out port pair.
 * Decodes incoming MC messages and emits semantic events.
 * Provides methods to send MC feedback (LCD, LEDs, faders, meters).
 *
 * Events emitted:
 *  'fader'    (channel, level)       – fader moved (level 0.0-1.0)
 *  'button'   (noteNum, velocity)    – button pressed / released
 *  'knob'     (channel, delta)       – V-Pot turned (+1/-1 steps, relative)
 *  'jogwheel' (delta)                – jog wheel delta
 *  'sysex'    (bytes)                – raw sysex received (for handshake)
 */
class MidiController extends EventEmitter {
  /**
   * @param {string}  inputName
   * @param {string}  outputName
   * @param {boolean} isExtender  – changes device SysEx ID used for LCD
   * @param {boolean} debug
   */
  constructor(inputName, outputName, isExtender = false, debug = false) {
    super();
    this.inputName  = inputName;
    this.outputName = outputName;
    this.deviceId   = isExtender ? MC.MC_DEVICE_EXT : MC.MC_DEVICE_MCU;
    this.debug      = debug;

    this._jzz    = null;
    this._input  = null;
    this._output = null;
    this._connected = false;
  }

  // ─── Lifecycle ──────────────────────────────────────────────────────────────

  async connect() {
    this._jzz = await JZZ();

    // Open output first so we can send handshake
    this._output = await this._jzz.openMidiOut(this.outputName);

    // Open input with message handler
    this._input = await this._jzz.openMidiIn(this.inputName);
    this._input.connect((msg) => this._handleMessage(msg));

    this._connected = true;

    // Send device query / wakeup (some controllers need this)
    await this._sendRaw(MC.buildDeviceQuery(this.deviceId));
    // Enable VU meter mode — without this X-Touch ignores Channel Pressure meter messages
    await this._sendRaw(MC.buildMeterEnable(this.deviceId));

    this.emit('connected');
    this._log(`MIDI connected: ${this.inputName} / ${this.outputName}`);
  }

  async disconnect() {
    this._connected = false;
    try { if (this._input)  await this._input.close();  } catch {}
    try { if (this._output) await this._output.close(); } catch {}
    this._input  = null;
    this._output = null;
    this.emit('disconnected');
  }

  get connected() { return this._connected; }

  // ─── Incoming message handling ──────────────────────────────────────────────

  _handleMessage(msg) {
    if (this.debug) {
      this._log('MIDI IN:', msg.toString());
    }

    const status = msg[0] & 0xF0;
    const channel = msg[0] & 0x0F;

    // Note On / Off (buttons)
    if (status === 0x90 || status === 0x80) {
      const note     = msg[1];
      const velocity = (status === 0x90) ? msg[2] : 0;
      this.emit('button', note, velocity);
      return;
    }

    // Pitch Bend (faders)
    if (status === 0xE0) {
      const level = MC.pitchBendToLevel(msg[1], msg[2]);
      this.emit('fader', channel, level);
      return;
    }

    // Control Change (knobs, jog wheel)
    if (status === 0xB0) {
      const cc    = msg[1];
      const value = msg[2];

      if (cc === MC.CC.JOG) {
        // Relative: 1-63 = clockwise, 65-127 = counter-clockwise
        const delta = value <= 63 ? value : value - 128;
        this.emit('jogwheel', delta);
        return;
      }

      // V-Pot (0x10–0x17)
      if (cc >= 0x10 && cc <= 0x17) {
        const ch    = cc - 0x10;
        const delta = value <= 63 ? value : value - 128;
        this.emit('knob', ch, delta);
        return;
      }
    }

    // SysEx (device responses, etc.)
    if (msg[0] === 0xF0) {
      this.emit('sysex', Array.from(msg));
      return;
    }

    // Channel Pressure (touch-sensitive faders: fader-touch detect)
    if (status === 0xD0) {
      // value 0x7F = touched, 0x00 = released
      this.emit('faderTouch', channel, msg[1] > 0);
      return;
    }
  }

  // ─── Outgoing: feedback to controller ──────────────────────────────────────

  /** Set a single button LED */
  async setLed(noteNum, state) {
    if (!this._connected) return;
    await this._sendRaw([0x90, noteNum & 0x7F, state]);
  }

  /** Set LED on (0x7F) */
  async ledOn(noteNum)   { await this.setLed(noteNum, MC.LED_ON); }
  /** Set LED off (0x00) */
  async ledOff(noteNum)  { await this.setLed(noteNum, MC.LED_OFF); }
  /** Set LED blinking */
  async ledBlink(noteNum){ await this.setLed(noteNum, MC.LED_BLINK); }

  /**
   * Move a fader to a specific level (0.0–1.0) with motor.
   * @param {number} channel  0-7 (0-7 for strips, 8 for master)
   * @param {number} level    0.0–1.0
   */
  async setFader(channel, level) {
    if (!this._connected) return;
    const [lsb, msb] = MC.levelToPitchBend(level);
    await this._sendRaw([0xE0 | (channel & 0x0F), lsb, msb]);
  }

  /**
   * Update a single channel LCD (top + bottom row, 7 chars each)
   * @param {number} channel  0-7
   * @param {string} top      7-char string
   * @param {string} bottom   7-char string
   */
  async setChannelLcd(channel, top, bottom) {
    if (!this._connected) return;
    const { topMsg, bottomMsg } = MC.buildLcdSysex(this.deviceId, channel, top, bottom);
    await this._sendRaw(topMsg);
    await this._sendRaw(bottomMsg);
  }

  /**
   * Update all 8 channel LCDs at once.
   * @param {string[]} tops     8 strings
   * @param {string[]} bottoms  8 strings
   */
  async setAllLcd(tops, bottoms) {
    if (!this._connected) return;
    const { topMsg, botMsg } = MC.buildAllLcdSysex(this.deviceId, tops, bottoms);
    await this._sendRaw(topMsg);
    await this._sendRaw(botMsg);
  }

  /**
   * Set a channel VU meter level.
   * @param {number} channel  0-7
   * @param {number} level    0.0–1.0
   */
  async setMeter(channel, level) {
    if (!this._connected) return;
    const bytes = MC.buildVuMeter(channel, level);
    await this._sendRaw(bytes);
  }

  /**
   * Set V-Pot LED ring.
   * @param {number} channel  0-7
   * @param {number} mode     0=single, 1=boost/cut, 2=wrap, 3=spread
   * @param {number} value    0-11
   */
  async setVpotLed(channel, mode, value) {
    if (!this._connected) return;
    await this._sendRaw(MC.buildVpotLed(channel, mode, value));
  }

  /** Clear all LEDs, faders to 0, LCDs to blank */
  async resetAll() {
    if (!this._connected) return;
    // Reset faders
    for (let ch = 0; ch < 9; ch++) await this.setFader(ch, 0);
    // All LEDs off (note 0x00–0x73)
    for (let n = 0; n < 0x74; n++) await this.setLed(n, MC.LED_OFF);
    // Blank LCDs
    const blank = Array(8).fill('       ');
    await this.setAllLcd(blank, blank);
  }

  // ─── Internal ───────────────────────────────────────────────────────────────

  async _sendRaw(bytes) {
    if (!this._output || !this._connected) return;
    try {
      await this._output.send(JZZ.MIDI(bytes));
    } catch (err) {
      this._log('MIDI send error:', err.message);
    }
  }

  _log(...args) {
    console.log(`[MIDI ${this.inputName}]`, ...args);
  }
}

/**
 * List all available MIDI ports.
 * Returns { inputs: string[], outputs: string[] }
 */
async function listPorts() {
  const jzz  = await JZZ();
  const info = jzz.info();
  return {
    inputs:  (info.inputs  || []).map(p => p.name),
    outputs: (info.outputs || []).map(p => p.name),
  };
}

module.exports = { MidiController, listPorts };
