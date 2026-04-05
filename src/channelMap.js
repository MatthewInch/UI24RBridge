'use strict';

/**
 * UI24R channel path prefixes.
 * These correspond to the SETD^<path>.mix^<val> protocol commands.
 *
 * i   = input channels  (i.0 … i.23)
 * l   = line inputs      (l.0, l.1)
 * p   = player          (p.0, p.1)
 * f   = FX returns       (f.0 … f.3)
 * a   = AUX masters      (a.0 … a.9)
 * v   = VCA groups       (v.0 … v.5)
 * m   = main mix
 */

// Bank I — fixed layout, 6 layers × 8 channels
const BANK_I = [
  // Layer 0: Input 1-8
  ['i.0','i.1','i.2','i.3','i.4','i.5','i.6','i.7'],
  // Layer 1: Input 9-16
  ['i.8','i.9','i.10','i.11','i.12','i.13','i.14','i.15'],
  // Layer 2: Input 17-24
  ['i.16','i.17','i.18','i.19','i.20','i.21','i.22','i.23'],
  // Layer 3: Line in L/R, Player L/R, FX returns 1-4
  ['l.0','l.1','p.0','p.1','f.0','f.1','f.2','f.3'],
  // Layer 4: AUX 1-8
  ['a.0','a.1','a.2','a.3','a.4','a.5','a.6','a.7'],
  // Layer 5: AUX 9-10, VCA 1-6
  ['a.8','a.9','v.0','v.1','v.2','v.3','v.4','v.5'],
];

const NUM_LAYERS = 6;
const CHANNELS_PER_LAYER = 8;

/**
 * Build the default ViewGroups structure (Bank U defaults to Bank I)
 */
function defaultViewGroups() {
  return BANK_I.map(layer => [...layer]);
}

/**
 * Resolve the channel paths for a given bank/layer, considering
 * the channelStart offset (0 or 1 → shift by 8 channels for extender).
 *
 * @param {string} bank       'I' | 'U' | 'V'
 * @param {number} layer      0-5
 * @param {number} start      0 or 1 (PrimaryChannelStart / SecondaryChannelStart)
 * @param {Array}  userGroups ViewGroups (for bank U)
 * @param {Array}  viewGroups mixer global views (for bank V), array of string[]
 * @returns {string[]}        8 channel path strings (or '' for empty slots)
 */
function resolveChannels(bank, layer, start, userGroups, viewGroups) {
  let base;
  if (bank === 'I') {
    base = BANK_I[layer] || [];
  } else if (bank === 'U') {
    base = (userGroups && userGroups[layer]) ? userGroups[layer] : BANK_I[layer] || [];
  } else if (bank === 'V') {
    const view = (viewGroups && viewGroups[layer]) ? viewGroups[layer] : [];
    // Each view may have more than 8 channels; take 8 starting at offset
    base = view.slice(start * 8, start * 8 + 8);
  } else {
    base = [];
  }

  // Apply channelStart offset for banks I and U
  if ((bank === 'I' || bank === 'U') && start === 1) {
    // Secondary controller shows channels 9-16 in layer 0, etc.
    // For these banks the offset means we shift by 8 within the same layer
    // (the second controller shows the next 8 channels)
    const nextBase = BANK_I[layer] || [];
    base = nextBase; // already resolved above; extender offset is handled by channelStart
    // For extender: show channels [8..15] of the same logical layer
    // We extend the channel list to 16 and slice the second 8
    const extended = extendedLayer(bank, layer, userGroups);
    base = extended.slice(start * 8, start * 8 + 8);
  }

  // Pad to 8
  const result = [];
  for (let i = 0; i < 8; i++) {
    result.push(base[i] || '');
  }
  return result;
}

/**
 * Returns up to 16 channels for a layer (for dual-controller setups)
 */
function extendedLayer(bank, layer, userGroups) {
  if (bank === 'I') return BANK_I[layer] || [];
  if (bank === 'U') return (userGroups && userGroups[layer]) ? userGroups[layer] : BANK_I[layer] || [];
  return [];
}

/**
 * Channel display name (short label for LCD)
 */
function channelLabel(path) {
  if (!path) return '        ';
  const [type, idx] = path.split('.');
  const n = parseInt(idx, 10) + 1;
  switch (type) {
    case 'i': return `In ${n}   `.slice(0, 7);
    case 'l': return n === 1 ? 'Line L ' : 'Line R ';
    case 'p': return n === 1 ? 'Ply L  ' : 'Ply R  ';
    case 'f': return `FX${n}    `.slice(0, 7);
    case 'a': return `Aux ${n}  `.slice(0, 7);
    case 'v': return `VCA ${n} `.slice(0, 7);
    case 'm': return 'Main   ';
    default:  return path.slice(0, 7).padEnd(7);
  }
}

/**
 * AUX send path: for channel `ch` sending to aux `auxIdx`
 * e.g. i.0 → aux 0 send: "i.0.aux.0"
 */
function auxSendPath(channelPath, auxIdx) {
  if (!channelPath) return null;
  return `${channelPath}.aux.${auxIdx}`;
}

/**
 * FX send path: for channel `ch` sending to fx `fxIdx`
 */
function fxSendPath(channelPath, fxIdx) {
  if (!channelPath) return null;
  return `${channelPath}.fx.${fxIdx}`;
}

module.exports = {
  BANK_I,
  NUM_LAYERS,
  CHANNELS_PER_LAYER,
  defaultViewGroups,
  resolveChannels,
  extendedLayer,
  channelLabel,
  auxSendPath,
  fxSendPath,
};
