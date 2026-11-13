// IRON REQUIEM - Menu 01 "Requiem for Steel"
// Slow C minor menu loop: choir, strings, bells and piano, then heavy guitars, timpani and half-time drums.
// Generates an editable LMMS 1.2 project (.mmp) that uses GeneralUser GS through Sf2 Player.
// Usage: node generate_menu01.mjs <soundfont.sf2> <outDir>
import { writeFileSync, mkdirSync } from "node:fs";
import { join, resolve } from "node:path";

const SF2 = resolve(process.argv[2] ?? "../Downloads/SoundFonts/GeneralUser-GS.sf2").replace(/\\/g, "/");
const OUT = resolve(process.argv[3] ?? "./Menu01");
mkdirSync(OUT, { recursive: true });

const BPM = 80;
const BAR = 192;          // LMMS ticks per 4/4 bar
const S16 = BAR / 16;     // one 16th note
const key = (midi) => midi - 12; // LMMS key 57 = A4 = MIDI 69

// Deterministic humanize
let seed = 666;
const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
const vel = (base, spread = 8) => Math.max(1, Math.min(127, Math.round(base + (rnd() - 0.5) * spread)));

// ---------------------------------------------------------------- music data
// i - VI - iv - V | i - VI - bII (Neapolitan) - V
const CHORDS = [
  { root: 36, voice: [60, 63, 67] }, // Cm
  { root: 32, voice: [60, 63, 68] }, // Ab
  { root: 29, voice: [60, 65, 68] }, // Fm
  { root: 31, voice: [59, 62, 67] }, // G
  { root: 36, voice: [60, 63, 67] }, // Cm
  { root: 32, voice: [60, 63, 68] }, // Ab
  { root: 37, voice: [61, 65, 68] }, // Db
  { root: 31, voice: [59, 62, 67] }, // G
];

// Melody per bar: [step16, lenSteps, midi]
const MELODY = [
  [[0, 8, 67], [8, 4, 75], [12, 4, 74]],
  [[0, 12, 72], [12, 4, 70]],
  [[0, 8, 68], [8, 4, 72], [12, 4, 77]],
  [[0, 8, 74], [8, 8, 71]],
  [[0, 8, 67], [8, 4, 75], [12, 4, 79]],
  [[0, 8, 80], [8, 4, 79], [12, 4, 75]],
  [[0, 8, 77], [8, 8, 73]],
  [[0, 12, 71], [12, 4, 67]],
];

// ---------------------------------------------------------------- sections (bars)
const SECTIONS = { calm: [0, 8], heavy: [8, 8] };
const TOTAL_BARS = 16;

const tracks = {};
const add = (track, section, bar, step, lenSteps, midi, v) => {
  ((tracks[track] ??= {})[section] ??= []).push({
    pos: Math.round((bar - SECTIONS[section][0]) * BAR + step * S16),
    len: Math.max(1, Math.round(lenSteps * S16)),
    key: key(midi),
    vol: Math.round((v / 127) * 100),
  });
};

// Drum map (GM)
const K = 36, SN = 38, CR = 49, CH = 52, TL = 41, TF = 43;

function section(name, heavy) {
  const [start, len] = SECTIONS[name];
  for (let b = 0; b < len; b++) {
    const bar = start + b, { root, voice } = CHORDS[b];
    for (const n of voice) add("Choir", name, bar, 0, 16, n, vel(heavy ? 100 : 84));
    for (const n of [root + 12, ...voice.map((v) => v - 12)]) add("Strings", name, bar, 0, 16, n, vel(heavy ? 96 : 78));
    if (b % 4 === 0) add("Bells", name, bar, 0, 16, root + 24, vel(heavy ? 110 : 96));
    for (let s = 0; s < 16; s += 2) add("Pulse", name, bar, s, 1.2, root + 12, vel(s % 4 ? 64 : 80));
    for (const [s, l, m] of MELODY[b]) {
      add("Piano", name, bar, s, l, m + (heavy ? 12 : 0), vel(heavy ? 92 : 100));
      if (heavy) add("Lead", name, bar, s, l, m, vel(108));
    }
    if (!heavy) continue;
    // Heavy half: one ringing power chord per bar, chug pickup into the next bar
    for (const [s, l] of [[0, 13], [14, 0.6], [15, 0.6]])
      for (const n of [root + 12, root + 19, root + 24]) {
        add("Guitar L", name, bar, s, l, n, vel(s ? 104 : 120));
        add("Guitar R", name, bar, s + 0.08, l, n, vel(s ? 100 : 116));
      }
    add("Bass", name, bar, 0, 13, root, vel(118));
    add("Bass", name, bar, 14, 0.6, root, vel(104)), add("Bass", name, bar, 15, 0.6, root, vel(104));
    for (const s of [0, 10, 14, 15]) add("Drums", name, bar, s, 1, K, vel(s ? 108 : 124));
    add("Drums", name, bar, 8, 1, SN, vel(126));
    add("Drums", name, bar, 0, 4, b % 4 === 0 ? CR : CH, vel(b % 4 === 0 ? 122 : 100));
    add("Timpani", name, bar, 0, 4, root, vel(118));
  }
  // Calm ends with a timpani swell, heavy ends with a tom fill back into the loop
  const last = start + len - 1;
  if (heavy) for (let s = 8; s < 16; s++) add("Drums", name, last, s, 1, s < 12 ? TF : TL, vel(104 + s));
  else for (let s = 0; s < 16; s++) add("Timpani", name, last, s, 1, 36, vel(60 + s * 4));
}

section("calm", false);
section("heavy", true);

