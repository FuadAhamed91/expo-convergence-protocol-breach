// Expo Convergence audio synthesizer.
// Generates every music loop and sound effect as 16-bit WAV into Assets/Resources/ExpoAudio.
// Run:  node Tools/AudioSynth/synth.js
// Pure JavaScript, no dependencies, deterministic (seeded noise).

'use strict';
const fs = require('fs');
const path = require('path');

const SR = 44100;
const OUT = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'ExpoAudio');
fs.mkdirSync(OUT, { recursive: true });

// ------------------------------------------------------------------ utilities

let seed = 1337;
function rand() { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; }
function noise() { return rand() * 2 - 1; }
const TAU = Math.PI * 2;
const mtof = (m) => 440 * Math.pow(2, (m - 69) / 12);
const clamp = (x, a, b) => Math.max(a, Math.min(b, x));
const lerp = (a, b, t) => a + (b - a) * t;

function buffer(seconds, channels = 2) {
  const n = Math.ceil(seconds * SR);
  const ch = [];
  for (let c = 0; c < channels; c++) ch.push(new Float32Array(n));
  return ch;
}

const LOOPS = new Set(['music_explore', 'music_tension', 'amb_city', 'alarm', 'terminal']);

function writeWav(name, ch) {
  const channels = ch.length, n = ch[0].length;
  // DC blocker (~10 Hz high-pass) so drums and saws don't leave an offset.
  for (const c of ch) {
    let x1 = 0, y1 = 0;
    const R = 1 - TAU * 10 / SR;
    for (let i = 0; i < n; i++) { const y = c[i] - x1 + R * y1; x1 = c[i]; y1 = y; c[i] = y; }
  }
  // One-shots fade out over their last 40 ms so truncated tails never click.
  if (!LOOPS.has(name)) {
    const f = Math.min(n, Math.floor(0.04 * SR));
    for (const c of ch) for (let i = 0; i < f; i++) c[n - 1 - i] *= i / f;
  }
  // Normalize to -1 dBFS with a gentle soft clip.
  let peak = 0;
  for (const c of ch) for (let i = 0; i < n; i++) peak = Math.max(peak, Math.abs(c[i]));
  const gain = peak > 0 ? 0.89 / peak : 1;
  const data = Buffer.alloc(44 + n * channels * 2);
  data.write('RIFF', 0); data.writeUInt32LE(36 + n * channels * 2, 4); data.write('WAVE', 8);
  data.write('fmt ', 12); data.writeUInt32LE(16, 16); data.writeUInt16LE(1, 20); data.writeUInt16LE(channels, 22);
  data.writeUInt32LE(SR, 24); data.writeUInt32LE(SR * channels * 2, 28); data.writeUInt16LE(channels * 2, 32); data.writeUInt16LE(16, 34);
  data.write('data', 36); data.writeUInt32LE(n * channels * 2, 40);
  let o = 44;
  for (let i = 0; i < n; i++) {
    for (let c = 0; c < channels; c++) {
      const v = Math.tanh(ch[c][i] * gain * 1.05) / Math.tanh(1.05);
      data.writeInt16LE(clamp(Math.round(v * 32767), -32768, 32767), o); o += 2;
    }
  }
  fs.writeFileSync(path.join(OUT, name + '.wav'), data);
  console.log(`  ${name}.wav  ${(n / SR).toFixed(2)}s  ${channels}ch`);
}

// Oscillators ------------------------------------------------------------

function polyblep(t, dt) {
  if (t < dt) { t /= dt; return t + t - t * t - 1; }
  if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
  return 0;
}

class Saw {
  constructor(freq) { this.phase = rand(); this.freq = freq; }
  next(freq = this.freq) {
    const dt = freq / SR;
    let v = 2 * this.phase - 1 - polyblep(this.phase, dt);
    this.phase += dt; if (this.phase >= 1) this.phase -= 1;
    return v;
  }
}

class Square {
  constructor(freq, pw = 0.5) { this.phase = rand(); this.freq = freq; this.pw = pw; }
  next(freq = this.freq) {
    const dt = freq / SR;
    let v = this.phase < this.pw ? 1 : -1;
    v += polyblep(this.phase, dt);
    v -= polyblep((this.phase + 1 - this.pw) % 1, dt);
    this.phase += dt; if (this.phase >= 1) this.phase -= 1;
    return v;
  }
}

// State-variable filter (Chamberlin, oversampled x2 for stability).
class SVF {
  constructor() { this.low = 0; this.band = 0; }
  process(x, cutoff, q = 0.7, mode = 'low') {
    const f = 2 * Math.sin(Math.PI * clamp(cutoff, 20, SR * 0.24) / (SR * 2));
    const damp = 1 / Math.max(0.5, q);
    let out = 0;
    for (let k = 0; k < 2; k++) {
      this.low += f * this.band;
      const high = x - this.low - damp * this.band;
      this.band += f * high;
      out = mode === 'low' ? this.low : mode === 'band' ? this.band : high;
    }
    return out;
  }
}

