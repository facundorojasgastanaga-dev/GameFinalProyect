// Usage: node visual_overhaul_inventory.js [outDir]
// Read-only usage inventory of Assets/ (models, prefabs, materials, textures, scenes, audio, anim, other).
// "In use" = reachable by GUID from a root: Build Settings scenes, Resources folders, GUIDs in ProjectSettings,
// asset paths / scene names written in C# (runtime, Editor tools, tests), and everything reachable from those
// through serialized references (scenes, prefabs, materials, controllers, .meta remaps, shader graphs...).
// Code, shaders and third-party folders are never candidates. Files cited by Docs/ or Tools/ scripts are kept
// as "protected" (source models, references) even when no game asset points at them.
const fs = require("fs"), path = require("path");
const project = path.resolve(__dirname, "../..");
const out = path.resolve(project, process.argv[2] || "Tools/IronRequiem/Reports/VisualOverhaul");
fs.mkdirSync(out, { recursive: true });
const rel = p => path.relative(project, p).split(path.sep).join("/");
function walk(dir, list = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, list); else list.push(p);
  }
  return list;
}
const all = walk(path.join(project, "Assets")).map(rel);
const metas = all.filter(p => p.endsWith(".meta"));
const files = all.filter(p => !p.endsWith(".meta"));
const guidToPath = new Map(), pathToGuid = new Map();
for (const m of metas) {
  const g = /guid: ([0-9a-f]{32})/.exec(fs.readFileSync(path.join(project, m), "utf8"));
  if (g) { const asset = m.slice(0, -5); guidToPath.set(g[1], asset); pathToGuid.set(asset, g[1]); }
}
const textExt = new Set([".unity", ".prefab", ".mat", ".asset", ".controller", ".overrideController", ".anim", ".mask", ".playable",
  ".shadergraph", ".shadersubgraph", ".vfx", ".lighting", ".preset", ".physicMaterial", ".physicsMaterial", ".spriteatlas", ".spriteatlasv2",
  ".inputactions", ".asmdef", ".asmref", ".terrainlayer", ".brush", ".flare", ".guiskin", ".fontsettings", ".mixer", ".renderTexture", ".cubemap", ".signal", ".json", ".uss", ".uxml", ".tss"]);
