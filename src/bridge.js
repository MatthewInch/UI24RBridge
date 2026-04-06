'use strict';

const { EventEmitter } = require('events');
const path   = require('path');
const fs     = require('fs');
const { app } = require('electron');

const { MidiController }  = require('./midiController');
const { MixerConnection } = require('./mixerConnection');
const { BankManager }     = require('./bankManager');
const { ButtonMap, writeDefaultIfMissing } = require('./buttonMap');
const { RtaProcessor }    = require('./rtaProcessor');
const MC   = require('./mcProtocol');
const CMap = require('./channelMap');

/**
 * Bridge — central coordinator
 *
 * Owns primary + optional secondary MidiController, MixerConnection,
 * BankManager, ButtonMap, and RtaProcessor.
 *
 * All MC MIDI events are decoded and translated to UI24R WebSocket
 * commands, and all mixer state updates are reflected back to the
 * controller LEDs, faders, and LCDs.
 *
 * Emits:
 *  'status' { midi, midi2, mixer }
 *  'log'    string
 *  'state'  object
 */
class Bridge extends EventEmitter {
  constructor(config) {
    super();
    this.config  = config;
    this.debug   = config['DebugMessages'] === 'true';

    this._primary   = null;
    this._secondary = null;
    this._mixer     = null;
    this._banks     = null;
    this._rta       = new RtaProcessor();
    this._vuLevels  = {};  // path → level (0.0–1.0) from VU2 messages
    this._buttonMap = null;

    // Send modes
    this._auxMode   = null;   // null or 0-9
    this._fxMode    = null;   // null or 0-3
    this._panMode   = false;
    this._auxLocked = config['AuxButtonBehavior'] === 'Lock';
    this._isPlaying = false;
    this._tapTempo  = 0;    // BPM received from mixer

    // Talkback
    this._talkbackPath = config['TalkBack']
      ? `i.${parseInt(config['TalkBack'], 10)}`
      : null;

    // RTA on select
    this._rtaOnSelect = config['RtaOnWhenSelect'] === 'true';

    // Per-channel state cache: path → { mute, solo, recArm, fader, gain, pan, phantom, name }
    this._channelState = {};

    // Channel currently selected on controller
    this._selectedChannel = null;

    // User bank editing
    this._userEditActive    = false;
    this._userEditStrip     = null;
    this._userEditSecondary = false;
    this._userButtonHeld    = false;
    this._allChannelPaths   = this._buildAllChannelPaths();
    this._jogCursor         = 0;

    // View groups from mixer (for Bank V)
    this._viewGroups = [];

    this._meterInterval = null;
  }

  // ─── Lifecycle ──────────────────────────────────────────────────────────────

  async start() {
    const cfg = this.config;

    // Ensure ButtonsDefault.json exists in userData
    const btnFile = path.join(
      app.getPath('userData'),
      cfg['PrimaryButtons'] || 'ButtonsDefault.json'
    );
    writeDefaultIfMissing(btnFile);
    this._buttonMap = new ButtonMap(btnFile);

    // Load user ViewGroups
    const userGroups = this._loadViewGroups();

    // Bank manager
    this._banks = new BankManager(cfg, userGroups);
    this._banks.on('channelsChanged', (pri, sec) => this._onChannelsChanged(pri, sec));

    // Mixer WebSocket
    this._mixer = new MixerConnection(cfg['UI24R-Url'], this.debug);
    this._bindMixerEvents();
    this._mixer.connect();

    // Primary MIDI controller
    const pIn  = cfg['MIDI-Input-Name'];
    const pOut = cfg['MIDI-Output-Name'];
    if (pIn && pOut) {
      this._primary = new MidiController(
        pIn, pOut,
        cfg['PrimaryIsExtender'] === 'true',
        this.debug
      );
      this._bindMidiEvents(this._primary, false);
      try { await this._primary.connect(); }
      catch (err) { this._log(`Primary MIDI failed: ${err.message}`); }
    }

    // Secondary MIDI controller
    const sIn  = cfg['MIDI-Input-Name-Second'];
    const sOut = cfg['MIDI-Output-Name-Second'];
    if (sIn && sOut) {
      this._secondary = new MidiController(
        sIn, sOut,
        cfg['SecondaryIsExtender'] === 'true',
        this.debug
      );
      this._bindMidiEvents(this._secondary, true);
      try { await this._secondary.connect(); }
      catch (err) { this._log(`Secondary MIDI failed: ${err.message}`); }
    }

    // VU meter refresh ~25fps
    this._meterInterval = setInterval(() => this._meterTick(), 40);

    this._emitStatus();
    this._log('Bridge started');
  }

