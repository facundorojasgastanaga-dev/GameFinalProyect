// IRON REQUIEM - Combat 01 "Rustborn Onslaught"
// Generates editable LMMS 1.2 projects (.mmp) that use GeneralUser GS through Sf2 Player.
// Usage: node generate_combat01.mjs <soundfont.sf2> <outDir>
import { writeFileSync, mkdirSync } from "node:fs";
import { join, resolve } from "node:path";

const SF2 = resolve(process.argv[2] ?? "../Downloads/SoundFonts/GeneralUser-GS.sf2").replace(/\\/g, "/");
const OUT = resolve(process.argv[3] ?? "./Combat01");
mkdirSync(OUT, { recursive: true });

const BPM = 180;
const BAR = 192;          // LMMS ticks per 4/4 bar
const S16 = BAR / 16;     // one 16th note
const key = (midi) => midi - 12; // LMMS key 57 = A4 = MIDI 69
const TRANSPOSE = -2;     // drop D -> drop C for every pitched track

// Deterministic humanize
let seed = 1337;
const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
const vel = (base, spread = 8) => Math.max(1, Math.min(127, Math.round(base + (rnd() - 0.5) * spread)));

// ---------------------------------------------------------------- music data
const D2 = 38; // drop-D root
const power = (root) => [root, root + 7, root + 12];

// Two-bar riff: [step16, lenSteps, offsetFromD, accent]
// Gallops (x.xx) on the open string with b2 / minor-third / tritone stabs
const RIFF = [
  [0, 1, 0, 1], [2, 1, 0, 0], [3, 1, 0, 0], [4, 1, 0, 1], [6, 1, 0, 0], [7, 1, 0, 0],
  [8, 1, 0, 1], [10, 1, 0, 0], [11, 1, 0, 0], [12, 2, 1, 1], [14, 1, 0, 0], [15, 1, 0, 0],
  [16, 1, 0, 1], [17, 1, 0, 0], [18, 1, 0, 0], [19, 1, 0, 0], [20, 2, 3, 1], [22, 1, 0, 0], [23, 1, 0, 0],
  [24, 2, 1, 1], [26, 1, 0, 0], [27, 1, 0, 0], [28, 2, 6, 1], [30, 2, 5, 1],
];
// Fill bar: straight 16th chugs climbing into the loop point
const FILL = Array.from({ length: 16 }, (_, i) => [i, 1, i < 12 ? 0 : [1, 3, 5, 6][i - 12], i % 4 === 0 ? 1 : 0]);

// Lead motifs: [step16, lenSteps, midi] over two bars
const LEAD_A = [[0, 4, 74], [4, 2, 77], [6, 2, 76], [8, 2, 74], [10, 2, 69], [12, 4, 70], [16, 6, 69], [22, 2, 67], [24, 8, 69]];
const LEAD_B = [[0, 4, 74], [4, 2, 77], [6, 2, 79], [8, 4, 81], [12, 4, 82], [16, 4, 81], [20, 2, 79], [22, 2, 77], [24, 4, 76], [28, 4, 73]];

// Half-time chord roots for the B section (one per bar)
const B_ROOTS = [0, -4, -2, -5, 0, -4, 3, -5];

// ---------------------------------------------------------------- sections (bars)
const SECTIONS = { intro: [0, 4], a: [4, 8], b: [12, 8], a2: [20, 8] };
const TOTAL_BARS = 28;

// Track note buckets: tracks[name][section] = notes
const tracks = {};
const add = (track, section, bar, step, lenSteps, midi, v) => {
  ((tracks[track] ??= {})[section] ??= []).push({
    pos: Math.round((bar - SECTIONS[section][0]) * BAR + step * S16),
    len: Math.max(1, Math.round(lenSteps * S16)),
    key: key(track === "Drums" ? midi : midi + TRANSPOSE),
    vol: Math.round((v / 127) * 100),
  });
};

// Drum map (GM)
const K = 36, SN = 38, HH = 42, OH = 46, CR = 49, RD = 51, CH = 52, TL = 41, TM = 45, TH = 48;

// One riff note: doubled guitars, low octave layer, bass and synth sub
function riffNote(section, bar, st, l, o, a) {
  const chord = l > 1 || a ? power(D2 + o) : [D2 + o, D2 + o + 7];
  const gate = l > 1 ? l * 0.95 : 0.55; // palm-muted chugs vs ringing chords
  for (const n of chord) {
    add("Guitar L", section, bar, st, gate, n, vel(a ? 124 : 108));
    add("Guitar R", section, bar, st + 0.08, gate, n, vel(a ? 120 : 104));
  }
  add("Guitar Low", section, bar, st + 0.04, gate, D2 + o - 12, vel(a ? 122 : 110));
  add("Bass", section, bar, st, gate, D2 + o - 12, vel(a ? 124 : 112));
  add("Sub", section, bar, st, gate, D2 + o - 12, vel(a ? 118 : 100));
}

