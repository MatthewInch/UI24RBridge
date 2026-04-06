'use strict';

/**
 * Mackie Control Protocol constants and helpers.
 *
 * References:
 *  - Mackie Control Universal Pro MIDI Implementation (public doc)
 *  - X-Touch MIDI implementation guide
 */

// ─── SysEx header ────────────────────────────────────────────────────────────
const MC_SYSEX_HEADER = [0xF0, 0x00, 0x00, 0x66];
// Device IDs: 0x14 = MCU, 0x15 = extender
const MC_DEVICE_MCU  = 0x14;
const MC_DEVICE_EXT  = 0x15;

// ─── Note numbers (buttons) ───────────────────────────────────────────────────
const NOTE = {
  // Channel strip (0-7 = ch1-8)
  REC:    (ch) => ch,          // 0x00–0x07
  SOLO:   (ch) => ch + 0x08,  // 0x08–0x0F
  MUTE:   (ch) => ch + 0x10,  // 0x10–0x17
  SELECT: (ch) => ch + 0x18,  // 0x18–0x1F

  // V-Pots (knob push)
  VPOT_PRESS: (ch) => ch + 0x20, // 0x20–0x27

  // Fader touch (ch 0-8, 8=master)
  FADER_TOUCH: (ch) => ch + 0x68, // 0x68–0x70

  // Transport
  REWIND:   0x5B,
  FORWARD:  0x5C,
  STOP:     0x5D,
  PLAY:     0x5E,
  RECORD:   0x5F,

  // Navigation
  UP:       0x60,
  DOWN:     0x61,
  LEFT:     0x62,
  RIGHT:    0x63,
  ZOOM:     0x64,
  SCRUB:    0x65,

  // Bank / channel navigation
  BANK_LEFT:    0x2E,
  BANK_RIGHT:   0x2F,
  CHANNEL_LEFT: 0x30,
  CHANNEL_RIGHT:0x31,

  // Flip
  FLIP:     0x32,

  // Global view
  GLOBAL_VIEW: 0x33,

  // Function keys F1–F8
  F1: 0x36, F2: 0x37, F3: 0x38, F4: 0x39,
  F5: 0x3A, F6: 0x3B, F7: 0x3C, F8: 0x3D,

  // Modifiers
  SHIFT:   0x46,
  OPTION:  0x47,
  CONTROL: 0x48,
  ALT:     0x49,

  // Automation
  READ_OFF: 0x4A,
  WRITE:    0x4B,
  TRIM:     0x4C,
  TOUCH:    0x4D,
  LATCH:    0x4E,
  GROUP:    0x4F,

  // Utilities
  SAVE:   0x50,
  UNDO:   0x51,
  CANCEL: 0x52,
  ENTER:  0x53,

  // Markers
  MARKER:    0x54,
  CYCLE:     0x55,
  DROP:      0x56,
  REPLACE:   0x57,
  CLICK:     0x58,
  SOLO_GLOBAL: 0x59,

  // Pan / surround
  PAN_SURROUND: 0x2A,

  // SMPTE / Beats (tap tempo)
  SMPTE_BEATS: 0x35,

  // User switches
  USER:    0x66,

  // Master fader touch
  MASTER_FADER_TOUCH: 0x70,
};

// ─── CC numbers (knobs) ───────────────────────────────────────────────────────
const CC = {
  VPOT: (ch) => ch + 0x10,  // 0x10–0x17  relative encoder value
  JOG:  0x3C,
};

// ─── Pitch bend (faders) ─────────────────────────────────────────────────────
// Fader channel = MIDI channel 0-7 for ch1-8, channel 8 for master
// Pitch bend range: 0x0000–0x3FFF (14-bit)

/**
 * Decode pitch bend bytes to 0.0–1.0
 * @param {number} lsb  low 7 bits
 * @param {number} msb  high 7 bits
 */
function pitchBendToLevel(lsb, msb) {
  return ((msb << 7) | lsb) / 0x3FFF;
}

/**
 * Encode level 0.0–1.0 to pitch bend [lsb, msb]
 */
function levelToPitchBend(level) {
  const val = Math.round(Math.max(0, Math.min(1, level)) * 0x3FFF);
  return [val & 0x7F, (val >> 7) & 0x7F];
}

// ─── LED states ───────────────────────────────────────────────────────────────
const LED_OFF = 0x00;
const LED_ON  = 0x7F;
const LED_BLINK = 0x01;

// ─── LCD SysEx ───────────────────────────────────────────────────────────────
/**
 * Build a SysEx message to update the channel LCD strip.
 * Each channel has 2 rows of 7 characters.
 *
 * @param {number} deviceId   MC_DEVICE_MCU or MC_DEVICE_EXT
 * @param {number} channel    0-7
 * @param {string} topLine    7-char string
 * @param {string} bottomLine 7-char string
 * @returns {number[]}
 */
function buildLcdSysex(deviceId, channel, topLine, bottomLine) {
  const offset = channel * 7;
  const topBytes    = stringToMcAscii(topLine,    7);
  const bottomBytes = stringToMcAscii(bottomLine, 7);

  // Top row: SysEx command 0x12, offset
  const topMsg    = [...MC_SYSEX_HEADER, deviceId, 0x12, offset,    ...topBytes,    0xF7];
  // Bottom row: offset + 56 (56 = 8 channels * 7 chars)
  const bottomMsg = [...MC_SYSEX_HEADER, deviceId, 0x12, offset + 56, ...bottomBytes, 0xF7];

  return { topMsg, bottomMsg };
}