class OnePole {
  constructor() { this.z = 0; }
  low(x, cutoff) { const a = Math.exp(-TAU * cutoff / SR); this.z = x * (1 - a) + this.z * a; return this.z; }
  high(x, cutoff) { return x - this.low(x, cutoff); }
}

function adsr(t, dur, a, d, s, r) {
  if (t < 0) return 0;
  if (t < a) return t / a;
  if (t < a + d) return lerp(1, s, (t - a) / d);
  if (t < dur) return s;
  if (t < dur + r) return lerp(s, 0, (t - dur) / r);
  return 0;
}

// Freeverb-style stereo reverb.
class Reverb {
  constructor(room = 0.84, damp = 0.25, wet = 0.3) {
    const combs = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];
    const aps = [556, 441, 341, 225];
    const scale = SR / 44100;
    this.room = room; this.damp = damp; this.wet = wet;
    this.c = [0, 23].map((spread) => combs.map((l) => ({ buf: new Float32Array(Math.floor((l + spread) * scale)), i: 0, store: 0 })));
    this.a = [0, 23].map((spread) => aps.map((l) => ({ buf: new Float32Array(Math.floor((l + spread) * scale)), i: 0 })));
  }
  process(L, R) {
    const input = (L + R) * 0.015;
    const out = [0, 0];
    for (let s = 0; s < 2; s++) {
      let acc = 0;
      for (const c of this.c[s]) {
        const y = c.buf[c.i];
        c.store = y * (1 - this.damp) + c.store * this.damp;
        c.buf[c.i] = input + c.store * this.room;
        if (++c.i >= c.buf.length) c.i = 0;
        acc += y;
      }
      for (const a of this.a[s]) {
        const b = a.buf[a.i];
        const y = -acc + b;
        a.buf[a.i] = acc + b * 0.5;
        if (++a.i >= a.buf.length) a.i = 0;
        acc = y;
      }
      out[s] = acc * this.wet;
    }
    return out;
  }
}

function applyReverb(ch, room, damp, wet) {
  const rv = new Reverb(room, damp, wet);
  for (let i = 0; i < ch[0].length; i++) {
    const [l, r] = rv.process(ch[0][i], ch[1][i]);
    ch[0][i] += l; ch[1][i] += r;
  }
}

function applyDelay(ch, seconds, feedback, mix) {
  const d = Math.floor(seconds * SR);
  const bl = new Float32Array(d), br = new Float32Array(d);
  let idx = 0;
  for (let i = 0; i < ch[0].length; i++) {
    const yl = bl[idx], yr = br[idx];
    bl[idx] = ch[0][i] + yr * feedback;   // ping-pong
    br[idx] = ch[1][i] + yl * feedback;
    ch[0][i] += yl * mix; ch[1][i] += yr * mix;
    if (++idx >= d) idx = 0;
  }
}

/** Render `seconds` + a tail, then fold the tail onto the start so the loop is seamless. */
function loopRender(seconds, tail, render) {
  const ch = buffer(seconds + tail);
  render(ch);
  const n = Math.ceil(seconds * SR);
  const out = buffer(seconds);
  for (let c = 0; c < 2; c++) {
    for (let i = 0; i < n; i++) out[c][i] = ch[c][i];
    for (let i = n; i < ch[c].length; i++) out[c][(i - n) % n] += ch[c][i];
  }
  return out;
}

function add(ch, i, l, r = l) {
  if (i < 0 || i >= ch[0].length) return;
  ch[0][i] += l; ch[1][i] += r;
}

// Instruments -------------------------------------------------------------

function pad(ch, start, dur, notes, opts = {}) {
  const { gain = 0.08, cutoff = 1400, attack = 0.9, release = 1.6, detune = 0.11, sweep = 0.4 } = opts;
  const oscs = [];
  for (const m of notes) for (const d of [-detune, 0, detune]) oscs.push({ osc: new Saw(), f: mtof(m + d), pan: rand() * 1.4 - 0.7 });
  const filt = [new SVF(), new SVF()];
  const s0 = Math.floor(start * SR), len = Math.floor((dur + release) * SR);
  for (let k = 0; k < len; k++) {
    const t = k / SR;
    const env = adsr(t, dur, attack, 0.6, 0.85, release);
    if (env <= 0 && t > dur) break;
    let l = 0, r = 0;
    for (const o of oscs) {
      const v = o.osc.next(o.f);
      l += v * (1 - Math.max(0, o.pan)); r += v * (1 + Math.min(0, o.pan));
    }
    const cut = cutoff * (1 + sweep * Math.sin(TAU * 0.07 * (start + t)));
    l = filt[0].process(l, cut, 0.9); r = filt[1].process(r, cut, 0.9);
    add(ch, s0 + k, l * env * gain / notes.length, r * env * gain / notes.length);
  }
}

