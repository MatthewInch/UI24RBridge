'use strict';

const fs   = require('fs');
const path = require('path');
const MC   = require('./mcProtocol');

/**
 * ButtonMap
 *
 * Loads a ButtonsDefault.json file and resolves note numbers to action names.
 * Allows users to remap any MC button to a different function.
 *
 * JSON format:
 * {
 *   "F1": "AUX1",
 *   "F2": "AUX2",
 *   "SAVE": "MUTE_ALL",
 *   "UNDO": "MUTE_FX",
 *   ...
 * }
 *
 * Supported action names:
 *   AUX1–AUX8, AUX9, AUX10
 *   FX1–FX4
 *   BANK_UP, BANK_DOWN, LAYER_UP, LAYER_DOWN
 *   MUTE_ALL, MUTE_FX, CLEAR_MUTE, CLEAR_SOLO
 *   RECORD_MTK, RECORD_2TRACK, RECORD_BOTH
 *   TAP_TEMPO
 *   SAVE_USERBANK
 *   TALKBACK
 *   MUTE_GROUP_1–MUTE_GROUP_6
 *   PAN_MODE
 *   MEDIA_PLAY, MEDIA_STOP, MEDIA_REWIND, MEDIA_FORWARD
 */

// Canonical button name → note number
const BUTTON_NAMES = {
  // Channel strip (dynamic - these cannot be remapped, they're channel-relative)
  // Navigation
  BANK_LEFT:    MC.NOTE.BANK_LEFT,
  BANK_RIGHT:   MC.NOTE.BANK_RIGHT,
  CHANNEL_LEFT: MC.NOTE.CHANNEL_LEFT,
  CHANNEL_RIGHT:MC.NOTE.CHANNEL_RIGHT,
  FLIP:         MC.NOTE.FLIP,
  GLOBAL_VIEW:  MC.NOTE.GLOBAL_VIEW,
  // Function keys
  F1: MC.NOTE.F1, F2: MC.NOTE.F2, F3: MC.NOTE.F3, F4: MC.NOTE.F4,
  F5: MC.NOTE.F5, F6: MC.NOTE.F6, F7: MC.NOTE.F7, F8: MC.NOTE.F8,
  // Modifiers
  SHIFT:   MC.NOTE.SHIFT,
  OPTION:  MC.NOTE.OPTION,
  CONTROL: MC.NOTE.CONTROL,
  ALT:     MC.NOTE.ALT,
  // Automation
  READ_OFF: MC.NOTE.READ_OFF,
  WRITE:    MC.NOTE.WRITE,
  TRIM:     MC.NOTE.TRIM,
  TOUCH:    MC.NOTE.TOUCH,
  LATCH:    MC.NOTE.LATCH,
  GROUP:    MC.NOTE.GROUP,
  // Utilities
  SAVE:   MC.NOTE.SAVE,
  UNDO:   MC.NOTE.UNDO,
  CANCEL: MC.NOTE.CANCEL,
  ENTER:  MC.NOTE.ENTER,
  // Transport
  REWIND:  MC.NOTE.REWIND,
  FORWARD: MC.NOTE.FORWARD,
  STOP:    MC.NOTE.STOP,
  PLAY:    MC.NOTE.PLAY,
  RECORD:  MC.NOTE.RECORD,
  // Misc
  SCRUB:        MC.NOTE.SCRUB,
  PAN_SURROUND: MC.NOTE.PAN_SURROUND,
  SMPTE_BEATS:  MC.NOTE.SMPTE_BEATS,
  USER:         MC.NOTE.USER,
  MARKER:       MC.NOTE.MARKER,
  CYCLE:        MC.NOTE.CYCLE,
  DROP:         MC.NOTE.DROP,
  REPLACE:      MC.NOTE.REPLACE,
  CLICK:        MC.NOTE.CLICK,
  SOLO_GLOBAL:  MC.NOTE.SOLO_GLOBAL,
};