  stop() {
    clearInterval(this._meterInterval);
    this._primary?.disconnect();
    this._secondary?.disconnect();
    this._mixer?.disconnect();
    this._primary = this._secondary = this._mixer = null;
    this._log('Bridge stopped');
  }

  // ─── MIDI event binding ─────────────────────────────────────────────────────

  _bindMidiEvents(ctrl, isSecondary) {
    ctrl.on('connected',    () => this._onMidiConnected(ctrl, isSecondary));
    ctrl.on('disconnected', () => this._emitStatus());
    ctrl.on('fader',        (ch, lv) => this._onFader(ch, lv, isSecondary));
    ctrl.on('button',       (n, v)   => this._onButton(n, v, isSecondary));
    ctrl.on('knob',         (ch, d)  => this._onKnob(ch, d, isSecondary));
    ctrl.on('jogwheel',     (d)      => this._onJogWheel(d, isSecondary));
    ctrl.on('faderTouch',   ()       => {}); // hook point for future automation
  }

  async _onMidiConnected(ctrl, isSecondary) {
    this._emitStatus();
    this._log(`MIDI ${isSecondary ? 'secondary' : 'primary'} connected: ${ctrl.inputName}`);
    setTimeout(() => {
      this._refreshController(ctrl, isSecondary).catch(err =>
        this._log('refreshController error:', err.message));
    }, 400);
  }

  // ─── Mixer event binding ─────────────────────────────────────────────────────

  _bindMixerEvents() {
    const m = this._mixer;

    m.on('connected',    () => { this._log('Mixer connected'); this._emitStatus(); });
    m.on('disconnected', () => { this._log('Mixer disconnected'); this._emitStatus(); });
    m.on('failed',       () => {
      this._log('Mixer connection failed — stopping bridge');
      this.stop();
      this.emit('stopped');
    });

    m.on('channelLevel', (p, v) => {
      this._updateCh(p, { fader: v });
      this._syncFaderToController(p, v);
    });

    m.on('channelMute', (p, v) => {
      this._updateCh(p, { mute: v });
      this._syncMuteLed(p, v);
    });

    m.on('channelSolo', (p, v) => {
      this._updateCh(p, { solo: v });
      this._syncSoloLed(p, v);
    });

    m.on('channelGain', (p, v) => {
      this._updateCh(p, { gain: v });
      if (this._auxMode === null && this._fxMode === null && !this._panMode)
        this._syncKnobRing(p, v);
    });

    m.on('channelPan', (p, v) => {
      this._updateCh(p, { pan: v });
      if (this._panMode)
        this._syncKnobRing(p, v);
    });

    m.on('channelName', (p, v) => {
      this._updateCh(p, { name: v });
      this._syncLcd();
      this._emitState();
    });

    m.on('phantom', (p, v) => {
      this._updateCh(p, { phantom: v });
      if (this.config['DefaultChannelRecButton'] === 'phantom')
        this._syncRecLed(p, v);
    });

    m.on('recArm', (p, v) => {
      this._updateCh(p, { recArm: v });
      if (this.config['DefaultChannelRecButton'] !== 'phantom')
        this._syncRecLed(p, v);
    });

    m.on('auxLevel', (p, auxIdx, v) => {
      this._updateCh(p, { [`aux_${auxIdx}`]: v });
      if (this._auxMode === auxIdx) this._syncFaderToController(p, v);
    });

    m.on('fxLevel', (p, fxIdx, v) => {
      this._updateCh(p, { [`fx_${fxIdx}`]: v });
      if (this._fxMode === fxIdx) this._syncFaderToController(p, v);
    });

    m.on('muteGroup', (groupIdx, active) => {
      const noteMap = [
        MC.NOTE.READ_OFF, MC.NOTE.WRITE, MC.NOTE.TRIM,
        MC.NOTE.TOUCH, MC.NOTE.LATCH, MC.NOTE.GROUP,
      ];
      const led = active ? MC.LED_ON : MC.LED_OFF;
      if (noteMap[groupIdx] !== undefined) {
        this._primary?.setLed(noteMap[groupIdx], led);
        this._secondary?.setLed(noteMap[groupIdx], led);
      }
    });

    m.on('meter', (data) => this._rta.process(data));
    m.on('vu', (levels) => { this._vuLevels = levels; });

    m.on('mediaState', (state) => {
      this._setPlayLed(state === 'play');
    });

    m.on('bpm', (v) => {
      this._tapTempo = Math.round(v);
      this._syncMainDisplay();
    });

    m.on('viewGroup', (groupIdx, channelIndices) => {
      this._handleViewGroupUpdate(groupIdx, channelIndices);
    });

    m.on('raw', (_msg) => { /* unhandled mixer messages */ });
  }