// blast: alternating kick/snare 8ths over 16th double kick (DOOM-style rush)
function riffBars(section, withLead, blastBars = 0) {
  const [start, len] = SECTIONS[section];
  for (let b = 0; b < len; b += 2) {
    const bar = start + b;
    const last = b === len - 2;
    const riff = last ? RIFF.filter(([s]) => s < 16).concat(FILL.map(([s, l, o, a]) => [s + 16, l, o, a])) : RIFF;
    for (const [s, l, o, a] of riff) riffNote(section, bar + Math.floor(s / 16), s % 16, l, o, a);
    for (let bb = 0; bb < 2; bb++) {
      const br = bar + bb;
      const blast = b + bb < blastBars;
      for (let s = 0; s < 16; s++) add("Drums", section, br, s, 1, K, vel(s % 4 === 0 ? 124 : 104));
      if (blast) {
        for (let s = 2; s < 16; s += 4) add("Drums", section, br, s, 1, SN, vel(112));
        for (let s = 0; s < 16; s += 2) add("Drums", section, br, s, 1, RD, vel(96));
        add("Drums", section, br, 0, 2, CH, vel(120));
      } else {
        add("Drums", section, br, 4, 1, SN, vel(122));
        add("Drums", section, br, 12, 1, SN, vel(126));
        for (let s = 0; s < 16; s += 2) add("Drums", section, br, s, 1, s % 4 === 2 ? OH : HH, vel(s % 4 ? 84 : 100));
        add("Drums", section, br, 10, 1, CH, vel(110));
      }
    }
    add("Drums", section, bar, 0, 4, b === 0 ? CR : CH, vel(118));
    if (last) for (let s = 8; s < 16; s++) add("Drums", section, bar + 1, s, 1, [TH, TH, TM, TM, TL, TL, SN, SN][s - 8], vel(110 + s));
  }
  if (withLead) leadBars(section);
}

function leadBars(section) {
  const [start, len] = SECTIONS[section];
  const phrases = [LEAD_A, LEAD_B, LEAD_A, LEAD_B];
  for (let p = 0; p < len / 2; p++)
    for (const [s, l, m] of phrases[p]) add("Lead", section, start + p * 2, s, l, m, vel(110));
}

// Intro: choir swell, pulse synth, toms build
{
  const [start] = SECTIONS.intro;
  for (const n of [62, 65, 69]) add("Choir", "intro", start, 0, 64, n, vel(96));
  for (const n of power(D2)) add("Guitar L", "intro", start, 0, 32, n, vel(110)), add("Guitar R", "intro", start, 0.08, 32, n, vel(106));
  add("Bass", "intro", start, 0, 32, D2 - 12, vel(110));
  add("Drums", "intro", start, 0, 4, CR, vel(120));
  for (let b = 0; b < 4; b++)
    for (let s = 0; s < 16; s++) add("Pulse", "intro", start + b, s, 0.7, s % 4 === 3 ? 62 + 12 : 50 + (s % 8 === 6 ? 3 : 0), vel(70 + b * 12));
  for (let s = 0; s < 16; s += 4) add("Drums", "intro", start + 2, s, 1, K, vel(118));
  for (let s = 0; s < 16; s += 2) add("Drums", "intro", start + 3, s, 1, K, vel(118));
  for (let s = 0; s < 16; s++) add("Drums", "intro", start + 3, s, 1, [TH, TM, TL, SN][Math.floor(s / 4)], vel(90 + s * 2));
}

riffBars("a", false);

// B: half-time breakdown with lead, choir and ringing chords
{
  const [start, len] = SECTIONS.b;
  for (let b = 0; b < len; b++) {
    const bar = start + b, r = D2 + B_ROOTS[b];
    // Half-time snare over relentless double kick, guitars stab and chug under the lead
    for (const [s, l, a] of [[0, 3, 1], [3, 1, 0], [4, 1, 0], [6, 2, 1], [8, 1, 0], [9, 1, 0], [10, 2, 1], [12, 1, 0], [13, 1, 0], [14, 1, 0], [15, 1, 0]])
      riffNote("b", bar, s, l, B_ROOTS[b], a);
    for (let s = 0; s < 16; s++) add("Drums", "b", bar, s, 1, K, vel(s % 2 ? 100 : 118));
    add("Drums", "b", bar, 8, 1, SN, vel(127));
    for (let s = 0; s < 16; s += 2) add("Drums", "b", bar, s, 1, RD, vel(s % 4 ? 76 : 92));
    if (b % 4 === 0) add("Drums", "b", bar, 0, 4, CR, vel(120));
    for (const n of [r + 24, r + 27, r + 31]) add("Choir", "b", bar, 0, 16, n, vel(84));
  }
  leadBars("b");
}