function pluck(ch, start, midi, opts = {}) {
  const { gain = 0.25, decay = 0.25, cutoff = 4200, pan = 0, square = false } = opts;
  const osc = square ? new Square(mtof(midi), 0.3) : new Saw(mtof(midi));
  const f = new SVF();
  const s0 = Math.floor(start * SR), len = Math.floor((decay * 5) * SR);
  for (let k = 0; k < len; k++) {
    const t = k / SR;
    const env = Math.exp(-t / decay);
    const cut = 300 + cutoff * Math.exp(-t / (decay * 0.6));
    const v = f.process(osc.next(), cut, 1.4) * env * gain;
    add(ch, s0 + k, v * (1 - Math.max(0, pan)), v * (1 + Math.min(0, pan)));
  }
}

// Karplus–Strong plucked string (oud / qanun colour).
function string(ch, start, midi, opts = {}) {
  const { gain = 0.35, decay = 0.996, bright = 0.5, pan = 0, dur = 2.5 } = opts;
  const period = Math.max(2, Math.round(SR / mtof(midi)));
  const buf = new Float32Array(period);
  for (let i = 0; i < period; i++) buf[i] = noise() * (0.5 + bright * 0.5);
  let idx = 0, last = 0;
  const s0 = Math.floor(start * SR), len = Math.floor(dur * SR);
  for (let k = 0; k < len; k++) {
    const cur = buf[idx];
    const avg = decay * (cur * (0.5 + bright * 0.25) + last * (0.5 - bright * 0.25));
    last = cur;
    buf[idx] = avg;
    if (++idx >= period) idx = 0;
    const env = Math.min(1, k / 40) * (k > len - 2000 ? (len - k) / 2000 : 1);
    add(ch, s0 + k, cur * gain * env * (1 - Math.max(0, pan)), cur * gain * env * (1 + Math.min(0, pan)));
  }
}

function bass(ch, start, dur, midi, opts = {}) {
  const { gain = 0.32, cutoff = 520 } = opts;
  const a = new Saw(mtof(midi)), b = new Square(mtof(midi - 12), 0.5);
  const f = new SVF();
  const s0 = Math.floor(start * SR), len = Math.floor((dur + 0.05) * SR);
  for (let k = 0; k < len; k++) {
    const t = k / SR;
    const env = adsr(t, dur, 0.005, 0.08, 0.7, 0.05);
    const cut = cutoff + 900 * Math.exp(-t / 0.06);
    const v = f.process(a.next() * 0.6 + b.next() * 0.5, cut, 1.1) * env * gain;
    add(ch, s0 + k, v, v);
  }
}

function kick(ch, start, gain = 0.9) {
  const s0 = Math.floor(start * SR);
  let ph = 0;
  for (let k = 0; k < SR * 0.45; k++) {
    const t = k / SR;
    const f = 48 + 120 * Math.exp(-t / 0.03);
    ph += TAU * f / SR;
    const v = Math.sin(ph) * Math.exp(-t / 0.18) * gain + (t < 0.004 ? noise() * 0.3 : 0);
    add(ch, s0 + k, v, v);
  }
}

function snare(ch, start, gain = 0.45) {
  const s0 = Math.floor(start * SR);
  const hp = new OnePole(), lp = new OnePole();
  let ph = 0;
  for (let k = 0; k < SR * 0.3; k++) {
    const t = k / SR;
    ph += TAU * 190 / SR;
    const n = lp.low(hp.high(noise(), 900), 7000);
    const v = (n * Math.exp(-t / 0.09) * 0.9 + Math.sin(ph) * Math.exp(-t / 0.05) * 0.5) * gain;
    add(ch, s0 + k, v, v);
  }
}

function hat(ch, start, gain = 0.12, open = false) {
  const s0 = Math.floor(start * SR);
  const hp = new OnePole();
  const dec = open ? 0.12 : 0.025;
  for (let k = 0; k < SR * (dec * 5); k++) {
    const t = k / SR;
    const v = hp.high(noise(), 7500) * Math.exp(-t / dec) * gain;
    add(ch, s0 + k, v * 0.8, v);
  }
}