// All valid action names
const VALID_ACTIONS = new Set([
  'AUX1','AUX2','AUX3','AUX4','AUX5','AUX6','AUX7','AUX8','AUX9','AUX10',
  'FX1','FX2','FX3','FX4',
  'BANK_UP','BANK_DOWN','LAYER_UP','LAYER_DOWN',
  'MUTE_ALL','MUTE_FX','CLEAR_MUTE','CLEAR_SOLO',
  'RECORD_MTK','RECORD_2TRACK','RECORD_BOTH',
  'TAP_TEMPO','SAVE_USERBANK','TALKBACK','PAN_MODE',
  'MUTE_GROUP_1','MUTE_GROUP_2','MUTE_GROUP_3',
  'MUTE_GROUP_4','MUTE_GROUP_5','MUTE_GROUP_6',
  'MEDIA_PLAY','MEDIA_STOP','MEDIA_REWIND','MEDIA_FORWARD',
  // Can also remap to default behaviours
  'DEFAULT',
]);

class ButtonMap {
  constructor(filePath) {
    // note number → action string
    this._map = new Map();
    // note number → original default action (for DEFAULT passthrough)
    this._defaults = new Map();

    if (filePath && fs.existsSync(filePath)) {
      this._load(filePath);
    }
  }

  /** Returns the action string for a given note number, or null for default */
  getAction(note) {
    return this._map.get(note) || null;
  }

  _load(filePath) {
    let raw;
    try {
      raw = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    } catch (err) {
      console.warn(`[ButtonMap] Failed to load ${filePath}:`, err.message);
      return;
    }

    for (const [buttonName, action] of Object.entries(raw)) {
      const noteNum = BUTTON_NAMES[buttonName.toUpperCase()];
      if (noteNum === undefined) {
        console.warn(`[ButtonMap] Unknown button: ${buttonName}`);
        continue;
      }
      const actionUpper = action.toUpperCase();
      if (!VALID_ACTIONS.has(actionUpper)) {
        console.warn(`[ButtonMap] Unknown action: ${action}`);
        continue;
      }
      this._map.set(noteNum, actionUpper);
    }

    console.log(`[ButtonMap] Loaded ${this._map.size} remappings from ${path.basename(filePath)}`);
  }
}

/**
 * Default ButtonsDefault.json — written to userData if not present.
 * This mirrors the original C# defaults exactly.
 */
const DEFAULT_BUTTON_CONFIG = {
  "BANK_LEFT":    "BANK_DOWN",
  "BANK_RIGHT":   "BANK_UP",
  "CHANNEL_LEFT": "LAYER_DOWN",
  "CHANNEL_RIGHT":"LAYER_UP",
  "F1": "AUX1",
  "F2": "AUX2",
  "F3": "AUX3",
  "F4": "AUX4",
  "F5": "AUX5",
  "F6": "AUX6",
  "F7": "AUX7",
  "F8": "AUX8",
  "SHIFT":   "FX1",
  "OPTION":  "FX2",
  "CONTROL": "FX3",
  "ALT":     "FX4",
  "SMPTE_BEATS": "TAP_TEMPO",
  "GLOBAL_VIEW": "SAVE_USERBANK",
  "SAVE":   "MUTE_ALL",
  "UNDO":   "MUTE_FX",
  "CANCEL": "CLEAR_MUTE",
  "ENTER":  "CLEAR_SOLO",
  "SCRUB":  "TALKBACK",
  "PAN_SURROUND": "PAN_MODE",
  "PLAY":    "MEDIA_PLAY",
  "STOP":    "MEDIA_STOP",
  "REWIND":  "MEDIA_REWIND",
  "FORWARD": "MEDIA_FORWARD",
  "READ_OFF": "MUTE_GROUP_1",
  "WRITE":    "MUTE_GROUP_2",
  "TRIM":     "MUTE_GROUP_3",
  "TOUCH":    "MUTE_GROUP_4",
  "LATCH":    "MUTE_GROUP_5",
  "GROUP":    "MUTE_GROUP_6",
  "RECORD":   "RECORD_BOTH",
};

function writeDefaultIfMissing(filePath) {
  if (!fs.existsSync(filePath)) {
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, JSON.stringify(DEFAULT_BUTTON_CONFIG, null, 2), 'utf8');
  }
}

module.exports = { ButtonMap, BUTTON_NAMES, DEFAULT_BUTTON_CONFIG, writeDefaultIfMissing };
