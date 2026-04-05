'use strict';

const { EventEmitter } = require('events');
const ChannelMap = require('./channelMap');

/**
 * BankManager
 *
 * Tracks which bank (I/U/V) and layer (0-5) is active,
 * resolves the current channel list, and stores ViewGroups.
 *
 * Events:
 *  'channelsChanged' (primary[], secondary[]) – new channel layout
 */
class BankManager extends EventEmitter {
  constructor(config, userGroups = null) {
    super();
    this.primaryStart   = parseInt(config['PrimaryChannelStart']   || '0', 10);
    this.secondaryStart = parseInt(config['SecondaryChannelStart'] || '1', 10);
    this.hasSecondary   = !!(config['MIDI-Input-Name-Second']);
    this.startBank      = parseInt(config['StartBank'] || '0', 10);

    this._bank       = ['I', 'U', 'V'][this.startBank] || 'I';
    this._layer      = 0;
    this._userGroups = userGroups || ChannelMap.defaultViewGroups();
    this._viewGroups = Array(6).fill(null).map(() => []);  // mixer global views

    this._primaryChannels   = [];
    this._secondaryChannels = [];
    this._resolve();
  }

  // ─── Navigation ─────────────────────────────────────────────────────────────

  get bank()  { return this._bank; }
  get layer() { return this._layer; }

  bankUp() {
    const banks = ['I', 'U', 'V'];
    const idx = banks.indexOf(this._bank);
    this._bank = banks[(idx + 1) % banks.length];
    this._layer = 0;
    this._resolve();
    this._changed();
  }

  bankDown() {
    const banks = ['I', 'U', 'V'];
    const idx = banks.indexOf(this._bank);
    this._bank = banks[(idx + 2) % banks.length];
    this._layer = 0;
    this._resolve();
    this._changed();
  }

  layerUp() {
    this._layer = (this._layer + 1) % ChannelMap.NUM_LAYERS;
    this._resolve();
    this._changed();
  }

  layerDown() {
    this._layer = (this._layer + ChannelMap.NUM_LAYERS - 1) % ChannelMap.NUM_LAYERS;
    this._resolve();
    this._changed();
  }

  // ─── Channels ────────────────────────────────────────────────────────────────

  /** 8 channel paths for the primary controller */
  get primaryChannels()   { return this._primaryChannels; }
  /** 8 channel paths for the secondary controller (empty if none) */
  get secondaryChannels() { return this._secondaryChannels; }

  /** Returns the resolved channel path for a strip (0-7) on a given controller */
  getChannel(stripIndex, isSecondary = false) {
    const list = isSecondary ? this._secondaryChannels : this._primaryChannels;
    return list[stripIndex] || '';
  }

  // ─── User bank (Bank U) editing ──────────────────────────────────────────────

  /** Set a channel slot in Bank U */
  setUserChannel(layer, slot, channelPath) {
    if (!this._userGroups[layer]) this._userGroups[layer] = Array(8).fill('');
    this._userGroups[layer][slot] = channelPath;
    if (this._bank === 'U' && this._layer === layer) {
      this._resolve();
      this._changed();
    }
  }

  getUserGroups() { return this._userGroups; }
  setUserGroups(groups) {
    this._userGroups = groups;
    if (this._bank === 'U') { this._resolve(); this._changed(); }
  }

  // ─── Global view groups (Bank V) ─────────────────────────────────────────────

  /** Called when mixer reports its global view groups */
  setViewGroups(groups) {
    this._viewGroups = groups;
    if (this._bank === 'V') { this._resolve(); this._changed(); }
  }

  // ─── Internal ────────────────────────────────────────────────────────────────

  _resolve() {
    this._primaryChannels = ChannelMap.resolveChannels(
      this._bank, this._layer, this.primaryStart, this._userGroups, this._viewGroups
    );
    if (this.hasSecondary) {
      this._secondaryChannels = ChannelMap.resolveChannels(
        this._bank, this._layer, this.secondaryStart, this._userGroups, this._viewGroups
      );
    } else {
      this._secondaryChannels = [];
    }
  }

  _changed() {
    this.emit('channelsChanged', this._primaryChannels, this._secondaryChannels);
  }

  /** Serializable state for UI */
  toStatus() {
    return {
      bank: this._bank,
      layer: this._layer,
      primaryChannels: this._primaryChannels,
      secondaryChannels: this._secondaryChannels,
    };
  }
}

module.exports = { BankManager };