// Darbuka-like "doum" (low) and "tek" (high) hand drum hits.
function darbuka(ch, start, high, gain = 0.3) {
  const s0 = Math.floor(start * SR);
  let ph = 0;
  const bp = new SVF();
  for (let k = 0; k < SR * 0.35; k++) {
    const t = k / SR;
    const f = high ? 520 + 300 * Math.exp(-t / 0.01) : 110 + 160 * Math.exp(-t / 0.02);
    ph += TAU * f / SR;
    const body = Math.sin(ph) * Math.exp(-t / (high ? 0.05 : 0.16));
    const slap = bp.process(noise(), high ? 3200 : 900, 2) * Math.exp(-t / 0.012) * (high ? 0.8 : 0.4);
    const v = (body + slap) * gain;
    add(ch, s0 + k, v * (high ? 0.8 : 1), v * (high ? 1 : 0.9));
  }
}

// FM bell (inharmonic partials) — used for the Big Ben Chrono Lock.
function bell(ch, start, midi, opts = {}) {
  const { gain = 0.4, dur = 4, ratio = 2.76, index = 3.5, pan = 0 } = opts;
  const f = mtof(midi);
  const s0 = Math.floor(start * SR), len = Math.floor(dur * SR);
  let pc = 0, pm = 0, pm2 = 0;
  for (let k = 0; k < len; k++) {
    const t = k / SR;
    const env = Math.exp(-t / (dur * 0.28)) * Math.min(1, t / 0.002);
    const idx = index * Math.exp(-t / (dur * 0.12));
    pm += TAU * f * ratio / SR;
    pm2 += TAU * f * 4.07 / SR;
    pc += TAU * f / SR + TAU * f * idx * Math.cos(pm) / SR;
    const v = (Math.sin(pc) + 0.3 * Math.sin(pm2) * Math.exp(-t / 0.4)) * env * gain;
    add(ch, s0 + k, v * (1 - Math.max(0, pan)), v * (1 + Math.min(0, pan)));
  }
}

function sweepTone(ch, start, dur, f0, f1, opts = {}) {
  const { gain = 0.3, shape = 'sine', attack = 0.005, curve = 1, pan = 0 } = opts;
  const s0 = Math.floor(start * SR), len = Math.floor(dur * SR);
  let ph = 0;
  const sq = new Square(f0, 0.5), sw = new Saw(f0);
  for (let k = 0; k < len; k++) {
    const t = k / SR, u = Math.pow(t / dur, curve);
    const f = f0 * Math.pow(f1 / f0, u);
    ph += TAU * f / SR;
    let v = shape === 'sine' ? Math.sin(ph) : shape === 'square' ? sq.next(f) : sw.next(f);
    const env = Math.min(1, t / attack) * Math.min(1, (dur - t) / 0.02);
    v *= env * gain;
    add(ch, s0 + k, v * (1 - Math.max(0, pan)), v * (1 + Math.min(0, pan)));
  }
}

function noiseBurst(ch, start, dur, opts = {}) {
  const { gain = 0.3, from = 2000, to = 2000, q = 0.8, mode = 'band', attack = 0.005, release = null } = opts;
  const s0 = Math.floor(start * SR), len = Math.floor(dur * SR);
  const fl = new SVF(), fr = new SVF();
  for (let k = 0; k < len; k++) {
    const t = k / SR, u = t / dur;
    const cut = from * Math.pow(to / from, u);
    const env = Math.min(1, t / attack) * (release ? Math.exp(-t / release) : Math.min(1, (dur - t) / 0.05));
    add(ch, s0 + k, fl.process(noise(), cut, q, mode) * env * gain, fr.process(noise(), cut, q, mode) * env * gain);
  }
}

// ------------------------------------------------------------------ music

const BPM = 100, BEAT = 60 / BPM, BAR = BEAT * 4;
// D minor with a harmonic-minor A major dominant (Phrygian-dominant colour).
const PROG = [
  { root: 50, chord: [62, 65, 69] },      // Dm
  { root: 46, chord: [58, 62, 65] },      // Bb
  { root: 43, chord: [55, 58, 62] },      // Gm
  { root: 45, chord: [57, 61, 64] },      // A
];