riffBars("a2", true, 4);

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
  "Guitar L": { bank: 0, patch: 30, vol: 150, pan: -70, gain: 1, fx: GUITAR_FX },
  "Guitar R": { bank: 0, patch: 29, vol: 145, pan: 70, gain: 1, fx: GUITAR_FX },
  "Guitar Low": { bank: 0, patch: 30, vol: 105, pan: 0, gain: 1, fx: [FX.drive(4, 1.6, 0.6), FX.lows(110, 1.3)] },
  Bass: { bank: 0, patch: 34, vol: 90, pan: 0, gain: 1, fx: [FX.drive(2, 1.3, 0.8), FX.lows(70, 1.4)] },
  Sub: { bank: 0, patch: 38, vol: 85, pan: 0, gain: 1, fx: [FX.lows(55, 1.3)] },
  Drums: { bank: 128, patch: 16, vol: 150, pan: 0, gain: 1, fx: [FX.comp(0.25, 3, 1.1)] },
  Lead: { bank: 0, patch: 81, vol: 200, pan: 10, gain: 2.5, reverb: 1 },
  Choir: { bank: 0, patch: 52, vol: 150, pan: 0, gain: 2, reverb: 1 },
  Pulse: { bank: 0, patch: 39, vol: 120, pan: -15, gain: 1 },
};

const esc = (s) => s.replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/</g, "&lt;");
const SECTION_ORDER = ["intro", "a", "b", "a2"];

function trackXml(name, sectionsUsed, offsetBars) {
  const i = INSTRUMENTS[name];
  const patterns = sectionsUsed
    .filter((s) => tracks[name]?.[s])
    .map((s) => {
      const [start, len] = SECTIONS[s];
      const notes = tracks[name][s]
        .sort((x, y) => x.pos - y.pos || x.key - y.key)
        .map((n) => `        <note pan="0" key="${n.key}" vol="${n.vol}" pos="${n.pos}" len="${n.len}"/>`)
        .join("\n");
      return `      <pattern type="1" name="${esc(name)} ${s}" muted="0" steps="16" pos="${(start - offsetBars) * BAR}">\n${notes}\n      </pattern>`;
    })
    .join("\n");
  return `    <track type="0" name="${esc(name)}" muted="0" solo="0">
      <instrumenttrack vol="${i.vol}" pan="${i.pan}" fxch="0" basenote="57" pitch="0" pitchrange="1" usemasterpitch="1">
        <instrument name="sf2player">
          <sf2player src="${esc(SF2)}" bank="${i.bank}" patch="${i.patch}" gain="${i.gain}" reverbOn="${i.reverb ?? 0}" reverbRoomSize="0.7" reverbDamping="0.3" reverbWidth="0.8" reverbLevel="0.6" chorusOn="0" chorusNum="3" chorusLevel="2" chorusSpeed="0.3" chorusDepth="8"/>
        </instrument>
        <fxchain numofeffects="${i.fx?.length ?? 0}" enabled="${i.fx ? 1 : 0}">${(i.fx ?? []).join("")}</fxchain>
      </instrumenttrack>
${patterns}
    </track>`;
}

// Exploration layer: same loop without guitars and drums, played in sync with the full loop in-game
const EXPLORE_TRACKS = ["Bass", "Sub", "Choir", "Lead"];

function projectXml(sectionsUsed, offsetBars, bars, trackNames = Object.keys(INSTRUMENTS)) {
  const body = trackNames.map((t) => trackXml(t, sectionsUsed, offsetBars)).join("\n");
  return `<?xml version="1.0"?>
<!DOCTYPE lmms-project>
<lmms-project version="1.0" creator="LMMS" creatorversion="1.2.2" type="song">
  <head bpm="${BPM}" timesig_numerator="4" timesig_denominator="4" mastervol="55" masterpitch="0"/>
  <song>
  <trackcontainer type="song" visible="1">
${body}
  </trackcontainer>
  <timeline lpstate="1" lp0pos="0" lp1pos="${bars * BAR}"/>
  </song>
</lmms-project>
`;
}

writeFileSync(join(OUT, "IR_Combat01_Full.mmp"), projectXml(SECTION_ORDER, 0, TOTAL_BARS));
writeFileSync(join(OUT, "IR_Combat01_Loop.mmp"), projectXml(["a", "b", "a2"], 4, TOTAL_BARS - 4));
writeFileSync(join(OUT, "IR_Combat01_Explore.mmp"), projectXml(["a", "b", "a2"], 4, TOTAL_BARS - 4, EXPLORE_TRACKS));
console.log(`Written to ${OUT} | ${BPM} BPM | full ${TOTAL_BARS} bars (${((TOTAL_BARS * 240) / BPM).toFixed(1)} s)`);
