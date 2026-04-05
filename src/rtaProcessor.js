'use strict';

/**
 * RtaProcessor
 *
 * The UI24R sends metering data as text frames over the WebSocket.
 * Format (observed): "RTA^<json_array_or_csv_levels>"
 *
 * The mixer sends multiple meter formats:
 *   "RTA^{"i":[0.1,0.2,...],"a":[...],"m":0.8}"
 *   or flat arrays depending on firmware version.
 *
 * This class decodes the data and maintains a level cache
 * that the bridge can read at its meter refresh rate (25fps).
 *
 * Channel path format → level 0.0–1.0
 */
class RtaProcessor {
  constructor() {
    // path → peak level (0.0–1.0)
    this._levels = {};
    // Decay rate per tick (25fps → ~0.04/tick for fast fallback)
    this._decay = 0.04;
  }

  /**
   * Process a raw RTA payload (everything after "RTA^")
   * @param {string} data
   */
  process(data) {
    if (!data) return;

    // Try JSON format first
    if (data.startsWith('{') || data.startsWith('[')) {
      this._processJson(data);
      return;
    }

    // Flat CSV format: "i.0:0.34,i.1:0.12,..."
    if (data.includes(':')) {
      this._processCsv(data);
      return;
    }

    // Compact array format (some firmware versions):
    // ordered by channel type/index — inputs first, then aux, etc.
    // Fall back to no-op if unrecognized
  }

  /**
   * Parse JSON meter format.
   * Expected: { "i": [lev0, lev1, ...], "a": [...], "l": [...], "p": [...], "f": [...], "m": lev }
   */
  _processJson(data) {
    let obj;
    try { obj = JSON.parse(data); } catch { return; }

    const typeMap = { i: 'i', a: 'a', l: 'l', p: 'p', f: 'f', v: 'v' };
    for (const [type, jsKey] of Object.entries(typeMap)) {
      if (Array.isArray(obj[jsKey])) {
        obj[jsKey].forEach((lev, idx) => {
          this._set(`${type}.${idx}`, lev);
        });
      }
    }
    // Main mix
    if (typeof obj.m === 'number') {
      this._set('m', obj.m);
    }
    // Nested meter objects (some firmware): { "io": { "0": lev, ... } }
    if (obj.io && typeof obj.io === 'object') {
      for (const [idx, lev] of Object.entries(obj.io)) {
        this._set(`i.${idx}`, lev);
      }
    }
  }

  /**
   * Parse CSV format: "i.0:0.34,i.1:0.12,a.0:0.56"
   */
  _processCsv(data) {
    for (const part of data.split(',')) {
      const [path, valStr] = part.split(':');
      if (path && valStr !== undefined) {
        this._set(path.trim(), parseFloat(valStr));
      }
    }
  }

  /**
   * Apply decay to all levels (call once per meter tick to simulate fallback)
   */
  tick() {
    for (const key of Object.keys(this._levels)) {
      this._levels[key] = Math.max(0, this._levels[key] - this._decay);
    }
  }

  /**
   * Get the current level for a channel path (0.0–1.0)
   */
  getLevel(channelPath) {
    return this._levels[channelPath] || 0;
  }

  /**
   * Get levels for an array of channel paths
   */
  getLevels(channelPaths) {
    return channelPaths.map(p => this.getLevel(p));
  }

  _set(path, value) {
    const lev = Math.max(0, Math.min(1, parseFloat(value) || 0));
    // Peak-hold: only update if new level is higher, or existing has decayed
    if ((this._levels[path] || 0) < lev) {
      this._levels[path] = lev;
    }
  }
}

module.exports = { RtaProcessor };