// ---------------------------------------------------------------- effects
// Waveshaper / Dynamics Processor take a 200-point curve over |input| 0..1, as base64 float32.
const curve = (f) => {
  const b = Buffer.alloc(800);
  for (let i = 0; i < 200; i++) b.writeFloatLE(Math.min(1, f(i / 199)), i * 4);
  return b.toString("base64");
};
const saturate = (k) => curve((x) => Math.tanh(k * x) / Math.tanh(k));
const compress = (thr, ratio) => curve((x) => (x < thr ? x : thr + (x - thr) / ratio));
const FX = {
  drive: (k, inGain, outGain) =>
    `<effect name="waveshaper" on="1" wet="1" gate="0" autoquit="1" autoquit_numerator="4" autoquit_denominator="4" syncmode="0"><waveshapercontrols inputGain="${inGain}" outputGain="${outGain}" clipInput="1" waveShape="${saturate(k)}"/><key/></effect>`,
  comp: (thr, ratio, outGain) =>
    `<effect name="dynamicsprocessor" on="1" wet="1" gate="0" autoquit="1" autoquit_numerator="4" autoquit_denominator="4" syncmode="0"><dynamicsprocessor_controls inputGain="1" outputGain="${outGain}" attack="5" release="60" stereoMode="0" waveShape="${compress(thr, ratio)}"/><key/></effect>`,
  lows: (freq, gain) =>
    `<effect name="bassbooster" on="1" wet="1" gate="0" autoquit="1" autoquit_numerator="4" autoquit_denominator="4" syncmode="0"><bassboostercontrols freq="${freq}" gain="${gain}" ratio="2"/><key/></effect>`,
};

// ---------------------------------------------------------------- LMMS XML
const GUITAR_FX = [FX.drive(5, 1.8, 0.55), FX.comp(0.3, 4, 1)];
const INSTRUMENTS = {
  Choir: { bank: 0, patch: 52, vol: 150, pan: 0, gain: 2, reverb: 1 },
  Strings: { bank: 0, patch: 48, vol: 110, pan: -20, gain: 1.5, reverb: 1 },
  Bells: { bank: 0, patch: 14, vol: 200, pan: 20, gain: 4, reverb: 1 },
  Piano: { bank: 0, patch: 0, vol: 200, pan: 15, gain: 3, reverb: 1 },
  Pulse: { bank: 0, patch: 38, vol: 70, pan: 0, gain: 1, fx: [FX.lows(60, 1.2)] },
  Lead: { bank: 0, patch: 81, vol: 130, pan: -10, gain: 2, reverb: 1 },
  "Guitar L": { bank: 0, patch: 30, vol: 140, pan: -70, gain: 1, fx: GUITAR_FX },
  "Guitar R": { bank: 0, patch: 29, vol: 135, pan: 70, gain: 1, fx: GUITAR_FX },
  Bass: { bank: 0, patch: 34, vol: 90, pan: 0, gain: 1, fx: [FX.drive(2, 1.3, 0.8), FX.lows(70, 1.4)] },
  Drums: { bank: 128, patch: 16, vol: 180, pan: 0, gain: 1, fx: [FX.comp(0.25, 3, 1.1)] },
  Timpani: { bank: 0, patch: 47, vol: 140, pan: 0, gain: 1.5, reverb: 1 },
};

const esc = (s) => s.replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");

function trackXml(name) {
  const i = INSTRUMENTS[name];
  const patterns = Object.keys(SECTIONS)
    .filter((s) => tracks[name]?.[s])
    .map((s) => {
      const notes = tracks[name][s]
        .sort((x, y) => x.pos - y.pos || x.key - y.key)
        .map((n) => `        <note pan="0" key="${n.key}" vol="${n.vol}" pos="${n.pos}" len="${n.len}"/>`)
        .join("\n");
      return `      <pattern type="1" name="${esc(name)} ${s}" muted="0" steps="16" pos="${SECTIONS[s][0] * BAR}">\n${notes}\n      </pattern>`;
    })
    .join("\n");
  return `    <track type="0" name="${esc(name)}" muted="0" solo="0">
      <instrumenttrack vol="${i.vol}" pan="${i.pan}" fxch="0" basenote="57" pitch="0" pitchrange="1" usemasterpitch="1">
        <instrument name="sf2player">
          <sf2player src="${esc(SF2)}" bank="${i.bank}" patch="${i.patch}" gain="${i.gain}" reverbOn="${i.reverb ?? 0}" reverbRoomSize="0.85" reverbDamping="0.3" reverbWidth="0.9" reverbLevel="0.7" chorusOn="0" chorusNum="3" chorusLevel="2" chorusSpeed="0.3" chorusDepth="8"/>
        </instrument>
        <fxchain numofeffects="${i.fx?.length ?? 0}" enabled="${i.fx ? 1 : 0}">${(i.fx ?? []).join("")}</fxchain>
      </instrumenttrack>
${patterns}
    </track>`;
}

const xml = `<?xml version="1.0"?>
<!DOCTYPE lmms-project>
<lmms-project version="1.0" creator="LMMS" creatorversion="1.2.2" type="song">
  <head bpm="${BPM}" timesig_numerator="4" timesig_denominator="4" mastervol="55" masterpitch="0"/>
  <song>
  <trackcontainer type="song" visible="1">
${Object.keys(INSTRUMENTS).map(trackXml).join("\n")}
  </trackcontainer>
  <timeline lpstate="1" lp0pos="0" lp1pos="${TOTAL_BARS * BAR}"/>
  </song>
</lmms-project>
`;

writeFileSync(join(OUT, "IR_Menu01_Loop.mmp"), xml);
console.log(`Written to ${OUT} | ${BPM} BPM | ${TOTAL_BARS} bars (${((TOTAL_BARS * 240) / BPM).toFixed(1)} s)`);
