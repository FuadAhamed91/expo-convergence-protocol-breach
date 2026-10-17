// Quick sanity report for the generated WAVs: peak, RMS, DC offset, NaNs and loop-seam jump.
'use strict';
const fs = require('fs');
const path = require('path');
const dir = path.join(__dirname, '..', '..', 'Assets', 'Resources', 'ExpoAudio');
for (const f of fs.readdirSync(dir).filter((x) => x.endsWith('.wav'))) {
  const b = fs.readFileSync(path.join(dir, f));
  const ch = b.readUInt16LE(22), n = (b.length - 44) / 2 / ch;
  let peak = 0, sum = 0, dc = 0, bad = 0;
  for (let i = 0; i < n * ch; i++) {
    const v = b.readInt16LE(44 + i * 2) / 32768;
    if (!Number.isFinite(v)) bad++;
    peak = Math.max(peak, Math.abs(v)); sum += v * v; dc += v;
  }
  const first = b.readInt16LE(44) / 32768, last = b.readInt16LE(44 + (n - 1) * ch * 2) / 32768;
  const rmsDb = 10 * Math.log10(sum / (n * ch) + 1e-12);
  console.log(`${f.padEnd(22)} peak ${peak.toFixed(2)}  rms ${rmsDb.toFixed(1)} dB  dc ${(dc / (n * ch)).toFixed(4)}  seam ${Math.abs(last - first).toFixed(3)}${bad ? '  NaN!' : ''}`);
}
