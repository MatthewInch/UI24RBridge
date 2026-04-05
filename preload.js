'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('bridge', {
  // Config
  loadConfig: () => ipcRenderer.invoke('config:load'),
  saveConfig: (cfg) => ipcRenderer.invoke('config:save', cfg),
  configPath: () => ipcRenderer.invoke('config:path'),

  // MIDI devices
  listMidi: () => ipcRenderer.invoke('midi:list'),

  // Bridge control
  isRunning: () => ipcRenderer.invoke('bridge:isRunning'),
  start: () => ipcRenderer.invoke('bridge:start'),
  stop: () => ipcRenderer.invoke('bridge:stop'),

  // ViewGroups persistence
  loadViewGroups: () => ipcRenderer.invoke('viewgroups:load'),
  saveViewGroups: (data) => ipcRenderer.invoke('viewgroups:save', data),

  // Events from main → renderer
  onStatus:  (cb) => ipcRenderer.on('bridge:status',  (_e, v) => cb(v)),
  onLog:     (cb) => ipcRenderer.on('bridge:log',     (_e, v) => cb(v)),
  onState:   (cb) => ipcRenderer.on('bridge:state',   (_e, v) => cb(v)),
  onStopped: (cb) => ipcRenderer.on('bridge:stopped', (_e, v) => cb(v)),

  // Remove listeners
  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),
});