  // ─── MIDI → Mixer: faders ────────────────────────────────────────────────────

  _onFader(stripIndex, level, isSecondary) {
    // Strip index 8 is the master fader in MCU protocol
    const p = stripIndex === 8 ? 'm' : this._banks.getChannel(stripIndex, isSecondary);
    if (!p) return;

    // MCU protocol requires the host to echo the fader position back immediately.
    // Without this, motorized faders snap back to the last host-confirmed position
    // when the user releases the fader.
    const ctrl = isSecondary ? this._secondary : this._primary;
    ctrl?.setFader(stripIndex, level);

    if (stripIndex === 8) {
      this._mixer.setFaderLevel(p, level);
    } else if (this._auxMode !== null) {
      this._mixer.setAuxSend(p, this._auxMode, level);
    } else if (this._fxMode !== null) {
      this._mixer.setFxSend(p, this._fxMode, level);
    } else {
      this._mixer.setFaderLevel(p, level);
    }
  }

  // ─── MIDI → Mixer: knobs ─────────────────────────────────────────────────────

  _onKnob(stripIndex, delta, isSecondary) {
    const p = this._banks.getChannel(stripIndex, isSecondary);
    if (!p) return;

    // MC relative encoders encode CW as 1–63 and CCW as 65–127 (65=fast, 127=slow).
    // A slow CCW turn sends value=65 → delta=−63, which is 63× larger magnitude than
    // a slow CW turn (delta=1). Normalize to ±1 per detent for symmetric speed.
    const clampedDelta = Math.sign(delta);

    if (this._panMode) {
      const cur  = this._getCh(p).pan ?? 0.5;
      const next = Math.max(0, Math.min(1, cur + clampedDelta * 0.02));
      this._mixer.setParam(`${p}.pan`, next);
      this._updateCh(p, { pan: next });
      this._syncKnobRing(p, next);
    } else {
      const cur  = this._getCh(p).gain || 0;
      const next = Math.max(0, Math.min(1, cur + clampedDelta * 0.005));
      this._mixer.setGain(p, next);
      this._updateCh(p, { gain: next });
      this._syncKnobRing(p, next);
    }
  }

  // ─── MIDI → Mixer: jog wheel ─────────────────────────────────────────────────

  _onJogWheel(delta, isSecondary) {
    if (!this._userEditActive || !this._userButtonHeld) return;

    // Cycle through all channel paths
    this._jogCursor = (this._jogCursor + delta + this._allChannelPaths.length)
      % this._allChannelPaths.length;

    const newPath = this._allChannelPaths[this._jogCursor];
    const strip   = this._userEditStrip;
    if (strip === null) return;

    // Preview on the strip's LCD
    const ctrl = isSecondary ? this._secondary : this._primary;
    ctrl?.setChannelLcd(strip,
      'ASSIGN '.slice(0, 7),
      CMap.channelLabel(newPath).padEnd(7).slice(0, 7)
    );
  }

  // ─── MIDI → Mixer: buttons ───────────────────────────────────────────────────

  _onButton(note, velocity, isSecondary) {
    const pressed = velocity > 0;

    // USER button: activate/deactivate user bank editing
    if (note === MC.NOTE.USER) {
      this._userButtonHeld = pressed;
      if (pressed && this._banks.bank === 'U') {
        this._userEditActive = true;
        this._primary?.ledBlink(MC.NOTE.USER);
        this._log('User bank edit mode — select a strip, then turn JOG');
      } else if (!pressed && this._userEditActive) {
        this._confirmUserEdit(isSecondary);
      }
      return;
    }

    // During user-edit, SELECT buttons pick the strip to reassign
    if (this._userEditActive && this._userButtonHeld && pressed) {
      for (let i = 0; i < 8; i++) {
        if (note === MC.NOTE.SELECT(i)) {
          this._userEditStrip     = i;
          this._userEditSecondary = isSecondary;
          const cur = this._banks.getChannel(i, isSecondary);
          const idx = this._allChannelPaths.indexOf(cur);
          if (idx >= 0) this._jogCursor = idx;
          this._log(`Editing strip ${i + 1} — turn JOG to select`);
          return;
        }
      }
    }

    if (pressed) this._onButtonPress(note, isSecondary);
    else         this._onButtonRelease(note, isSecondary);
  }