/**
 * Build SysEx to update ALL 8 channel LCDs at once.
 * @param {number}   deviceId
 * @param {string[]} topLines     8 strings of 7 chars each
 * @param {string[]} bottomLines  8 strings of 7 chars each
 */
function buildAllLcdSysex(deviceId, topLines, bottomLines) {
  const topBytes = [];
  const botBytes = [];
  for (let i = 0; i < 8; i++) {
    topBytes.push(...stringToMcAscii(topLines[i] || '', 7));
    botBytes.push(...stringToMcAscii(bottomLines[i] || '', 7));
  }
  const topMsg = [...MC_SYSEX_HEADER, deviceId, 0x12, 0,  ...topBytes, 0xF7];
  const botMsg = [...MC_SYSEX_HEADER, deviceId, 0x12, 56, ...botBytes, 0xF7];
  return { topMsg, botMsg };
}

/**
 * Convert a string to 7-bit ASCII bytes, padded/truncated to `len`
 */
function stringToMcAscii(str, len) {
  const result = [];
  const s = (str || '').padEnd(len, ' ').slice(0, len);
  for (let i = 0; i < len; i++) {
    result.push(s.charCodeAt(i) & 0x7F);
  }
  return result;
}

// ─── VU Meter SysEx ──────────────────────────────────────────────────────────
/**
 * Build a channel meter message for Mackie Control.
 * Format: single Channel Pressure message on MIDI channel 0.
 *   status byte: 0xD0  (always)
 *   data byte:   (channelNumber << 4) | meterValue
 *     meterValue 0–13 = normal levels  (raw_byte / 18)
 *     meterValue 14   = clip
 *
 * @param {number} channel  0-7
 * @param {number} level    0.0–1.0
 * @returns {number[]}  [0xD0, data]
 */
function buildVuMeter(channel, level) {
  const raw = level * 255;
  const val = raw >= 240 ? 14 : Math.min(13, Math.floor(raw / 18));
  return [0xD0, ((channel & 0x07) << 4) | val];
}

// ─── V-Pot ring LEDs ─────────────────────────────────────────────────────────
/**
 * Build CC message for V-Pot LED ring.
 * @param {number} channel  0-7
 * @param {number} mode     0=single, 1=boost/cut, 2=wrap, 3=spread
 * @param {number} value    0-11 (position)
 */
function buildVpotLed(channel, mode, value) {
  const cc = 0x30 + channel;
  const data = ((mode & 0x03) << 4) | (value & 0x0F);
  return [0xB0, cc, data];
}

// ─── Main (timecode) 7-segment display ────────────────────────────────────────
/**
 * Build CC messages to write text to the main 7-segment display (12 chars).
 *
 * The Mackie Control / X-Touch timecode display interprets the CC data byte
 * as an ASCII character code (7 bits, 0–127). The controller's firmware
 * handles the 7-segment rendering internally.
 *
 * CC 0x4B = leftmost digit, CC 0x40 = rightmost digit.
 * Text position 0 maps to the leftmost digit.
 *
 * @param {string} text  Up to 12-character string (will be uppercased + padded)
 * @returns {number[][]}  Array of 3-byte CC messages [0xB0, cc, value]
 */
function buildMainDisplay(text) {
  const s = (text || '').toUpperCase().padEnd(12, ' ').slice(0, 12);
  const msgs = [];
  for (let i = 0; i < 12; i++) {
    // CC 0x4B = position 0 (leftmost), CC 0x40 = position 11 (rightmost)
    const cc  = 0x40 + (11 - i);
    const val = s.charCodeAt(i) & 0x7F;
    msgs.push([0xB0, cc, val]);
  }
  return msgs;
}

// ─── Device inquiry / handshake ───────────────────────────────────────────────
/**
 * Build the MC device query SysEx (host → device)
 * Device responds with its firmware version
 */
function buildDeviceQuery(deviceId) {
  return [...MC_SYSEX_HEADER, deviceId, 0x00, 0xF7];
}

/**
 * Build the SysEx to enable VU meter mode on the controller.
 * Without this, X-Touch and compatible devices ignore Channel Pressure
 * meter messages and display nothing on the meter LEDs.
 * Command 0x21 with value 0x01 = enable meters.
 */
function buildMeterEnable(deviceId) {
  return [...MC_SYSEX_HEADER, deviceId, 0x21, 0x01, 0xF7];
}

module.exports = {
  buildMeterEnable,
  MC_SYSEX_HEADER,
  MC_DEVICE_MCU,
  MC_DEVICE_EXT,
  NOTE,
  CC,
  LED_OFF,
  LED_ON,
  LED_BLINK,
  pitchBendToLevel,
  levelToPitchBend,
  buildLcdSysex,
  buildAllLcdSysex,
  buildVuMeter,
  buildVpotLed,
  buildMainDisplay,
  buildDeviceQuery,
  stringToMcAscii,
};
