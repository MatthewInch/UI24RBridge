# UI24R Bridge — JavaScript/Electron Edition

Connects a **Mackie Control** compatible MIDI controller to a **Soundcraft UI24R** mixer.

## Download

| Platform | File |
|----------|------|
| **Windows** | `.exe` installer |
| **Linux** | `.AppImage` (run anywhere) or `.deb` (Debian/Ubuntu) |
| **macOS** | `.dmg` |

👉 **[Download the latest release](https://github.com/MatthewInch/UI24RBridge/releases/latest)**

---

This is a full rewrite of the original [.NET UI24RBridge](https://github.com/MatthewInch/UI24RBridge/tree/master) in JavaScript/Electron, replacing the unstable .NET MIDI stack with [JZZ.js](https://jazz-soft.net/doc/JZZ/) and communicating with the mixer over a plain TCP socket (the UI24R's native protocol).

## Requirements

- [Node.js](https://nodejs.org/) 18 or later
- npm 9 or later
- A Soundcraft UI24R mixer reachable on your local network
- A Mackie Control (MCU) compatible MIDI controller — tested with **Behringer X-Touch**

## Installation

```bash
git clone https://github.com/MatthewInch/UI24RBridge.git
cd UI24RBridge
npm install
```

## Running

```bash
npm start
```

On first launch:

1. Enter the **Mixer IP** (e.g. `192.168.1.100`) in the URL field
2. Select your **MIDI controller** input and output ports
3. Click **Start Bridge**

The bridge connects automatically on the next launch if the config has been saved.

## Configuration

Settings are saved via the UI. The underlying file is `appsettings.json` in the app's user-data folder.

| Setting | Description |
|---------|-------------|
| **Mixer URL** | IP or hostname of the UI24R (e.g. `192.168.1.100`) |
| **MIDI Input / Output** | Primary controller ports |
| **Second MIDI Input / Output** | Optional secondary / extender controller ports |
| **Secondary is Extender** | Enable if the second controller is an MCU extender unit |
| **Channel Rec button** | `rec` = MTK arm, `phantom` = 48V phantom power |
| **Main Rec button** | Which recorders the main REC button arms |
| **AUX button behavior** | `Release` = hold to switch (momentary), `Lock` = toggle |
| **Start Bank** | Which bank is active on startup (I / U / V) |
| **Talkback channel** | Input channel number used as talkback mic (Scrub button) |
| **RTA on Select** | Enable RTA display on the mixer when selecting a channel |
| **Primary / Secondary Channel Start** | `0` = strips 1–8, `1` = strips 9–16 (for extender offset) |
| **Sync ID** | Optional identifier for multi-bridge setups |
| **Debug Messages** | Verbose MIDI and mixer protocol logging |

## Controller Mapping

### Channel strips (per strip 1–8)

| Controller | Action |
|-----------|--------|
| Fader | Channel fader level (or AUX/FX send level in AUX/FX mode) |
| V-Pot (knob) | Gain (normal mode) or Pan (pan mode) |
| MUTE button | Toggle mute |
| SOLO button | Toggle solo |
| SELECT button | Select channel (highlights on LCD) |
| REC button | Toggle MTK rec-arm or 48V phantom (configurable) |
| Master fader (fader 9) | Main mix fader |

### Bank and Layer navigation

| Button | Action |
|--------|--------|
| BANK LEFT / RIGHT | Switch bank (I → U → V) |
| CHANNEL LEFT / RIGHT | Switch layer within current bank |

**Bank I** — Fixed channel layout, 6 layers:
- Layer 1: Inputs 1–8
- Layer 2: Inputs 9–16
- Layer 3: Inputs 17–24
- Layer 4: Line In L/R, Player L/R, FX Returns 1–4
- Layer 5: AUX masters 1–8
- Layer 6: AUX masters 9–10, VCA groups 1–6

**Bank U** — User-defined layout. Hold USER + press SELECT on a strip, then turn the JOG wheel to assign a channel. Release USER to confirm.

**Bank V** — View Groups from the mixer (requires view groups to be configured on the mixer).

### AUX and FX sends

| Button | Action |
|--------|--------|
| F1–F8 | AUX 1–8 send mode — faders show and control per-channel AUX send levels |
| SHIFT / OPTION / CONTROL / ALT | FX 1–4 send mode — faders show FX send levels |

In **Release** mode (default): hold the button to enter the mode, release to return to main mix.  
In **Lock** mode: press to toggle the mode on/off.

### Pan mode

| Button | Action |
|--------|--------|
| PAN/SURROUND | Toggle pan mode — V-Pot knobs control channel pan instead of gain |

V-Pot LED ring shows pan position (boost/cut mode, center = center pan).

### Transport

| Button | Action |
|--------|--------|
| PLAY | Play (toggles play/stop) — LED shows playback state |
| STOP | Stop playback |
| REWIND | Previous track |
| FORWARD | Next track |

### Other functions (configurable via ButtonsDefault.json)

| Action name | Description |
|-------------|-------------|
| `AUX1`–`AUX8` | Enter AUX send mode |
| `FX1`–`FX4` | Enter FX send mode |
| `MUTE_GROUP_1`–`MUTE_GROUP_6` | Toggle mute group |
| `MUTE_ALL` | Mute all channels |
| `MUTE_FX` | Mute FX |
| `CLEAR_MUTE` | Clear all mutes |
| `CLEAR_SOLO` | Clear all solos |
| `TAP_TEMPO` | Tap tempo |
| `PAN_MODE` | Toggle pan mode |
| `TALKBACK` | Hold for talkback (unmutes the configured talkback channel) |
| `RECORD_MTK` | Start MTK recording |
| `RECORD_2TRACK` | Start 2-track recording |
| `RECORD_BOTH` | Start both recorders |
| `MEDIA_PLAY` | Play/stop toggle |
| `MEDIA_STOP` | Stop |
| `MEDIA_REWIND` | Previous track |
| `MEDIA_FORWARD` | Next track |
| `BANK_UP` / `BANK_DOWN` | Navigate banks |
| `LAYER_UP` / `LAYER_DOWN` | Navigate layers |
| `SAVE_USERBANK` | Save current user bank layout |

Button mapping is defined in `ButtonsDefault.json` in the app's user-data folder.

## Project Structure

```
ui24r-bridge/
├── main.js               Electron main process, IPC handlers
├── preload.js            Context bridge (IPC API exposed to renderer)
├── renderer/
│   ├── index.html        App UI
│   └── renderer.js       UI logic
├── src/
│   ├── bridge.js         Central coordinator — wires MIDI ↔ mixer
│   ├── midiController.js JZZ.js MIDI I/O + Mackie Control decoding
│   ├── mixerConnection.js TCP client for the UI24R native protocol
│   ├── bankManager.js    Bank I/U/V + layer state machine
│   ├── channelMap.js     Channel path definitions and LCD label helpers
│   ├── mcProtocol.js     Mackie Control constants and SysEx builders
│   ├── buttonMap.js      Configurable button → action mapping
│   ├── rtaProcessor.js   RTA spectrum data processor
│   └── config.js         Config load/save
├── ButtonsDefault.json   Default button-to-action mapping
├── ViewGroups.json       Default user bank channel layout
└── package.json
```

## Building a distributable

```bash
npm run build:win    # Windows installer (NSIS)
npm run build:linux  # AppImage + .deb
npm run build:mac    # macOS .dmg
npm run build        # all platforms
```

Output goes to `dist/`.

## Migrating from the .NET version

The JavaScript version is a drop-in replacement with the same feature set and the same `appsettings.json` configuration keys. The main differences:

- Works on Windows, Linux, and macOS without any runtime installation beyond Node.js
- No native MIDI bindings — uses JZZ.js (pure JavaScript)
- Connects to the mixer using the UI24R native TCP protocol rather than a WebSocket library
- Configuration and logs are in the Electron app UI instead of a system tray app

## License

MIT