  _onButtonPress(note, isSecondary) {
    // Button map lookup
    const action = this._buttonMap?.getAction(note);
    if (action && action !== 'DEFAULT') {
      this._dispatchAction(action, isSecondary);
      return;
    }

    const N  = MC.NOTE;
    // Channel strip buttons (not remappable via button map — channel-relative)
    for (let i = 0; i < 8; i++) {
      if (note === N.MUTE(i))   { this._mixer.toggleMute(this._banks.getChannel(i, isSecondary));   return; }
      if (note === N.SOLO(i))   { this._mixer.toggleSolo(this._banks.getChannel(i, isSecondary));   return; }
      if (note === N.SELECT(i)) { this._onSelect(i, isSecondary);                                   return; }
      if (note === N.REC(i))    { this._onChannelRec(i, isSecondary);                               return; }
    }

    if (this.debug) this._log(`Unhandled note: 0x${note.toString(16)}`);
  }

  _onButtonRelease(note, isSecondary) {
    // Release-mode AUX/FX: return to main mix on release
    if (!this._auxLocked) {
      const action = this._buttonMap?.getAction(note);
      if (action) {
        if (/^AUX\d+$/.test(action) || /^FX\d+$/.test(action)) {
          this._clearAuxFxMode(); return;
        }
        if (action === 'TALKBACK' && this._talkbackPath) {
          this._mixer.setMute(this._talkbackPath, true); return;
        }
      }
    }
  }

  // ─── Action dispatcher ────────────────────────────────────────────────────────

  _dispatchAction(action, isSecondary) {
    const auxM = action.match(/^AUX(\d+)$/);
    if (auxM) { this._onAuxButton(parseInt(auxM[1], 10) - 1); return; }

    const fxM = action.match(/^FX(\d+)$/);
    if (fxM) { this._onFxButton(parseInt(fxM[1], 10) - 1); return; }

    const mgM = action.match(/^MUTE_GROUP_(\d+)$/);
    if (mgM) { this._mixer.toggleMuteGroup(parseInt(mgM[1], 10) - 1); return; }

    switch (action) {
      case 'BANK_UP':       this._banks.bankUp();    this._emitState(); break;
      case 'BANK_DOWN':     this._banks.bankDown();  this._emitState(); break;
      case 'LAYER_UP':      this._banks.layerUp();   this._emitState(); break;
      case 'LAYER_DOWN':    this._banks.layerDown(); this._emitState(); break;
      case 'MUTE_ALL':      this._mixer.muteAll();    break;
      case 'MUTE_FX':       this._mixer.muteFx();     break;
      case 'CLEAR_MUTE':    this._mixer.clearMute();  break;
      case 'CLEAR_SOLO':    this._mixer.clearSolo();  break;
      case 'TAP_TEMPO':     this._mixer.tapTempo();   break;
      case 'SAVE_USERBANK': this._saveUserGroups();   break;
      case 'PAN_MODE':      this._togglePanMode();    break;
      case 'RECORD_MTK':    this._mixer.setMtkRecord(true);   break;
      case 'RECORD_2TRACK': this._mixer.set2TrackRecord(true); break;
      case 'RECORD_BOTH':
        this._mixer.setMtkRecord(true);
        this._mixer.set2TrackRecord(true);
        break;
      case 'MEDIA_PLAY':
        if (this._isPlaying) {
          this._mixer.mediaStop();
          this._setPlayLed(false);
        } else {
          this._mixer.mediaPlay();
          this._setPlayLed(true);
        }
        break;
      case 'MEDIA_STOP':
        this._mixer.mediaStop();
        this._setPlayLed(false);
        break;
      case 'MEDIA_REWIND':  this._mixer.mediaRewind();  break;
      case 'MEDIA_FORWARD': this._mixer.mediaForward(); break;
      case 'TALKBACK':
        if (this._talkbackPath) this._mixer.setMute(this._talkbackPath, false);
        break;
    }
  }

