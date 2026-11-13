// Usage: node visual_overhaul_ab.js <run> [<run> ...]
// Reads Tools/IronRequiem/Reports/CombatLoad/<run>.json/.csv and <run>-system.json (nvidia-smi samples every 5 s:
// memory MiB, util %, temp C, power W, pstate, graphics clock MHz). Prints the A/B table rows as Markdown.
const fs = require("fs"), path = require("path");
const dir = path.join(__dirname, "Reports", "CombatLoad");
const q = (a, p) => a[Math.min(a.length - 1, Math.floor(a.length * p))];
const rows = [];
for (const run of process.argv.slice(2)) {
  const d = JSON.parse(fs.readFileSync(path.join(dir, run + ".json"), "utf8"));
  const csv = fs.readFileSync(path.join(dir, run + ".csv"), "utf8").trim().split("\n").slice(1).map(l => l.split(",").map(Number));
  const frames = csv.map(r => r[1]).sort((a, b) => a - b); const n = frames.length;
  const mean = frames.reduce((a, b) => a + b, 0) / n;
  let sys = []; try { sys = JSON.parse(fs.readFileSync(path.join(dir, run + "-system.json"), "utf8")); } catch { }
  const gpu = sys.map(s => String(s.gpu || "").split(",").map(x => x.trim())).filter(g => g.length >= 6 && !isNaN(+g[3]));
  const watts = gpu.map(g => +g[3]), clock = gpu.map(g => +g[5]), temp = gpu.map(g => +g[2]), pstates = [...new Set(gpu.map(g => g[4]))];
  const avg = a => a.length ? (a.reduce((x, y) => x + y, 0) / a.length) : NaN;
  const sd = a => { const m = avg(a); return Math.sqrt(avg(a.map(x => (x - m) ** 2))); };
  rows.push({
    run, samples: n, mean, median: q(frames, .5), p95: q(frames, .95), p99: q(frames, .99), max: frames[n - 1],
    over8: 100 * frames.filter(v => v > 8.333).length / n, over16: 100 * frames.filter(v => v > 16.667).length / n,
    over33: frames.filter(v => v > 33.3).length, over50: frames.filter(v => v > 50).length,
    cpu: d.cpuFrame && d.cpuFrame.mean, gpu: d.gpuFrame && d.gpuFrame.mean, gpuP99: d.gpuFrame && d.gpuFrame.p99, main: d.mainThread && d.mainThread.mean, render: d.renderThread && d.renderThread.mean,
    shots: d.playerShots, enemyShots: d.enemyShots, deaths: d.deaths, gc: d.gcCollections, errors: (d.errors || []).length,
    watts: avg(watts), wattsSd: sd(watts), clock: avg(clock), clockSd: sd(clock), temp: avg(temp), pstates: pstates.join("/"), res: d.resolution, vsync: d.vsync
  });
}
const f = (x, k = 2) => x === undefined || x === null || isNaN(x) ? "n/a" : x.toFixed(k);
console.log("| Pasada | Muestras | Media | Mediana | p95 | p99 | Máx | >8,33 ms | >16,67 ms | >33 ms | >50 ms | CPU | GPU (p99) | Hilo principal | Hilo de render | GPU W (σ) | Reloj MHz (σ) | Temp | P-state | Disparos jug./enem. | Muertes | GC | Errores |");
console.log("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
for (const r of rows) console.log(`| ${r.run} | ${r.samples} | ${f(r.mean)} | ${f(r.median)} | ${f(r.p95)} | ${f(r.p99)} | ${f(r.max, 1)} | ${f(r.over8, 1)} % | ${f(r.over16, 2)} % | ${r.over33} | ${r.over50} | ${f(r.cpu)} | ${f(r.gpu)} (${f(r.gpuP99)}) | ${f(r.main)} | ${f(r.render)} | ${f(r.watts, 1)} (${f(r.wattsSd, 1)}) | ${f(r.clock, 0)} (${f(r.clockSd, 0)}) | ${f(r.temp, 0)} | ${r.pstates} | ${r.shots}/${r.enemyShots} | ${r.deaths} | ${r.gc} | ${r.errors} |`);
fs.writeFileSync(path.join(__dirname, "Reports", "VisualOverhaul", "ab-" + process.argv.slice(2).join("_") + ".json"), JSON.stringify(rows, null, 1));
