// Usage: node summarize_combat_load.js <run> [<run> ...]  (reads Tools/IronRequiem/Reports/CombatLoad/<run>.json/.csv)
// Prints distribution summaries plus the share of frames over 8.33 / 16.67 ms, worst 1 s windows and long stalls.
const fs = require("fs"), path = require("path");
const dir = path.join(__dirname, "Reports", "CombatLoad");
const f = x => x && x.samples ? `mean ${x.mean.toFixed(2)} | med ${x.median.toFixed(2)} | p95 ${x.p95.toFixed(2)} | p99 ${x.p99.toFixed(2)} | max ${x.maximum.toFixed(1)} | >8.33 ${x.overBudgetPercent.toFixed(1)}%` : "n/a";
for (const run of process.argv.slice(2)) {
  const d = JSON.parse(fs.readFileSync(path.join(dir, run + ".json"), "utf8"));
  const rows = fs.readFileSync(path.join(dir, run + ".csv"), "utf8").trim().split("\n").slice(1).map(l => l.split(",").map(Number));
  const frames = rows.map(r => r[1]);
  const over = t => (100 * frames.filter(v => v > t).length / frames.length).toFixed(2);
  const sec = {}; for (const r of rows) { const s = Math.floor(r[0]); (sec[s] = sec[s] || []).push(r[1]); }
  const fps1 = Object.values(sec).map(a => a.length).sort((a, b) => a - b);
  const low1 = fps1[Math.floor(fps1.length * .01)], low5 = fps1[Math.floor(fps1.length * .05)];
  console.log(`== ${run}: ${d.environment} ${d.resolution} ${d.graphicsApi} vsync=${d.vsync} target=${d.targetFps} measured=${d.measuredSeconds.toFixed(0)}s samples=${d.sampleCount} invalidRes=${d.invalidResolutionFrames}`);
  console.log(`   waves=${d.waves} playerShots=${d.playerShots} enemyShots=${d.enemyShots} deaths=${d.deaths} gcCollections=${d.gcCollections} errors=${(d.errors || []).length}`);
  console.log(`   frame  ${f(d.frame)} | >16.67 ${over(16.667)}% | >33 ${d.frame.over33} | >50 ${d.frame.over50}`);
  console.log(`   cpu    ${f(d.cpuFrame)}`);
  console.log(`   gpu    ${f(d.gpuFrame)}`);
  console.log(`   main   ${f(d.mainThread)}`);
  console.log(`   render ${f(d.renderThread)}`);
  console.log(`   1-s windows: min ${fps1[0]} fps, 1% low ${low1}, 5% low ${low5}, median ${fps1[Math.floor(fps1.length / 2)]}`);
  // Phase view: seconds 0-14 of each wave (12 enemies alive) vs 14-20 (kills/cleanup).
  const phase = (a, b) => { const v = rows.filter(r => { const t = r[0] % 20; return t >= a && t < b; }).map(r => r[1]).sort((x, y) => x - y); return `mean ${(v.reduce((x, y) => x + y, 0) / v.length).toFixed(2)} p95 ${v[Math.floor(v.length * .95)].toFixed(2)} p99 ${v[Math.floor(v.length * .99)].toFixed(2)} >8.33 ${(100 * v.filter(x => x > 8.333).length / v.length).toFixed(1)}%`; };
  console.log(`   12 alive (0-14s): ${phase(0, 14)}`);
  console.log(`   kills (14-20s):   ${phase(14, 20)}`);
}
