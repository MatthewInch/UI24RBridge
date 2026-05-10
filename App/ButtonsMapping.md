# X-Touch Universal Control Surface — MIDI Button Reference

All button-press messages are sent as `9n 00 7f` (note-on) / `9n 00 00` (note-off) where `n`
is the channel (0 for channel 1). The byte in the table is the note number (second byte).

The `Buttons*.json` files use these bytes as values and `ButtonsEnum` names as keys.
Only include the functions you want active — unrecognised keys are skipped.

## Channel strip (repeated × 8, offset by channel index)

| MIDI byte       | Physical button |
|-----------------|-----------------|
| 0x00 + ch (0–7) | REC             |
| 0x08 + ch (0–7) | SOLO            |
| 0x10 + ch (0–7) | MUTE            |
| 0x18 + ch (0–7) | SELECT          |

So channel 1 REC = 0x00, channel 2 REC = 0x01 … channel 1 SOLO = 0x08, etc.
The `Ch1*` keys in the JSON refer to the base offset (channel 1).

## Encoder assign

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x28      | TRACK           |
| 0x2a      | PAN/SURROUND    |
| 0x2c      | EQ              |
| 0x29      | SEND            |
| 0x2b      | PLUG-IN         |
| 0x2d      | INSTR           |

## 7-segment display area

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x34      | DISPLAY (NAME/VALUE) |
| 0x35      | SMPTE/BEATS     |

## Global view section

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x33      | GLOBAL VIEW     |
| 0x3e      | MIDI TRACKS     |
| 0x3f      | INPUTS          |
| 0x40      | AUDIO TRACKS    |
| 0x41      | AUDIO INST      |
| 0x42      | AUX             |
| 0x43      | BUSES           |
| 0x44      | OUTPUTS         |
| 0x45      | USER            |

## Function

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x36      | F1              |
| 0x37      | F2              |
| 0x38      | F3              |
| 0x39      | F4              |
| 0x3a      | F5              |
| 0x3b      | F6              |
| 0x3c      | F7              |
| 0x3d      | F8              |

## Modify

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x46      | SHIFT           |
| 0x47      | OPTION          |
| 0x48      | CONTROL         |
| 0x49      | ALT/CMD         |

## Automation

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x4a      | READ/OFF        |
| 0x4b      | WRITE           |
| 0x4c      | TRIM            |
| 0x4d      | TOUCH           |
| 0x4e      | LATCH           |
| 0x4f      | GROUP           |

## Utility

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x50      | SAVE            |
| 0x51      | UNDO            |
| 0x52      | CANCEL          |
| 0x53      | ENTER           |

## Transport
| MIDI byte | Physical button |
|-----------|-----------------|
| 0x54      | MARKER          |
| 0x55      | NUDGE           |
| 0x56      | CYCLE           |
| 0x57      | DROP            |
| 0x58      | REPLACE         |
| 0x59      | CLICK           |
| 0x5a      | SOLO            |

## Playback

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x5b      | REWIND ◀◀       |
| 0x5c      | FAST FWD ▶▶     |
| 0x5d      | STOP ■          |
| 0x5e      | PLAY ▶          |
| 0x5f      | RECORD ●        |

## Fader bank / channel navigation

| MIDI byte | Physical button   |
|-----------|-------------------|
| 0x2e      | FADER BANK ◀      |
| 0x2f      | FADER BANK ▶      |
| 0x30      | CHANNEL ◀         |
| 0x31      | CHANNEL ▶         |

## Cursor / jog

| MIDI byte | Physical button |
|-----------|-----------------|
| 0x60      | UP ▲            |
| 0x61      | DOWN ▼          |
| 0x62      | LEFT ◀          |
| 0x63      | RIGHT ▶         |
| 0x64      | CENTER (zoom)   |
| 0x65      | SCRUB           |