  // ─── Channel actions ──────────────────────────────────────────────────────────

  _onSelect(stripIndex, isSecondary) {
    const p = this._banks.getChannel(stripIndex, isSecondary);
    if (!p) return;
    this._selectedChannel = p;
    this._refreshSelectLeds();
    if (this._rtaOnSelect) this._mixer.setRta(p, true);
    this._log(`Selected: ${p}`);
    this._emitState();
  }

  _onChannelRec(stripIndex, isSecondary) {
    const p    = this._banks.getChannel(stripIndex, isSecondary);
    const mode = this.config['DefaultChannelRecButton'] || 'rec';
    if (mode === 'phantom') this._mixer.togglePhantom(p);
    else                    this._mixer.toggleRecArm(p);
  }

  // ─── AUX / FX modes ──────────────────────────────────────────────────────────

  _onAuxButton(auxIdx) {
    if (this._auxLocked && this._auxMode === auxIdx) { this._clearAuxFxMode(); return; }
    this._auxMode = auxIdx;
    this._fxMode  = null;
    this._log(`AUX ${auxIdx + 1}`);
    this._refreshAuxFxLeds();
    this._syncFadersToAux(auxIdx);
    this._syncLcd();
    this._emitState();
  }

  _onFxButton(fxIdx) {
    if (this._auxLocked && this._fxMode === fxIdx) { this._clearAuxFxMode(); return; }
    this._fxMode  = fxIdx;
    this._auxMode = null;
    this._log(`FX ${fxIdx + 1}`);
    this._refreshAuxFxLeds();
    this._syncFadersToFx(fxIdx);
    this._syncLcd();
    this._emitState();
  }

  _clearAuxFxMode() {
    this._auxMode = null;
    this._fxMode  = null;
    this._refreshAuxFxLeds();
    this._syncFadersToMain();
    this._emitState();
  }

  _togglePanMode() {
    this._panMode = !this._panMode;
    const s = this._panMode ? MC.LED_ON : MC.LED_OFF;
    this._primary?.setLed(MC.NOTE.PAN_SURROUND, s);
    this._secondary?.setLed(MC.NOTE.PAN_SURROUND, s);
    this._log(`Pan mode: ${this._panMode ? 'ON' : 'OFF'}`);
    this._syncAllKnobRings();
    this._emitState();
  }

  // ─── User bank editing ────────────────────────────────────────────────────────

  _confirmUserEdit(isSecondary) {
    this._userEditActive = false;
    this._primary?.ledOff(MC.NOTE.USER);

    if (this._userEditStrip === null) return;

    const newPath = this._allChannelPaths[this._jogCursor];
    this._banks.setUserChannel(this._banks.layer, this._userEditStrip, newPath);
    this._log(`User bank: layer ${this._banks.layer + 1}, strip ${this._userEditStrip + 1} → ${newPath}`);
    this._userEditStrip = null;

    // Refresh LCD to confirm
    const ctrl     = this._userEditSecondary ? this._secondary : this._primary;
    const channels = this._userEditSecondary
      ? this._banks.secondaryChannels
      : this._banks.primaryChannels;
    this._refreshLcd(ctrl, channels);
  }

  _buildAllChannelPaths() {
    const p = [];
    for (let i = 0; i < 24; i++) p.push(`i.${i}`);
    p.push('l.0', 'l.1', 'p.0', 'p.1');
    for (let i = 0; i < 4; i++) p.push(`f.${i}`);
    for (let i = 0; i < 10; i++) p.push(`a.${i}`);
    for (let i = 0; i < 6; i++) p.push(`v.${i}`);
    p.push('m');
    return p;
  }

  // ─── View groups (Bank V) ─────────────────────────────────────────────────────

  _handleViewGroupUpdate(groupIdx, channelIndices) {
    // Convert flat channel indices to path strings using the mixer's channel order:
    // 0-23 = inputs, 24-25 = line in, 26-27 = player, 28-31 = fx returns,
    // 32-41 = aux masters, 42-47 = VCA groups, 48 = main mix
    const indexToPath = (idx) => {
      if (idx < 24)  return `i.${idx}`;
      if (idx < 26)  return `l.${idx - 24}`;
      if (idx < 28)  return `p.${idx - 26}`;
      if (idx < 32)  return `f.${idx - 28}`;
      if (idx < 42)  return `a.${idx - 32}`;
      if (idx < 48)  return `v.${idx - 42}`;
      if (idx === 48) return 'm';
      return '';
    };

    this._viewGroups[groupIdx] = channelIndices
      .map(idx => indexToPath(parseInt(idx, 10)))
      .filter(Boolean);

    this._banks.setViewGroups(this._viewGroups);
  }