const isText = p => textExt.has(path.extname(p));
const guidRe = /[0-9a-f]{32}/g;
function refsOf(p) {
  const set = new Set();
  const scan = file => {
    let txt; try { txt = fs.readFileSync(path.join(project, file), "utf8"); } catch { return; }
    for (const m of txt.matchAll(guidRe)) if (guidToPath.has(m[0])) set.add(guidToPath.get(m[0]));
  };
  if (isText(p) && fs.statSync(path.join(project, p)).size < 80 * 1024 * 1024) scan(p);
  // .meta: model material remaps, sprite/texture references, script default references, etc.
  if (fs.existsSync(path.join(project, p + ".meta"))) {
    const meta = fs.readFileSync(path.join(project, p + ".meta"), "utf8");
    const own = pathToGuid.get(p);
    for (const m of meta.matchAll(guidRe)) if (m[0] !== own && guidToPath.has(m[0])) set.add(guidToPath.get(m[0]));
  }
  set.delete(p);
  return set;
}
// ---- roots ----
const roots = new Map(); // path -> reason
const addRoot = (p, why) => { if (!roots.has(p)) roots.set(p, why); };
const buildSettings = fs.readFileSync(path.join(project, "ProjectSettings/EditorBuildSettings.asset"), "utf8");
for (const m of buildSettings.matchAll(/path: (Assets\/[^\n]+\.unity)/g)) addRoot(m[1].trim(), "Build Settings");
for (const f of files) if (/(^|\/)Resources\//.test(f)) addRoot(f, "carpeta Resources");
for (const f of fs.readdirSync(path.join(project, "ProjectSettings")).filter(n => fs.statSync(path.join(project, "ProjectSettings", n)).isFile())) {
  const txt = fs.readFileSync(path.join(project, "ProjectSettings", f), "utf8");
  for (const m of txt.matchAll(guidRe)) if (guidToPath.has(m[0])) addRoot(guidToPath.get(m[0]), "ProjectSettings/" + f);
}
// C# string literals: "Assets/..." paths (files or folders) and scene names.
const cs = files.filter(f => f.endsWith(".cs"));
const csText = cs.map(f => [f, fs.readFileSync(path.join(project, f), "utf8")]);
const literalPaths = new Map();
for (const [f, t] of csText) for (const m of t.matchAll(/"(Assets\/[^"]+)"/g)) { const p = m[1].replace(/\/$/, ""); if (!literalPaths.has(p)) literalPaths.set(p, f); }
for (const [p, by] of literalPaths) {
  if (files.includes(p)) addRoot(p, "ruta en " + by);
  else { const prefix = p + "/"; const hit = files.filter(f => f.startsWith(prefix) && !f.slice(prefix.length).includes("/")); for (const h of hit) addRoot(h, "carpeta citada en " + by); }
}
// Composite constants such as Art + "/Bunker_Floor.mat": file names quoted in C# that exist exactly once.
const byName = new Map(); for (const f of files) { const n = path.basename(f); byName.set(n, (byName.get(n) || []).concat(f)); }
for (const [f, t] of csText) for (const m of t.matchAll(/"\/?([A-Za-z0-9_\-. ]+\.(?:prefab|mat|asset|unity|controller|anim|fbx|png|wav|ogg|mp3|shader|overrideController|mask|exr|tga|jpg|psd))"/g)) {
  for (const h of byName.get(m[1]) || []) addRoot(h, "nombre en " + f);
}
for (const s of files.filter(f => f.endsWith(".unity"))) {
  const name = path.basename(s, ".unity");
  for (const [f, t] of csText) if (t.includes('"' + name + '"')) { addRoot(s, "nombre de escena en " + f); break; }
}
// ---- reachability ----
const usedBy = new Map();
const queue = [...roots.keys()]; for (const r of queue) usedBy.set(r, roots.get(r));
while (queue.length) {
  const p = queue.shift();
  for (const d of refsOf(p)) if (!usedBy.has(d)) { usedBy.set(d, "referenciado por " + p); queue.push(d); }
}
// ---- protected (never candidates) ----
const docs = [];
for (const dir of ["Docs", "Tools/IronRequiem"]) {
  const base = path.join(project, dir); if (!fs.existsSync(base)) continue;
  for (const f of walk(base)) {
    const r = rel(f);
    if (/^Tools\/IronRequiem\/(Backups|Builds|Reports|Downloads|Exports|SourceModels|Music|Blender\/.*\.blend)/.test(r)) continue;
    if (!/\.(md|py|ps1|js|sh|txt|json)$/.test(r)) continue;
    docs.push([r, fs.readFileSync(f, "utf8")]);
  }
}
function protectedReason(p) {
  if (/\.(cs|asmdef|asmref|dll|so|jslib|rsp|shader|shadergraph|shadersubgraph|hlsl|cginc|compute|raytrace|uss|uxml|tss)$/.test(p)) return "código o shader (no candidato)";
  if (/^Assets\/(Plugins|TextMesh Pro|NavMeshComponents)\//.test(p)) return "tercero/paquete embebido (no candidato)";
  if (/^Assets\/FPS\/Tutorials\//.test(p)) return "tutorial IET: el paquete los carga por tipo";
  if (/\/Source\//.test(p)) return "modelo/archivo fuente (carpeta Source)";
  if (/^Assets\/[^/]+\.(asset|txt)$/.test(p) || /^Assets\/Rendering\//.test(p)) return "configuración de render/proyecto";
  if (/(^|\/)Editor\//.test(p)) return "recurso de Editor";
  if (/\.(md|txt|pdf)$/.test(p)) return "documentación";
  const name = path.basename(p);
  for (const [d, t] of docs) if (t.includes(p) || (name.length > 12 && t.includes(name))) return "citado en " + d + " (fuente/referencia)";
  return null;
}
const kind = p => {
  const e = path.extname(p).toLowerCase();
  if ([".fbx", ".obj", ".blend", ".dae", ".3ds", ".max"].includes(e)) return "modelo";
  if (e === ".prefab") return "prefab"; if (e === ".mat") return "material"; if (e === ".unity") return "escena";
  if ([".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".hdr", ".tif", ".tiff", ".bmp", ".gif"].includes(e)) return "textura";
  if ([".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac"].includes(e)) return "audio";
  if ([".anim", ".controller", ".overrideController", ".mask"].includes(e)) return "animación";
  if ([".cs", ".asmdef", ".dll"].includes(e)) return "código";
  if ([".shader", ".shadergraph", ".hlsl", ".cginc", ".compute", ".shadersubgraph"].includes(e)) return "shader";
  if ([".ttf", ".otf"].includes(e)) return "fuente";
  return "otro";
};
// Everything a protected file needs (tutorial scenes, a source model's materials...) is kept with it.
const keptBy = new Map();
for (const f of files) if (!usedBy.has(f)) { const pr = protectedReason(f); if (pr) keptBy.set(f, pr); }
const pq = [...keptBy.keys()];
while (pq.length) { const p = pq.shift(); for (const d of refsOf(p)) if (!usedBy.has(d) && !keptBy.has(d)) { keptBy.set(d, "dependencia de conservado " + p); pq.push(d); } }
const rows = []; let usedBytes = 0, candBytes = 0, protBytes = 0;
for (const f of files.sort()) {
  const size = fs.statSync(path.join(project, f)).size;
  let state, proof;
  if (usedBy.has(f)) { state = "en uso"; proof = usedBy.get(f); usedBytes += size; }
  else { const pr = keptBy.get(f); if (pr) { state = "conservado"; proof = pr; protBytes += size; } else { state = "sin uso"; proof = "ningún root lo alcanza por GUID; sin ruta/nombre en C#; no citado en Docs/Tools"; candBytes += size; } }
  rows.push({ path: f, kind: kind(f), bytes: size, state, proof });
}
const esc = s => '"' + String(s).replace(/"/g, '""') + '"';
fs.writeFileSync(path.join(out, "inventory.csv"), "ruta,tipo,bytes,estado,prueba\n" + rows.map(r => [r.path, r.kind, r.bytes, r.state, r.proof].map(esc).join(",")).join("\n"));
const folders = {};
for (const r of rows) { const parts = r.path.split("/"); const key = parts.slice(0, Math.min(3, parts.length - 1)).join("/"); const s = folders[key] ||= { files: 0, bytes: 0, enUso: 0, sinUso: 0, conservado: 0, sinUsoBytes: 0 }; s.files++; s.bytes += r.bytes; if (r.state === "en uso") s.enUso++; else if (r.state === "sin uso") { s.sinUso++; s.sinUsoBytes += r.bytes; } else s.conservado++; }
const byKind = {}; for (const r of rows) { const k = byKind[r.kind] ||= { total: 0, enUso: 0, sinUso: 0, conservado: 0 }; k.total++; k[r.state === "en uso" ? "enUso" : r.state === "sin uso" ? "sinUso" : "conservado"]++; }
fs.writeFileSync(path.join(out, "inventory-summary.json"), JSON.stringify({ utc: new Date().toISOString(), files: rows.length, roots: roots.size, usedBytes, candidateBytes: candBytes, protectedBytes: protBytes, byKind, folders }, null, 1));
console.log(JSON.stringify({ files: rows.length, roots: roots.size, used: rows.filter(r => r.state === "en uso").length, candidates: rows.filter(r => r.state === "sin uso").length, protectedCount: rows.filter(r => r.state === "conservado").length, candMB: (candBytes / 1048576).toFixed(1), byKind }, null, 1));
