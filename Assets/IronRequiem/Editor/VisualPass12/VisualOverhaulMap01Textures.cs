using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// Pase visual 12 · Map01 textures made outside the project (Art/Map01/Textures/Source, never modified).
    ///  Process          1024 px copies + normal maps derived from luminance, with import settings.
    ///  CreateMaterials  IronRequiem/HardSurface materials, world triplanar at 2 m per tile (the greybox cubes have 0..1 UVs).
    ///  ApplyRoom name   swaps the Bunker_* greybox materials of ONE Map01 room scene for the new ones. RevertRoom undoes it.
    /// The shared Bunker_* materials are not touched (the hangar and the old campaign use them).
    public static class VisualOverhaulMap01Textures
    {
        const string Source = "Assets/IronRequiem/Art/Map01/Textures/Source/";
        const string Out = "Assets/IronRequiem/Art/Map01/Textures/";
        const string Mats = "Assets/IronRequiem/Art/Map01/Materials/";
        const string Scenes = "Assets/IronRequiem/Scenes/Map01_Bunker/";
        const string ShaderName = "IronRequiem/HardSurface";
        const int Size = 1024;

        // name, metallic, smoothness, normal strength
        static readonly (string name, float metal, float smooth, float bump)[] Set =
        {
            ("M01_COM_Wall_Concrete_A", 0f, .15f, 2.5f), ("M01_COM_Wall_Panel_A", .6f, .4f, 2.5f), ("M01_COM_Floor_Concrete_A", 0f, .25f, 1.5f),
            ("M01_COM_Floor_Plate_A", .7f, .45f, 3f), ("M01_COM_Floor_Plate_A_Rust", .5f, .3f, 3f), ("M01_COM_Ceiling_Slab_A", 0f, .1f, 2f),
            ("M01_COM_Hazard_Stripes", .1f, .3f, 1f), ("M01_COM_Metal_Rust_A", .5f, .3f, 2.5f), ("M01_COM_Trim_Sheet", .6f, .4f, 3f),
            ("M01_COM_Door_Frame_Metal", .7f, .4f, 3f), ("M01_COM_Grating_A", .6f, .4f, 2f),
            // zone and room surfaces (blocks B-D)
            ("M01_T1_Wall_LabCeramic", 0f, .45f, 1.5f), ("M01_T1_Floor_LabEpoxy", 0f, .5f, 1f), ("M01_T1_Ceiling_CleanPanels", .2f, .35f, 2f),
            ("M01_T1_R01_ControlAcceso_Wall", .5f, .4f, 2.5f), ("M01_T1_R02_PozoMontacargas_Shaft", .5f, .35f, 2.5f), ("M01_T1_R04_Ensayos_Floor", .1f, .45f, 2f),
            ("M01_T1_R05_Diagnostico_Wall", .3f, .4f, 2f), ("M01_T2_Wall_ConcreteDirty", 0f, .12f, 2.5f), ("M01_T2_Floor_ConcreteOily", 0f, .35f, 2f),
            ("M01_T2_R10_Trituradora_Metal", .6f, .3f, 3f), ("M01_T3_Wall_ConcreteCracked", 0f, .1f, 3f), ("M01_T3_Floor_DustRubble", 0f, .08f, 2.5f),
            ("M01_T3_R14_Fundicion_Slag", .1f, .2f, 3f), ("M01_T3_R16_Reactor_Shield", .6f, .3f, 3f), ("M01_T3_Military_Olive", .5f, .3f, 2f),
            ("M01_T2_R11_Decantadores_Tank", .6f, .35f, 2.5f), ("M01_T2_R12_Colector_Wet", 0f, .4f, 2.5f), ("M01_T2_Wall_PanelTorn", .5f, .3f, 3f),
        };
        // greybox material -> candidates. Pick: the room's own texture, else the zone's, else the first (common block).
        static readonly (string from, string[] to)[] Surfaces =
        {
            ("Bunker_Floor", new[] { "M01_COM_Floor_Concrete_A", "M01_T1_Floor_LabEpoxy", "M01_T1_R04_Ensayos_Floor", "M01_T2_Floor_ConcreteOily", "M01_T3_Floor_DustRubble", "M01_T3_R14_Fundicion_Slag" }),
            ("Bunker_Wall", new[] { "M01_COM_Wall_Concrete_A", "M01_T1_Wall_LabCeramic", "M01_T1_R01_ControlAcceso_Wall", "M01_T1_R02_PozoMontacargas_Shaft", "M01_T1_R05_Diagnostico_Wall", "M01_T2_Wall_ConcreteDirty", "M01_T2_R11_Decantadores_Tank", "M01_T2_R12_Colector_Wet", "M01_T2_Wall_PanelTorn", "M01_T3_Wall_ConcreteCracked", "M01_T3_R16_Reactor_Shield" }),
            ("Bunker_Ceiling", new[] { "M01_COM_Ceiling_Slab_A", "M01_T1_Ceiling_CleanPanels" }),
            ("Bunker_RustMetal", new[] { "M01_COM_Metal_Rust_A", "M01_T2_R10_Trituradora_Metal", "M01_T3_Military_Olive" }),
            ("Bunker_Hazard", new[] { "M01_COM_Hazard_Stripes" }),
        };
        static string Pick(string[] c, string path, int tramo)
        {
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path), "_R([0-9]+)_");
            string room = m.Success ? "_R" + m.Groups[1].Value + "_" : "?";
            foreach (var n in c) if (n.Contains(room)) return n;
            if ((room == "_R07_" || room == "_R09_") && Array.IndexOf(c, "M01_T2_Wall_PanelTorn") >= 0) return "M01_T2_Wall_PanelTorn"; // variety inside the zone
            foreach (var n in c) if (n.StartsWith("M01_T" + tramo + "_") && n != "M01_T2_Wall_PanelTorn" && !System.Text.RegularExpressions.Regex.IsMatch(n, "_R[0-9]+_")) return n;
            return c[0];
        }
        // greybox material -> new material
        static readonly (string from, string to)[] Swap =
        {
            ("Bunker_Floor", "M01_COM_Floor_Concrete_A"), ("Bunker_Wall", "M01_COM_Wall_Concrete_A"), ("Bunker_Ceiling", "M01_COM_Ceiling_Slab_A"),
            ("Bunker_RustMetal", "M01_COM_Metal_Rust_A"), ("Bunker_Hazard", "M01_COM_Hazard_Stripes"),
        };

        public static string Process()
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(Out);
            foreach (var s in Set)
            {
                string src = Source + s.name + ".png";
                if (!File.Exists(src)) { log.AppendLine("FALTA " + src); continue; }
                if (File.Exists(Out + s.name + ".png") && File.Exists(Out + s.name + "_N.png")) { log.AppendLine("ya estaba " + s.name); continue; }
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!raw.LoadImage(File.ReadAllBytes(src))) { log.AppendLine("NO SE LEE " + src); continue; }
                int w = raw.width, h = raw.height; var px = raw.GetPixels32();
                int crop = Mathf.Min(w, h); string note = "";
                if (s.name.Contains("Hazard")) { int p = Period(px, w, h); int n = p > 0 ? h / p : 0; if (n > 0) { crop = Mathf.Min(crop, n * p); note = " periodo " + p + " px, recorte " + crop; } }
                var col = Resample(px, w, h, crop, Size);
                Save(Out + s.name + ".png", col);
                Save(Out + s.name + "_N.png", Normal(col, Size, s.bump));
                UnityEngine.Object.DestroyImmediate(raw);
                log.AppendLine($"{s.name}: {w}x{h} -> {Size}{note}");
            }
            AssetDatabase.Refresh();
            foreach (var s in Set)
            {
                Import(Out + s.name + ".png", false); Import(Out + s.name + "_N.png", true);
            }
            return log.ToString();
        }

        public static string CreateMaterials()
        {
            var log = new StringBuilder();
            var shader = Shader.Find(ShaderName); if (!shader) throw new InvalidOperationException("Sin shader " + ShaderName);
            Directory.CreateDirectory(Mats); AssetDatabase.Refresh();
            foreach (var s in Set)
            {
                if (s.name.Contains("Grating")) continue; // needs cutout; not used by the greybox
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Out + s.name + ".png"); var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(Out + s.name + "_N.png");
                if (!tex) { log.AppendLine("sin textura " + s.name); continue; }
                string path = Mats + s.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool fresh = !m;
                if (fresh) m = new Material(shader) { name = s.name };
                m.shader = shader;
                m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Metallic", s.metal); m.SetFloat("_Smoothness", s.smooth);
                m.SetFloat("_UseTriplanar", 1); m.EnableKeyword("_TRIPLANAR"); m.SetFloat("_TriplanarScale", 2f); m.SetFloat("_TriplanarSharpness", 8f);
                if (nrm) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_UseNormalMap", 1); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); }
                m.enableInstancing = true;
                if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                log.AppendLine((fresh ? "creado " : "actualizado ") + path);
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        public static string ApplyRoom(string room) => SwapRoom(RoomPath(room), false);
        public static string RevertRoom(string room) => SwapRoom(RoomPath(room), true);

        /// Every Map01_R*.unity room. A room that is open in the Editor is skipped and reported, never forced.
        public static string ApplyAll() => All(false);
        public static string RevertAll() => All(true);
        static string All(bool back)
        {
            var log = new StringBuilder();
            foreach (var file in Directory.GetFiles(Scenes, "Map01_R*.unity"))
            {
                string path = Scenes + Path.GetFileName(file);
                try { log.Append(SwapRoom(path, back)); }
                catch (Exception e) { log.AppendLine("SALTADO " + path + ": " + e.Message); }
            }
            return log.ToString();
        }

        /// Copies one room scene to Art/Map01/Preview and applies the new materials to the COPY. The real room is not touched.
        public static string Preview(string room)
        {
            string src = RoomPath(room); const string folder = "Assets/IronRequiem/Art/Map01/Preview";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string dst = folder + "/" + room.Trim() + "_Texturas.unity";
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == dst) throw new InvalidOperationException("Cierra primero la vista previa: " + dst);
            AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(src, dst)) throw new InvalidOperationException("No se pudo copiar " + src);
            return "copia " + dst + System.Environment.NewLine + SwapRoom(dst, false);
        }

        /// Zone of a room from its number: R01-R05 deep labs, R06-R13 infrastructure, R14-R18 near the surface.
        static int Tramo(string path)
        {
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path), "_R([0-9]+)_");
            int n = m.Success ? int.Parse(m.Groups[1].Value) : 1;
            return n <= 5 ? 1 : n <= 13 ? 2 : 3;
        }

        // zone grime: amount, colour, wear. Deep labs are dusty but intact; the middle is sooty and greasy; the top is covered in pale dust.
        static readonly (float amount, Color color, float scale, float wear)[] Grime =
        {
            (.56f, new Color(.08f, .09f, .10f), 3.0f, .35f),
            (.78f, new Color(.10f, .075f, .055f), 2.2f, .65f),
            (.72f, new Color(.36f, .33f, .28f), 2.6f, .55f),
        };

        /// One dirty variant per zone of every Map01 material (world-space grime of the HardSurface shader, no new textures).
        public static string CreateDirtVariants()
        {
            var log = new StringBuilder();
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IronRequiem/Art/VisualOverhaul/Textures/VO_Noise.png");
            if (!noise) throw new InvalidOperationException("Sin VO_Noise.png");
            foreach (var s in Set)
            {
                var clean = AssetDatabase.LoadAssetAtPath<Material>(Mats + s.name + ".mat"); if (!clean) continue;
                for (int t = 1; t <= 3; t++)
                {
                    string path = Mats + s.name + "_T" + t + ".mat"; var g = Grime[t - 1];
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool fresh = !m;
                    if (fresh) m = new Material(clean); else m.CopyPropertiesFromMaterial(clean);
                    m.name = s.name + "_T" + t;
                    bool painted = s.name.Contains("Hazard");
                    m.SetTexture("_NoiseMap", noise); m.SetFloat("_UseDirt", 1); m.EnableKeyword("_DIRT");
                    bool alreadyDirty = s.name.StartsWith("M01_T2_") || s.name.StartsWith("M01_T3_"); // painted dirty: the shader only adds the large uneven stains
                    m.SetColor("_DirtColor", g.color); m.SetFloat("_DirtAmount", painted ? g.amount * .8f : alreadyDirty ? g.amount * .6f : g.amount); m.SetFloat("_DirtScale", g.scale);
                    m.SetFloat("_UseDirtBreakup", 1); m.EnableKeyword("_DIRTBREAKUP"); m.SetFloat("_DirtBreakup", 1f);
                    m.SetFloat("_DirtScale", g.scale * (1f + .23f * (Mathf.Abs(s.name.GetHashCode()) % 5 - 2) / 2f)); // each surface its own scale: wall, floor and ceiling stains do not line up
                    m.SetFloat("_DirtFloorY", -100000f); m.SetFloat("_DirtFloorHeight", 1f); // rooms sit at many heights: no floor band, grime by noise and on upward faces
                    if (s.metal > .3f) { m.SetFloat("_UseEdgeWear", 1); m.EnableKeyword("_EDGEWEAR"); m.SetFloat("_WearAmount", g.wear); }
                    m.SetFloat("_Smoothness", s.smooth * (t == 1 ? .9f : .65f));
                    if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
                    log.AppendLine((fresh ? "creado " : "actualizado ") + path);
                }
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static string RoomPath(string room)
        {
            if (string.IsNullOrWhiteSpace(room)) throw new ArgumentException("Falta el nombre del cuarto, p. ej. Map01_R01_ControlAcceso");
            return Scenes + room.Trim() + ".unity";
        }

        static string SwapRoom(string path, bool back)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            // an open scene is edited in place, but only when it has no unsaved changes (nothing of the user's can be lost)
            Scene opened = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == path) opened = SceneManager.GetSceneAt(i);
            if (opened.IsValid() && opened.isDirty) throw new InvalidOperationException("La escena está abierta con cambios sin guardar; guárdala o descártalos primero: " + path);
            var map = new Dictionary<Material, Material>(); var picked = new StringBuilder();
            int tramo = Tramo(path);
            foreach (var (from, cands) in Surfaces)
            {
                string to = Pick(cands, path, tramo);
                var bunker = AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/" + from + ".mat"); var clean = AssetDatabase.LoadAssetAtPath<Material>(Mats + to + ".mat");
                if (!bunker || !clean) throw new InvalidOperationException("Falta material " + from + " o " + to);
                var dirty = AssetDatabase.LoadAssetAtPath<Material>(Mats + to + "_T" + tramo + ".mat");
                var target = back ? bunker : dirty ? dirty : clean;
                // any state of this surface (greybox, any candidate, clean or zone variant) goes to the target
                var sources = new List<Material> { bunker };
                foreach (var c in cands)
                {
                    var v0 = AssetDatabase.LoadAssetAtPath<Material>(Mats + c + ".mat"); if (v0) sources.Add(v0);
                    for (int t = 1; t <= 3; t++) { var v = AssetDatabase.LoadAssetAtPath<Material>(Mats + c + "_T" + t + ".mat"); if (v) sources.Add(v); }
                }
                foreach (var src in sources) if (src != target) map[src] = target;
                picked.AppendLine(from + " -> " + target.name);
            }
            var log = new StringBuilder(); var count = new Dictionary<string, int>();
            var scene = opened.IsValid() ? opened : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int n = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(r)) continue; // doors and props keep their prefab materials
                        var mats = r.sharedMaterials; bool changed = false;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] && map.TryGetValue(mats[i], out var to)) { count[mats[i].name] = (count.TryGetValue(mats[i].name, out int c) ? c : 0) + 1; mats[i] = to; changed = true; }
                        if (changed) { r.sharedMaterials = mats; n++; }
                    }
                log.Append(picked);
                foreach (var kv in count) log.AppendLine(kv.Key + ": " + kv.Value);
                if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se pudo guardar " + path); }
                log.AppendLine((n > 0 ? "GUARDADO " : "sin cambios ") + path + " · " + n + " renderers");
            }
            finally { if (!opened.IsValid()) EditorSceneManager.CloseScene(scene, true); }
            return log.ToString();
        }

        // ---- image helpers (CPU, deterministic)
        static float Lum(Color32 c) => (.2126f * c.r + .7152f * c.g + .0722f * c.b) / 255f;

        /// Vertical period of the stripes along the left column (0 if none found).
        static int Period(Color32[] px, int w, int h)
        {
            int best = 0; float bestErr = float.MaxValue;
            for (int p = h / 6; p <= h / 2; p++)
            {
                float err = 0; int n = 0;
                for (int y = 0; y + p < h; y += 2) { err += Mathf.Abs(Lum(px[y * w]) - Lum(px[(y + p) * w])); n++; }
                err /= Mathf.Max(1, n);
                if (err < bestErr - 1e-4f) { bestErr = err; best = p; }
            }
            return bestErr < .08f ? best : 0;
        }

        /// Bilinear resample of the top-left crop x crop square (texture rows are bottom-up: the crop keeps the top rows).
        static Color32[] Resample(Color32[] px, int w, int h, int crop, int size)
        {
            var o = new Color32[size * size]; int y0 = h - crop; float k = (float)crop / size;
            for (int y = 0; y < size; y++)
            {
                float fy = (y + .5f) * k - .5f; int ya = Mathf.Clamp(Mathf.FloorToInt(fy), 0, crop - 1), yb = Mathf.Min(ya + 1, crop - 1); float ty = Mathf.Clamp01(fy - ya);
                for (int x = 0; x < size; x++)
                {
                    float fx = (x + .5f) * k - .5f; int xa = Mathf.Clamp(Mathf.FloorToInt(fx), 0, crop - 1), xb = Mathf.Min(xa + 1, crop - 1); float tx = Mathf.Clamp01(fx - xa);
                    Color a = px[(y0 + ya) * w + xa], b = px[(y0 + ya) * w + xb], c = px[(y0 + yb) * w + xa], d = px[(y0 + yb) * w + xb];
                    Color v = Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty); v.a = 1;
                    o[y * size + x] = v;
                }
            }
            return o;
        }

        /// Tangent-space normal from luminance as height (Sobel, wrapped so the map tiles).
        static Color32[] Normal(Color32[] col, int size, float strength)
        {
            var hgt = new float[size * size]; for (int i = 0; i < hgt.Length; i++) hgt[i] = Lum(col[i]);
            var o = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int yu = (y + 1) % size, yd = (y + size - 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xr = (x + 1) % size, xl = (x + size - 1) % size;
                    float dx = (hgt[yu * size + xr] + 2 * hgt[y * size + xr] + hgt[yd * size + xr]) - (hgt[yu * size + xl] + 2 * hgt[y * size + xl] + hgt[yd * size + xl]);
                    float dy = (hgt[yu * size + xl] + 2 * hgt[yu * size + x] + hgt[yu * size + xr]) - (hgt[yd * size + xl] + 2 * hgt[yd * size + x] + hgt[yd * size + xr]);
                    var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    o[y * size + x] = new Color32((byte)(n.x * 127.5f + 127.5f), (byte)(n.y * 127.5f + 127.5f), (byte)(n.z * 127.5f + 127.5f), 255);
                }
            }
            return o;
        }

        static void Save(string path, Color32[] px)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply(false);
            File.WriteAllBytes(path, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
        }

        static void Import(string path, bool normal)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter; if (!imp) return;
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; imp.sRGBTexture = !normal;
            imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.anisoLevel = 8; imp.maxTextureSize = Size; imp.alphaSource = TextureImporterAlphaSource.None;
            imp.SaveAndReimport();
        }
    }
}
