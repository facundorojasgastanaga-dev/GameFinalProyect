// Usage: node visual_overhaul_quarantine.js move|restore [date]
// Moves every "sin uso" row of Reports/VisualOverhaul/inventory.csv (asset + .meta) to
// Tools/IronRequiem/Quarantine/<date>/<original path>, outside Assets. Nothing is deleted. A manifest with SHA-256
// of every moved file is written next to it; "restore" moves the files back to their original paths.
const fs = require("fs"), path = require("path"), crypto = require("crypto");
const project = path.resolve(__dirname, "../..");
const mode = process.argv[2], date = process.argv[3] || "20261107";
const qroot = path.join(project, "Tools/IronRequiem/Quarantine", date);
const manifestPath = path.join(qroot, "manifest.json");
const sha = f => crypto.createHash("sha256").update(fs.readFileSync(f)).digest("hex");
function parseCsv(text) {
  const rows = []; let row = [], cur = "", q = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (q) { if (ch === '"' && text[i + 1] === '"') { cur += '"'; i++; } else if (ch === '"') q = false; else cur += ch; }
    else if (ch === '"') q = true; else if (ch === ",") { row.push(cur); cur = ""; } else if (ch === "\n") { row.push(cur); rows.push(row); row = []; cur = ""; } else cur += ch;
  }
  if (cur.length || row.length) { row.push(cur); rows.push(row); }
  return rows;
}
function move(from, to) { fs.mkdirSync(path.dirname(to), { recursive: true }); fs.renameSync(from, to); }
if (mode === "move") {
  if (fs.existsSync(manifestPath)) throw new Error("Quarantine already exists: " + manifestPath);
  const rows = parseCsv(fs.readFileSync(path.join(project, "Tools/IronRequiem/Reports/VisualOverhaul/inventory.csv"), "utf8")).slice(1).filter(r => r[3] === "sin uso");
  const moved = [];
  for (const r of rows) {
    const rel = r[0];
    for (const f of [rel, rel + ".meta"]) {
      const src = path.join(project, f); if (!fs.existsSync(src)) continue;
      const hash = sha(src); move(src, path.join(qroot, f)); moved.push({ path: f, bytes: fs.statSync(path.join(qroot, f)).size, sha256: hash });
    }
  }
  fs.writeFileSync(manifestPath, JSON.stringify({ utc: new Date().toISOString(), note: "Moved, not deleted. Restore: node Tools/IronRequiem/visual_overhaul_quarantine.js restore " + date, files: moved }, null, 1));
  console.log("moved " + moved.length + " files, " + (moved.reduce((a, b) => a + b.bytes, 0) / 1048576).toFixed(1) + " MB");
} else if (mode === "restore") {
  const m = JSON.parse(fs.readFileSync(manifestPath, "utf8")); let n = 0;
  for (const f of m.files) { const src = path.join(qroot, f.path); if (fs.existsSync(src)) { move(src, path.join(project, f.path)); n++; } }
  fs.renameSync(manifestPath, manifestPath + ".restored-" + Date.now()); console.log("restored " + n);
} else throw new Error("mode: move|restore");
