// Original procedural motor/chain rasp, using no third-party recordings or paid services.
// A periodic buffer and periodic filter warm-up keep the two-second loop continuous.
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
const output = process.argv[2] ?? 'Assets/IronRequiem/Audio/SFX/SawBot/SawMotor_Attack_Loop.wav';
const rate = 44100, count = rate * 2;
const raw = new Float64Array(count), filtered = new Float64Array(count);
let seed = 741299;
for (let i = 0; i < count; i++) {
  const t = i / rate;
  seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
  const noise = seed / 2147483648 - 1;
  const phase = 2 * Math.PI * 110 * t - 9 * Math.cos(Math.PI * t);
  let motor = 0;
  for (let harmonic = 1; harmonic <= 24; harmonic++) motor += Math.sin(phase * harmonic) / Math.pow(harmonic, 1.12);
  const firing = .83 + .17 * Math.cos(phase * .5);
  const chain = Math.sin(phase * 5.5) * (.45 + .35 * Math.cos(phase));
  raw[i] = Math.tanh(1.7 * motor) * .56 * firing + chain * .16 + noise * .19;
}
let low = 0, dc = 0;
for (let round = 0; round < 4; round++) for (let i = 0; i < count; i++) {
  low += .34 * (raw[i] - low);
  dc += .009 * (low - dc);
  filtered[i] = low - dc;
}
const peak = filtered.reduce((p, x) => Math.max(p, Math.abs(x)), 0);
const pcm = Buffer.alloc(count * 2);
for (let i = 0; i < count; i++) pcm.writeInt16LE(Math.round(filtered[i] / peak * .72 * 32767), i * 2);
const header = Buffer.alloc(44);
header.write('RIFF'); header.writeUInt32LE(36 + pcm.length, 4); header.write('WAVEfmt ', 8);
header.writeUInt32LE(16, 16); header.writeUInt16LE(1, 20); header.writeUInt16LE(1, 22);
header.writeUInt32LE(rate, 24); header.writeUInt32LE(rate * 2, 28); header.writeUInt16LE(2, 32); header.writeUInt16LE(16, 34);
header.write('data', 36); header.writeUInt32LE(pcm.length, 40);
mkdirSync(dirname(output), { recursive: true }); writeFileSync(output, Buffer.concat([header, pcm]));
console.log(`Original mono saw motor: ${count / rate}s, peak -2.9 dBFS, seam delta=${Math.abs(filtered[0]-filtered[count-1]).toFixed(6)}.`);