  // ─── Controller refresh ──────────────────────────────────────────────────────

  async _refreshController(ctrl, isSecondary) {
    if (!ctrl?.connected) return;
    const channels = isSecondary
      ? this._banks.secondaryChannels
      : this._banks.primaryChannels;

    // Faders (0-7 = channel strips, 8 = master)
    for (let i = 0; i < 8; i++) {
      const p = channels[i];
      await ctrl.setFader(i, p ? this._getFaderLevel(p) : 0);
    }
    await ctrl.setFader(8, this._getCh('m').fader || 0);

    // Channel strip LEDs
    for (let i = 0; i < 8; i++) {
      const p = channels[i];
      const s = this._getCh(p);
      await ctrl.setLed(MC.NOTE.MUTE(i),   s.mute   ? MC.LED_ON : MC.LED_OFF);
      await ctrl.setLed(MC.NOTE.SOLO(i),   s.solo   ? MC.LED_ON : MC.LED_OFF);
      await ctrl.setLed(MC.NOTE.SELECT(i), p === this._selectedChannel ? MC.LED_ON : MC.LED_OFF);
      await ctrl.setLed(MC.NOTE.REC(i),    this._getRecLedState(p) ? MC.LED_ON : MC.LED_OFF);
    }

    // LCD
    await this._refreshLcd(ctrl, channels);
    this._syncMainDisplay();

    // V-Pot rings
    for (let i = 0; i < 8; i++) {
      const p   = channels[i];
      const val = this._panMode
        ? (this._getCh(p).pan ?? 0.5)
        : (this._getCh(p).gain || 0);
      const pos = Math.max(1, Math.min(11, Math.round(val * 10) + 1));
      await ctrl.setVpotLed(i, this._panMode ? 1 : 0, pos);
    }

    // Mode LEDs
    this._refreshAuxFxLeds();
    ctrl.setLed(MC.NOTE.PAN_SURROUND, this._panMode ? MC.LED_ON : MC.LED_OFF);
  }

  async _refreshLcd(ctrl, channels) {
    if (!ctrl?.connected) return;
    const tops = [], bots = [];

    for (let i = 0; i < 8; i++) {
      const p     = channels[i];
      const state = this._getCh(p);
      const name  = ((state.name || p || '').slice(0, 6)).padEnd(6, ' ');

      if (this._auxMode !== null && p) {
        const v = this._mixer?.getState(`${p}.aux.${this._auxMode}.value`) || 0;
        tops.push(`${name} `);
        bots.push((`A${this._auxMode + 1}:${Math.round(v * 100)}`).padEnd(7).slice(0, 7));
      } else if (this._fxMode !== null && p) {
        const v = this._mixer?.getState(`${p}.fx.${this._fxMode}.value`) || 0;
        tops.push(`${name} `);
        bots.push((`F${this._fxMode + 1}:${Math.round(v * 100)}`).padEnd(7).slice(0, 7));
      } else {
        tops.push(`${name} `);
        bots.push(CMap.channelLabel(p));
      }
    }
    await ctrl.setAllLcd(tops, bots);
  }

  _refreshAuxFxLeds() {
    const N = MC.NOTE;
    const fKeys = [N.F1, N.F2, N.F3, N.F4, N.F5, N.F6, N.F7, N.F8];
    const fxKeys = [N.SHIFT, N.OPTION, N.CONTROL, N.ALT];

    for (let i = 0; i < 8; i++) {
      const s = this._auxMode === i ? MC.LED_ON : MC.LED_OFF;
      this._primary?.setLed(fKeys[i], s);
      this._secondary?.setLed(fKeys[i], s);
    }
    for (let i = 0; i < 4; i++) {
      const s = this._fxMode === i ? MC.LED_ON : MC.LED_OFF;
      this._primary?.setLed(fxKeys[i], s);
      this._secondary?.setLed(fxKeys[i], s);
    }
  }

