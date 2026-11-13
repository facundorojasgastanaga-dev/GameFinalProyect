// Prints peak / RMS (dBFS) and clipped-sample count for 16-bit or float PCM WAV files.
// Usage: node wav_levels.mjs <file.wav> [more.wav ...]
import { readFileSync } from "node:fs";
import { basename } from "node:path";

const db = (x) => (x > 0 ? (20 * Math.log10(x)).toFixed(1) : "-inf");

for (const file of process.argv.slice(2)) {
  const buf = readFileSync(file);
  let off = 12, fmt, data;
  while (off < buf.length - 8) {
    const id = buf.toString("ascii", off, off + 4), size = buf.readUInt32LE(off + 4);
    if (id === "fmt ") fmt = { format: buf.readUInt16LE(off + 8), ch: buf.readUInt16LE(off + 10), rate: buf.readUInt32LE(off + 12), bits: buf.readUInt16LE(off + 22) };
    if (id === "data") data = buf.subarray(off + 8, off + 8 + size);
    off += 8 + size + (size & 1);
  }
  const step = fmt.bits / 8, float = fmt.format === 3;
  let peak = 0, sum = 0, clip = 0;
  const n = Math.floor(data.length / step);
  for (let i = 0; i < n; i++) {
    const v = Math.abs(float ? data.readFloatLE(i * step) : data.readInt16LE(i * step) / 32768);
    peak = Math.max(peak, v); sum += v * v; if (v >= 0.999) clip++;
  }
  const secs = n / fmt.ch / fmt.rate;
  console.log(`${basename(file).padEnd(34)} ${secs.toFixed(1)}s peak ${db(peak)} dB  rms ${db(Math.sqrt(sum / n))} dB  clipped ${clip}`);
}