function musicExplore() {
  const bars = 16, len = bars * BAR;
  return loopRender(len, 4, (ch) => {
    for (let bar = 0; bar < bars; bar++) {
      const c = PROG[Math.floor(bar / 2) % 4];
      const t0 = bar * BAR;
      if (bar % 2 === 0) pad(ch, t0, BAR * 2 - 0.2, c.chord, { gain: 0.11, cutoff: 1100 });
      for (let e = 0; e < 8; e++) bass(ch, t0 + e * BEAT / 2, BEAT / 2 - 0.04, c.root - (e % 2 === 1 ? 0 : 12) + 12, { gain: 0.2, cutoff: 380 });
      // Arpeggio in sixteenths.
      const arp = [c.chord[0], c.chord[1], c.chord[2], c.chord[0] + 12, c.chord[2], c.chord[1]];
      for (let s = 0; s < 16; s++) pluck(ch, t0 + s * BEAT / 4, arp[s % arp.length] + 12, { gain: 0.07, decay: 0.12, cutoff: 3200, pan: s % 2 ? 0.35 : -0.35 });
      kick(ch, t0, 0.55); kick(ch, t0 + BEAT * 2, 0.45);
      for (let e = 0; e < 8; e++) if (e % 2 === 1) hat(ch, t0 + e * BEAT / 2, 0.06);
      if (bar % 4 === 3) { darbuka(ch, t0 + BEAT * 2.5, false, 0.2); darbuka(ch, t0 + BEAT * 3, true, 0.16); darbuka(ch, t0 + BEAT * 3.5, true, 0.14); }
    }
    // Oud motif in A Phrygian dominant over bars 5-8 and 13-16.
    const motif = [[0, 69], [0.5, 70], [1, 73], [1.5, 70], [2, 69], [3, 67], [3.5, 65], [4, 64], [5, 65], [5.5, 67], [6, 69], [7, 62]];
    for (const startBar of [4, 12])
      for (const [b, m] of motif) string(ch, startBar * BAR + b * BEAT, m, { gain: 0.3, decay: 0.997, bright: 0.6, pan: 0.15 });
    applyDelay(ch, BEAT * 0.75, 0.32, 0.22);
    applyReverb(ch, 0.86, 0.3, 0.38);
  });
}

function musicTension() {
  const bars = 16, len = bars * BAR;
  return loopRender(len, 3, (ch) => {
    for (let bar = 0; bar < bars; bar++) {
      const c = PROG[Math.floor(bar / 2) % 4];
      const t0 = bar * BAR;
      if (bar % 2 === 0) pad(ch, t0, BAR * 2 - 0.2, c.chord.map((m) => m - 12), { gain: 0.09, cutoff: 800, sweep: 0.6 });
      for (let s = 0; s < 16; s++) bass(ch, t0 + s * BEAT / 4, BEAT / 4 - 0.02, c.root + (s % 4 === 2 ? 12 : 0), { gain: 0.22, cutoff: 600 });
      for (let b = 0; b < 4; b++) kick(ch, t0 + b * BEAT, 0.8);
      snare(ch, t0 + BEAT, 0.42); snare(ch, t0 + BEAT * 3, 0.42);
      for (let s = 0; s < 16; s++) hat(ch, t0 + s * BEAT / 4, s % 2 ? 0.05 : 0.09, s % 8 === 6);
      // Brass-like stabs on the off-beats.
      for (const off of [0.5, 1.5, 2.75]) pluck(ch, t0 + off * BEAT, c.chord[0], { gain: 0.16, decay: 0.18, cutoff: 1800, square: true });
      for (const off of [0.5, 1.5, 2.75]) pluck(ch, t0 + off * BEAT, c.chord[2], { gain: 0.12, decay: 0.18, cutoff: 1800, square: true });
      if (bar % 4 === 3) noiseBurst(ch, t0 + BEAT * 1, BEAT * 3, { gain: 0.12, from: 400, to: 7000, mode: 'band', q: 1.2, attack: BEAT * 2.6 });
      if (bar % 2 === 1) for (let i = 0; i < 4; i++) darbuka(ch, t0 + BEAT * 3 + i * BEAT / 4, i % 2 === 1, 0.18);
    }
    applyDelay(ch, BEAT * 0.5, 0.25, 0.15);
    applyReverb(ch, 0.8, 0.35, 0.28);
  });
}