  _refreshSelectLeds() {
    [
      { channels: this._banks.primaryChannels,   ctrl: this._primary   },
      { channels: this._banks.secondaryChannels, ctrl: this._secondary },
    ].forEach(({ channels, ctrl }) => {
      if (!ctrl?.connected) return;
      channels.forEach((p, i) => {
        ctrl.setLed(MC.NOTE.SELECT(i), p === this._selectedChannel ? MC.LED_ON : MC.LED_OFF);
      });
    });
  }

  // ─── Sync helpers ─────────────────────────────────────────────────────────────

  _setPlayLed(playing) {
    this._isPlaying = playing;
    const s = playing ? MC.LED_ON : MC.LED_OFF;
    this._primary?.setLed(MC.NOTE.PLAY, s);
    this._secondary?.setLed(MC.NOTE.PLAY, s);
  }

  _syncFaderToController(p, level) {
    if (p === 'm') {
      this._primary?.setFader(8, level);
      this._secondary?.setFader(8, level);
      return;
    }
    this._forStrip(p, (i, ctrl) => ctrl.setFader(i, level));
  }

  _syncMuteLed(p, muted) {
    this._forStrip(p, (i, ctrl) =>
      ctrl.setLed(MC.NOTE.MUTE(i), muted ? MC.LED_ON : MC.LED_OFF));
  }

  _syncSoloLed(p, soloed) {
    this._forStrip(p, (i, ctrl) =>
      ctrl.setLed(MC.NOTE.SOLO(i), soloed ? MC.LED_ON : MC.LED_OFF));
  }

  _syncRecLed(p, on) {
    this._forStrip(p, (i, ctrl) =>
      ctrl.setLed(MC.NOTE.REC(i), on ? MC.LED_ON : MC.LED_OFF));
  }

  _syncKnobRing(p, value) {
    // MC V-Pot position 0 = all LEDs off; positions 1–11 are the visible range.
    // Map 0.0→1, 1.0→11 so the minimum gain always lights the leftmost LED.
    const pos = Math.max(1, Math.min(11, Math.round(value * 10) + 1));
    this._forStrip(p, (i, ctrl) =>
      ctrl.setVpotLed(i, this._panMode ? 1 : 0, pos));
  }

  _syncAllKnobRings() {
    this._forAllStrips((p, i, ctrl) => {
      const val = this._panMode
        ? (this._getCh(p).pan ?? 0.5)
        : (this._getCh(p).gain || 0);
      const pos = Math.max(1, Math.min(11, Math.round(val * 10) + 1));
      ctrl.setVpotLed(i, this._panMode ? 1 : 0, pos);
    });
  }

  _syncLcd() {
    if (this._primary?.connected)
      this._refreshLcd(this._primary, this._banks.primaryChannels);
    if (this._secondary?.connected)
      this._refreshLcd(this._secondary, this._banks.secondaryChannels);
  }

  _syncMainDisplay() {
    // Left side: bank letter + layer number (e.g. "I1", "U3", "V1")
    const bankStr = `${this._banks.bank}${this._banks.layer + 1}`;
    // Right side: tap tempo BPM (e.g. "120BPM") or blank
    const bpmStr  = this._tapTempo > 0 ? `${this._tapTempo}BPM` : '';
    // Compose into 12 chars: bank left-justified, BPM right-justified
    const text = bankStr.padEnd(12 - bpmStr.length, ' ') + bpmStr;
    this._primary?.setMainDisplay(text);
    this._secondary?.setMainDisplay(text);
  }

  _syncFadersToAux(auxIdx) {
    this._forAllStrips((p, i, ctrl) => {
      ctrl.setFader(i, p ? this._mixer.getState(`${p}.aux.${auxIdx}.value`) : 0);
    });
  }

  _syncFadersToFx(fxIdx) {
    this._forAllStrips((p, i, ctrl) => {
      ctrl.setFader(i, p ? this._mixer.getState(`${p}.fx.${fxIdx}.value`) : 0);
    });
  }

  _syncFadersToMain() {
    this._forAllStrips((p, i, ctrl) => {
      ctrl.setFader(i, p ? this._getFaderLevel(p) : 0);
    });
    this._syncLcd();
  }

