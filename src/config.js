'use strict';

const fs = require('fs');
const path = require('path');
const { app } = require('electron');

// Config lives in user data dir (persists across updates) with fallback to app dir
function configPath() {
  return path.join(app.getPath('userData'), 'appsettings.json');
}

const DEFAULTS = {
  'UI24R-Url': 'ws://192.168.5.2',
  'MIDI-Input-Name': '',
  'MIDI-Output-Name': '',
  'MIDI-Input-Name-Second': '',
  'MIDI-Output-Name-Second': '',
  'PrimaryIsExtender': 'false',
  'SecondaryIsExtender': 'true',
  'PrimaryChannelStart': '0',
  'SecondaryChannelStart': '1',
  'Protocol': 'MC',
  'SyncID': '',
  'DefaultRecButton': '2TrackAndMTK',
  'DefaultChannelRecButton': 'rec',
  'AuxButtonBehavior': 'Release',
  'PrimaryButtons': 'ButtonsDefault.json',
  'StartBank': '0',
  'TalkBack': '',
  'RtaOnWhenSelect': 'false',
  'DebugMessages': 'false',
};

function exists() {
  const p = configPath();
  if (fs.existsSync(p)) return true;
  // also check app dir
  const fallback = path.join(__dirname, '..', 'appsettings.json');
  return fs.existsSync(fallback);
}

function load() {
  let raw = {};
  const p = configPath();
  if (fs.existsSync(p)) {
    raw = JSON.parse(fs.readFileSync(p, 'utf8'));
  } else {
    const fallback = path.join(__dirname, '..', 'appsettings.json');
    if (fs.existsSync(fallback)) {
      raw = JSON.parse(fs.readFileSync(fallback, 'utf8'));
    }
  }
  return Object.assign({}, DEFAULTS, raw);
}

function save(cfg) {
  const p = configPath();
  fs.mkdirSync(path.dirname(p), { recursive: true });
  fs.writeFileSync(p, JSON.stringify(cfg, null, 2), 'utf8');
}

module.exports = { load, save, exists, configPath, DEFAULTS };