function musicFinale() {
  const ch = buffer(26);
  // D major triumph: D A Bm G | D/F# G A D
  const prog = [[50, [62, 66, 69]], [45, [61, 64, 69]], [47, [59, 62, 66]], [43, [59, 62, 67]], [42, [62, 66, 69]], [43, [62, 67, 71]], [45, [61, 64, 69]], [50, [62, 66, 69, 74]]];
  prog.forEach(([root, chord], i) => {
    const t0 = i * BAR * 0.75;
    pad(ch, t0, BAR * 0.75 + 0.3, chord, { gain: 0.16, cutoff: 2600, attack: 0.25, release: i === 7 ? 6 : 1.2, detune: 0.09 });
    bass(ch, t0, BAR * 0.75 - 0.1, root, { gain: 0.3, cutoff: 700 });
    kick(ch, t0, 0.9);
    // Timpani-style roll into each chord.
    for (let r = 0; r < 4; r++) kick(ch, t0 + BAR * 0.75 - 0.32 + r * 0.08, 0.25);
    hat(ch, t0, 0.18, true);
  });
  // Fanfare lead.
  const lead = [[0, 74], [0.5, 78], [1, 81], [2.25, 79], [2.5, 78], [3, 76], [4.5, 78], [5, 81], [5.5, 86], [6.75, 85], [7, 83], [7.5, 81], [9, 86]];
  for (const [b, m] of lead) {
    const saw = new Saw(), saw2 = new Saw();
    const f = mtof(m);
    const s0 = Math.floor(b * BEAT * 0.75 * SR), len = Math.floor(1.2 * SR);
    const flt = new SVF();
    for (let k = 0; k < len; k++) {
      const t = k / SR;
      const vib = 1 + 0.006 * Math.sin(TAU * 5.5 * t) * Math.min(1, t / 0.3);
      const env = adsr(t, 0.7, 0.02, 0.2, 0.75, 0.4);
      const v = flt.process(saw.next(f * vib) + saw2.next(f * vib * 1.004), 3500, 0.8) * env * 0.12;
      add(ch, s0 + k, v * 0.9, v);
    }
  }
  for (let i = 0; i < 6; i++) bell(ch, 1.2 + i * 1.8, 86 + [0, 4, 7, 12, 7, 4][i], { gain: 0.08, dur: 3, ratio: 3.5, index: 2 });
  noiseBurst(ch, BAR * 0.75 * 7 - 2.5, 2.6, { gain: 0.16, from: 600, to: 9000, mode: 'band', attack: 2.4 });
  applyReverb(ch, 0.9, 0.2, 0.42);
  return ch;
}

function ambience() {
  const len = 20;
  return loopRender(len, 2, (ch) => {
    const lp = [new OnePole(), new OnePole()];
    const wind = [new SVF(), new SVF()];
    let hum = 0;
    for (let i = 0; i < Math.ceil((len + 2) * SR); i++) {
      const t = i / SR;
      const w = 0.6 + 0.4 * Math.sin(TAU * t / 10);  // period divides the loop length
      hum += TAU * 55 / SR;
      for (let c = 0; c < 2; c++) {
        const rumble = lp[c].low(noise(), 140) * 0.9;
        const gust = wind[c].process(noise(), 500 + 400 * Math.sin(TAU * t / 5 + c), 0.6, 'band') * 0.25 * w;
        const h = (Math.sin(hum) * 0.05 + Math.sin(hum * 2.01) * 0.025);
        ch[c][i] += rumble + gust + h;
      }
    }
    for (let k = 0; k < 6; k++) noiseBurst(ch, 1.5 + k * 3.2, 2.2, { gain: 0.05, from: 300, to: 1600, mode: 'band', q: 0.7, attack: 1.0 });
    applyReverb(ch, 0.9, 0.5, 0.35);
  });
}

// ------------------------------------------------------------------ effects

function sfx(seconds, fn, reverb = null) {
  const ch = buffer(seconds);
  fn(ch);
  if (reverb) applyReverb(ch, reverb[0], reverb[1], reverb[2]);
  return ch;
}