  _forAllStrips(fn) {
    [
      { channels: this._banks.primaryChannels,   ctrl: this._primary   },
      { channels: this._banks.secondaryChannels, ctrl: this._secondary },
    ].forEach(({ channels, ctrl }) => {
      if (!ctrl?.connected) return;
      channels.forEach((p, i) => fn(p, i, ctrl));
    });
  }

  _forStrip(chPath, fn) {
    [
      { channels: this._banks.primaryChannels,   ctrl: this._primary   },
      { channels: this._banks.secondaryChannels, ctrl: this._secondary },
    ].forEach(({ channels, ctrl }) => {
      if (!ctrl?.connected) return;
      const i = channels.indexOf(chPath);
      if (i >= 0) fn(i, ctrl);
    });
  }

  // ─── Bank change ──────────────────────────────────────────────────────────────

  _onChannelsChanged(primary, secondary) {
    this._log(`Bank ${this._banks.bank}  Layer ${this._banks.layer + 1}`);
    if (this._primary?.connected)
      this._refreshController(this._primary, false).catch(err => this._log('refresh error:', err.message));
    if (this._secondary?.connected)
      this._refreshController(this._secondary, true).catch(err => this._log('refresh error:', err.message));
    this._syncMainDisplay();
    this._emitState();
  }

  // ─── VU meters ───────────────────────────────────────────────────────────────

  _meterTick() {
    if (!this._primary?.connected) return;

    const send = (channels, ctrl) => {
      if (!ctrl?.connected) return;
      channels.forEach((p, i) => {
        if (!p) return;
        const lev = this._vuLevels[p] || 0;
        // Skip sending silent meters to reduce MIDI spam when there's no audio
        if (lev > 0) ctrl.setMeter(i, lev);
      });
    };

    send(this._banks.primaryChannels,   this._primary);
    send(this._banks.secondaryChannels, this._secondary);
  }

  // ─── State helpers ────────────────────────────────────────────────────────────

  _getCh(p) {
    return (p && this._channelState[p]) || {};
  }

  _updateCh(p, update) {
    if (!p) return;
    this._channelState[p] = Object.assign(this._channelState[p] || {}, update);
  }

  _getFaderLevel(p) {
    if (!p) return 0;
    if (this._auxMode !== null) return this._mixer?.getState(`${p}.aux.${this._auxMode}`) || 0;
    if (this._fxMode  !== null) return this._mixer?.getState(`${p}.fx.${this._fxMode}`) || 0;
    return this._getCh(p).fader || 0;
  }

  _getRecLedState(p) {
    if (!p) return false;
    const s = this._getCh(p);
    return this.config['DefaultChannelRecButton'] === 'phantom' ? s.phantom : s.recArm;
  }

  // ─── ViewGroups persistence ──────────────────────────────────────────────────

  _loadViewGroups() {
    for (const f of [
      path.join(app.getPath('userData'), 'ViewGroups.json'),
      path.join(__dirname, '..', 'ViewGroups.json'),
    ]) {
      if (fs.existsSync(f)) {
        try { return JSON.parse(fs.readFileSync(f, 'utf8')); } catch {}
      }
    }
    return null;
  }

  _saveUserGroups() {
    const file = path.join(app.getPath('userData'), 'ViewGroups.json');
    fs.writeFileSync(file, JSON.stringify(this._banks.getUserGroups(), null, 2), 'utf8');
    this._log('ViewGroups saved');
    // Brief blink to confirm
    this._primary?.ledBlink(MC.NOTE.GLOBAL_VIEW);
    setTimeout(() => this._primary?.ledOff(MC.NOTE.GLOBAL_VIEW), 600);
  }

  // ─── Status / state emission ─────────────────────────────────────────────────

  _emitStatus() {
    this.emit('status', {
      midi:  this._primary?.connected   ? 'connected' : 'disconnected',
      midi2: this._secondary?.connected ? 'connected' : 'disconnected',
      mixer: this._mixer?.connected     ? 'connected' : 'disconnected',
    });
  }

  _emitState() {
    const bs = this._banks.toStatus();
    this.emit('state', {
      ...bs,
      auxMode:  this._auxMode,
      fxMode:   this._fxMode,
      panMode:  this._panMode,
      selected: this._selectedChannel,
      channelDetails: bs.primaryChannels.map(p => ({ path: p, ...this._getCh(p) })),
    });
  }

  _log(...args) {
    const msg = args.join(' ');
    console.log('[Bridge]', msg);
    this.emit('log', msg);
  }
}

module.exports = Bridge;
