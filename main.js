'use strict';

const { app, BrowserWindow, ipcMain, Tray, Menu, nativeImage } = require('electron');
const path = require('path');
const fs = require('fs');

const Config = require('./src/config');
const Bridge = require('./src/bridge');

let mainWindow = null;
let tray = null;
let bridge = null;

// ─── Window ──────────────────────────────────────────────────────────────────

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 900,
    height: 640,
    minWidth: 700,
    minHeight: 500,
    title: 'UI24R Bridge',
    backgroundColor: '#0f0f12',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));
  mainWindow.on('closed', () => { mainWindow = null; });
}

// ─── Tray ─────────────────────────────────────────────────────────────────────

function createTray() {
  // Minimal inline 16x16 PNG icon as base64 so no external file is needed
  const icon = nativeImage.createEmpty();
  tray = new Tray(icon);
  tray.setToolTip('UI24R Bridge');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Show', click: () => mainWindow && mainWindow.show() },
    { type: 'separator' },
    { label: 'Quit', click: () => app.quit() },
  ]));
  tray.on('click', () => mainWindow && mainWindow.show());
}

// ─── Bridge lifecycle ─────────────────────────────────────────────────────────

async function startBridge() {
  const config = Config.load();
  bridge = new Bridge(config);

  bridge.on('status', (status) => {
    sendToRenderer('bridge:status', status);
  });
  bridge.on('log', (msg) => {
    sendToRenderer('bridge:log', msg);
  });
  bridge.on('state', (state) => {
    sendToRenderer('bridge:state', state);
  });
  bridge.on('stopped', () => {
    bridge = null;
    sendToRenderer('bridge:status', { midi: 'disconnected', mixer: 'disconnected' });
    sendToRenderer('bridge:stopped', {});
  });

  await bridge.start();
}

function stopBridge() {
  if (bridge) {
    bridge.stop();
    bridge = null;
  }
}

// ─── IPC ─────────────────────────────────────────────────────────────────────

function sendToRenderer(channel, data) {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send(channel, data);
  }
}

ipcMain.handle('config:load', () => Config.load());
ipcMain.handle('config:save', (_event, cfg) => Config.save(cfg));
ipcMain.handle('config:path', () => Config.configPath());

ipcMain.handle('midi:list', async () => {
  const JZZ = require('jzz');
  await JZZ();
  const info = JZZ.info();
  return {
    inputs: (info.inputs || []).map(p => p.name),
    outputs: (info.outputs || []).map(p => p.name),
  };
});

ipcMain.handle('bridge:isRunning', () => bridge !== null);

ipcMain.handle('bridge:start', async () => {
  stopBridge();
  await startBridge();
  return { ok: true };
});

ipcMain.handle('bridge:stop', () => {
  stopBridge();
  sendToRenderer('bridge:status', { midi: 'disconnected', mixer: 'disconnected' });
  return { ok: true };
});

ipcMain.handle('viewgroups:load', () => {
  const file = path.join(app.getPath('userData'), 'ViewGroups.json');
  if (fs.existsSync(file)) return JSON.parse(fs.readFileSync(file, 'utf8'));
  // fallback to app dir
  const fallback = path.join(__dirname, 'ViewGroups.json');
  if (fs.existsSync(fallback)) return JSON.parse(fs.readFileSync(fallback, 'utf8'));
  return null;
});

ipcMain.handle('viewgroups:save', (_event, data) => {
  const file = path.join(app.getPath('userData'), 'ViewGroups.json');
  fs.writeFileSync(file, JSON.stringify(data, null, 2), 'utf8');
  return { ok: true };
});

// ─── App lifecycle ────────────────────────────────────────────────────────────

app.whenReady().then(async () => {
  createWindow();
  createTray();
  // Auto-start if config exists
  if (Config.exists()) {
    await startBridge();
  }
});

app.on('window-all-closed', () => {
  // Keep running in tray on all platforms
  // app.quit() only when tray quit is used
});

app.on('activate', () => {
  if (mainWindow === null) createWindow();
});

app.on('before-quit', () => {
  stopBridge();
});