const effects = {
  ui_click: () => sfx(0.08, (c) => { sweepTone(c, 0, 0.04, 2400, 1800, { gain: 0.3 }); noiseBurst(c, 0, 0.015, { gain: 0.2, from: 6000, to: 6000 }); }),
  type: () => sfx(0.04, (c) => { sweepTone(c, 0, 0.02, 3200, 2600, { gain: 0.12 }); }),
  ui_confirm: () => sfx(0.6, (c) => { bell(c, 0, 81, { gain: 0.25, dur: 0.6, ratio: 2, index: 1.2 }); bell(c, 0.08, 88, { gain: 0.25, dur: 0.6, ratio: 2, index: 1.2 }); }, [0.6, 0.4, 0.2]),
  deny: () => sfx(0.3, (c) => { sweepTone(c, 0, 0.24, 140, 120, { gain: 0.25, shape: 'square' }); sweepTone(c, 0, 0.24, 147, 126, { gain: 0.2, shape: 'square' }); }),
  passport_switch: () => sfx(0.45, (c) => { noiseBurst(c, 0, 0.18, { gain: 0.2, from: 800, to: 6000 }); bell(c, 0.06, 84, { gain: 0.22, dur: 0.4, ratio: 3, index: 1.5 }); }, [0.6, 0.4, 0.25]),
  tick: () => sfx(0.12, (c) => { darbuka(c, 0, true, 0.35); }),
  checkpoint: () => sfx(0.8, (c) => { [76, 81, 88].forEach((m, i) => bell(c, i * 0.07, m, { gain: 0.18, dur: 0.7, ratio: 2, index: 1 })); }, [0.7, 0.3, 0.3]),
  sector: () => sfx(1.6, (c) => { noiseBurst(c, 0, 1.2, { gain: 0.2, from: 200, to: 2400, attack: 0.5 }); sweepTone(c, 0.2, 1.2, 110, 220, { gain: 0.25, shape: 'saw' }); bell(c, 0.7, 69, { gain: 0.25, dur: 1.2 }); }, [0.85, 0.3, 0.35]),
  cloak_on: () => sfx(1.1, (c) => {
    noiseBurst(c, 0, 0.9, { gain: 0.35, from: 900, to: 9000, q: 2, attack: 0.15 });
    for (let i = 0; i < 9; i++) bell(c, 0.05 + i * 0.07, 88 + (i % 3) * 5, { gain: 0.06, dur: 0.5, ratio: 1.5, index: 0.8, pan: (i % 2 ? 0.5 : -0.5) });
  }, [0.8, 0.3, 0.35]),
  cloak_off: () => sfx(0.6, (c) => { noiseBurst(c, 0, 0.5, { gain: 0.25, from: 7000, to: 700, q: 1.5 }); }, [0.7, 0.4, 0.25]),
  prism_fire: () => sfx(0.5, (c) => { sweepTone(c, 0, 0.32, 2600, 380, { gain: 0.35, curve: 0.5 }); sweepTone(c, 0, 0.28, 1300, 240, { gain: 0.2, shape: 'saw', curve: 0.5 }); noiseBurst(c, 0, 0.08, { gain: 0.3, from: 5000, to: 2000 }); }, [0.7, 0.3, 0.2]),
  prism_hit: () => sfx(0.8, (c) => { kick(c, 0, 0.6); noiseBurst(c, 0, 0.5, { gain: 0.35, from: 3000, to: 900, q: 3, release: 0.12 }); for (let i = 0; i < 12; i++) noiseBurst(c, rand() * 0.4, 0.02, { gain: 0.25, from: 4000 + rand() * 4000, to: 3000 }); }, [0.7, 0.3, 0.2]),
  disable: () => sfx(0.8, (c) => { sweepTone(c, 0, 0.6, 900, 70, { gain: 0.3, shape: 'saw', curve: 0.7 }); for (let i = 0; i < 8; i++) noiseBurst(c, rand() * 0.5, 0.025, { gain: 0.2, from: 5000, to: 4000 }); }),
  reboot: () => sfx(0.5, (c) => { sweepTone(c, 0, 0.35, 200, 1400, { gain: 0.22, curve: 2 }); sweepTone(c, 0.35, 0.1, 1400, 1400, { gain: 0.15 }); }),
  chrono_on: () => sfx(3.2, (c) => {
    // Big Ben: a deep bell strike with a swell of time-warp underneath.
    bell(c, 0, 52, { gain: 0.6, dur: 3.2, ratio: 2.76, index: 4 });
    bell(c, 0, 64, { gain: 0.25, dur: 2.4, ratio: 3.2, index: 2 });
    sweepTone(c, 0, 1.2, 60, 30, { gain: 0.4, curve: 0.5 });
    noiseBurst(c, 0, 1.4, { gain: 0.12, from: 4000, to: 300, q: 2 });
  }, [0.88, 0.2, 0.3]),
  chrono_off: () => sfx(1.4, (c) => { bell(c, 0, 76, { gain: 0.3, dur: 1.4, ratio: 2.76, index: 2.5 }); }, [0.8, 0.3, 0.25]),
  zap: () => sfx(0.6, (c) => { for (let i = 0; i < 6; i++) sweepTone(c, i * 0.05, 0.06, 900 + rand() * 600, 200, { gain: 0.28, shape: 'square' }); noiseBurst(c, 0, 0.4, { gain: 0.3, from: 6000, to: 1500, release: 0.1 }); }),
  caught: () => sfx(1.8, (c) => {
    kick(c, 0, 1);
    [50, 51, 56].forEach((m) => pluck(c, 0, m, { gain: 0.3, decay: 0.6, cutoff: 1500, square: true }));
    sweepTone(c, 0, 1.2, 220, 55, { gain: 0.25, shape: 'saw', curve: 0.6 });
    noiseBurst(c, 0, 0.6, { gain: 0.25, from: 2000, to: 300, release: 0.2 });
  }, [0.85, 0.3, 0.35]),
  fail: () => sfx(3, (c) => { [[62, 65, 69], [58, 62, 65], [57, 60, 64]].forEach((ch3, i) => pad(c, i * 0.7, 0.6, ch3.map((m) => m - 12), { gain: 0.25, cutoff: 900, attack: 0.05, release: 1 })); }, [0.88, 0.3, 0.35]),
  authenticate: () => sfx(1.2, (c) => { [74, 78, 81, 86].forEach((m, i) => bell(c, i * 0.08, m, { gain: 0.2, dur: 1.0, ratio: 2, index: 1.4 })); noiseBurst(c, 0, 0.3, { gain: 0.1, from: 2000, to: 9000 }); }, [0.8, 0.3, 0.3]),
  door: () => sfx(1.5, (c) => { noiseBurst(c, 0, 1.0, { gain: 0.3, from: 300, to: 1200, q: 0.7, attack: 0.1 }); sweepTone(c, 0, 1.0, 70, 90, { gain: 0.2, shape: 'saw' }); kick(c, 1.0, 0.5); }, [0.7, 0.4, 0.25]),
  field_pass: () => sfx(0.7, (c) => { noiseBurst(c, 0, 0.5, { gain: 0.25, from: 1500, to: 5000, q: 3 }); sweepTone(c, 0, 0.5, 600, 1200, { gain: 0.12 }); }, [0.6, 0.3, 0.25]),
  terminal: () => loopRender(1.2, 0.3, (c) => { for (let i = 0; i < 12; i++) sweepTone(c, i * 0.1, 0.05, 1200 + ((i * 7) % 5) * 300, 1200 + ((i * 7) % 5) * 300, { gain: 0.08 }); }),
  alarm: () => loopRender(1.6, 0.4, (c) => {
    sweepTone(c, 0, 0.8, 620, 880, { gain: 0.22, shape: 'saw' });
    sweepTone(c, 0.8, 0.8, 880, 620, { gain: 0.22, shape: 'saw' });
    for (let i = 0; i < 4; i++) sweepTone(c, i * 0.4, 0.12, 1240, 1240, { gain: 0.07, shape: 'square' });
    applyReverb(c, 0.7, 0.4, 0.2);
  }),
  landmark: () => sfx(7, (c) => {
    noiseBurst(c, 0, 3.2, { gain: 0.25, from: 200, to: 8000, q: 1.5, attack: 3.0 });
    sweepTone(c, 0, 3.2, 110, 440, { gain: 0.2, shape: 'saw', curve: 2 });
    kick(c, 3.2, 1);
    pad(c, 3.2, 2.5, [62, 66, 69, 74], { gain: 0.35, cutoff: 3000, attack: 0.05, release: 1.5 });
    for (let i = 0; i < 6; i++) bell(c, 3.25 + i * 0.12, [86, 90, 93, 98, 93, 90][i], { gain: 0.12, dur: 2 });
  }, [0.9, 0.25, 0.4]),
  firework: () => sfx(1.6, (c) => { noiseBurst(c, 0, 0.12, { gain: 0.5, from: 3000, to: 500, release: 0.05 }); kick(c, 0, 0.4); for (let i = 0; i < 30; i++) noiseBurst(c, 0.1 + rand() * 1.1, 0.015, { gain: 0.15, from: 5000 + rand() * 3000, to: 4000 }); }, [0.85, 0.3, 0.35]),
  finale: () => sfx(4, (c) => { kick(c, 0, 1); pad(c, 0, 2.5, [50, 57, 62, 66, 69, 74], { gain: 0.4, cutoff: 3500, attack: 0.02, release: 1.4 }); for (let i = 0; i < 8; i++) bell(c, 0.05 + i * 0.1, 86 + [0, 4, 7, 12][i % 4], { gain: 0.1, dur: 2.5 }); }, [0.9, 0.2, 0.4]),
  slide: () => sfx(0.6, (c) => { noiseBurst(c, 0, 0.5, { gain: 0.3, from: 2500, to: 700, q: 0.8, attack: 0.02 }); }),
  step_a: () => sfx(0.12, (c) => { noiseBurst(c, 0, 0.06, { gain: 0.5, from: 900, to: 500, q: 0.9, release: 0.02 }); sweepTone(c, 0, 0.03, 140, 80, { gain: 0.35 }); }),
  step_b: () => sfx(0.12, (c) => { noiseBurst(c, 0, 0.06, { gain: 0.5, from: 1100, to: 600, q: 0.9, release: 0.02 }); sweepTone(c, 0, 0.03, 155, 90, { gain: 0.35 }); }),
};

// ------------------------------------------------------------------ render

console.log('Rendering music...');
writeWav('music_explore', musicExplore());
writeWav('music_tension', musicTension());
writeWav('music_finale', musicFinale());
writeWav('amb_city', ambience());
console.log('Rendering effects...');
for (const [name, fn] of Object.entries(effects)) writeWav(name, fn());
console.log('Done ->', OUT);
