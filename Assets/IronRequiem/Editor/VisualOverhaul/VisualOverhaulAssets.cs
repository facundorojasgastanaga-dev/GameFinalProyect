using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): shared generated assets. Procedural textures, IronRequiem/HardSurface materials and a
    // tiny mesh kit (lathe / box / tube). Everything lives under Assets/IronRequiem/Art/VisualOverhaul; nothing existing
    // is modified. Re-running regenerates in place (same paths, same GUIDs).
    public static class VisualOverhaulAssets
    {
        public const string Root = "Assets/IronRequiem/Art/VisualOverhaul";
        public const string ShaderName = "IronRequiem/HardSurface";
        public const string BunkerArt = "Assets/IronRequiem/Art/Environment/Bunker";
        public static string Tex => Root + "/Textures";
        public static string Mats => Root + "/Materials";
        public static string Meshes => Root + "/Meshes";

        // ---------------- noise ----------------
        static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 1442695040; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float Noise(float x, float y, int s, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int W(int v) => ((v % period) + period) % period;
            float a = Hash(W(x0), W(y0), s), b = Hash(W(x0 + 1), W(y0), s), c = Hash(W(x0), W(y0 + 1), s), d = Hash(W(x0 + 1), W(y0 + 1), s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        public static float Fbm(float u, float v, int s, int baseFreq = 4, int octaves = 5)
        { float sum = 0, amp = .5f, norm = 0; int f = baseFreq; for (int o = 0; o < octaves; o++) { sum += amp * Noise(u * f, v * f, s + o * 17, f); norm += amp; amp *= .5f; f *= 2; } return sum / norm; }

        static Texture2D Save(string name, int size, Func<int, int, Color> pixel, bool linear, bool normal = false, bool clamp = false, bool alpha = false)
        {
            string path = Tex + "/" + name + ".png"; Directory.CreateDirectory(Tex);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = pixel(x, y);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false, linear); t.SetPixels(px); t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path); var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; imp.sRGBTexture = !linear && !normal;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat; imp.anisoLevel = 4; imp.maxTextureSize = size; imp.mipmapEnabled = true;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None; imp.alphaIsTransparency = alpha;
            imp.textureCompression = TextureImporterCompression.Compressed; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D NormalFromHeight(string name, int size, float[] h, float bump)
        {
            return Save(name, size, (x, y) =>
            {
                float H(int dx, int dy) => h[((y + dy + size) % size) * size + (x + dx + size) % size];
                var n = new Vector3((H(-1, 0) - H(1, 0)) * bump, (H(0, -1) - H(0, 1)) * bump, 1).normalized;
                return new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }, true, true);
        }

        public static Texture2D T(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + name + ".png");

        public static string BuildTextures()
        {
            Directory.CreateDirectory(Tex);
            // Tileable noise: R broad fbm, G fine fbm, B streaks. Linear data.
            Save("VO_Noise", 256, (x, y) => { float u = x / 256f, v = y / 256f; return new Color(Fbm(u, v, 5, 4, 5), Fbm(u, v, 9, 16, 3), Fbm(u * 6, v * .5f, 13, 4, 3), 1); }, true);
            Save("VO_Mask_PanelsOnly", 4, (x, y) => new Color(1, 0, 0, 1), true);
            Save("VO_Mask_PanelsWear", 4, (x, y) => new Color(1, 1, 0, 1), true);

            // Suit plates in UV space (module meshes): 4x4 plates, 6 px seams, chips, a few cyan slit lights.
            const int S = 512; var height = new float[S * S];
            var albedo = new Color[S * S]; var mask = new Color[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S; float pu = Mathf.Repeat(u * 4, 1), pv = Mathf.Repeat(v * 4, 1);
                int cell = Mathf.FloorToInt(u * 4) + Mathf.FloorToInt(v * 4) * 4;
                float edge = Mathf.Min(Mathf.Min(pu, 1 - pu), Mathf.Min(pv, 1 - pv));
                float seam = edge < .012f ? 1 : 0, bevel = Mathf.Clamp01(1 - edge / .05f);
                float grain = Fbm(u, v, 21, 8, 4), chips = Fbm(u, v, 37, 16, 3);
                var plate = new Color(.86f, .82f, .74f) * (.88f + grain * .2f);
                bool chip = chips > .66f && bevel > .2f; // chipped ceramic near plate edges shows graphite underneath
                var c = seam > 0 ? new Color(.08f, .08f, .085f) : chip ? new Color(.2f, .2f, .21f) : plate;
                // slit lights in two of the plates
                bool slit = (cell == 5 || cell == 10) && Mathf.Abs(pv - .5f) < .035f && pu > .2f && pu < .8f;
                if (slit) c = new Color(.15f, .5f, .55f);
                c.a = 1; albedo[y * S + x] = c;
                height[y * S + x] = seam > 0 ? 0 : slit ? .35f : .8f - bevel * .35f - (chip ? .25f : 0);
                mask[y * S + x] = new Color(slit ? 1 : 0, Mathf.Clamp01(bevel * 1.3f + (chip ? .6f : 0)), seam > 0 ? 1 : bevel * .25f, 1);
            }
            Save("VO_Suit_Albedo", S, (x, y) => albedo[y * S + x], false);
            Save("VO_Suit_Mask", S, (x, y) => mask[y * S + x], true);
            NormalFromHeight("VO_Suit_Normal", S, height, 3.5f);

            // Decals (RGBA, clamp, alpha from input).
            Save("VO_Decal_Stain", 256, (x, y) => { float u = x / 256f - .5f, v = y / 256f - .5f; float r = Mathf.Sqrt(u * u + v * v) * 2; float n = Fbm(x / 256f, y / 256f, 61, 4, 5); float a = Mathf.Clamp01((1 - r) * 1.6f + (n - .5f) * 1.4f - .25f); return new Color(.09f, .075f, .05f, a * .85f); }, false, false, true, true);
            Save("VO_Decal_Rust", 256, (x, y) => { float u = x / 256f, v = y / 256f; float col = Mathf.Abs(u - .5f) * 2; float streak = Fbm(u * 3, v * .4f, 71, 4, 4); float a = Mathf.Clamp01((1 - col) * 1.3f * Mathf.Pow(v, .7f) + (streak - .55f) * 2 - .2f) * Mathf.Clamp01((1 - col) * 3); return new Color(.42f, .19f, .06f, a * .8f); }, false, false, true, true);
            Save("VO_Decal_Burn", 256, (x, y) => { float u = x / 256f - .5f, v = y / 256f - .5f; float r = Mathf.Sqrt(u * u + v * v) * 2; float ang = Mathf.Atan2(v, u); float spikes = .5f + .5f * Mathf.Sin(ang * 9 + Fbm(x / 256f, y / 256f, 83, 4, 3) * 6); float a = Mathf.Clamp01(1.2f - r * (1.1f + spikes * .5f)); return new Color(.02f, .018f, .015f, Mathf.Clamp01(a * 1.4f)); }, false, false, true, true);
            Save("VO_Decal_Hazard", 256, (x, y) =>
            {
                // Original warning sign: yellow triangle, black border, black bar + dot, worn by noise.
                float u = x / 256f, v = y / 256f; float wear = Fbm(u, v, 91, 6, 4);
                float tx = Mathf.Abs(u - .5f), ty = v - .1f; bool inTri = ty > 0 && ty < .8f && tx < (.8f - ty) * .62f;
                bool inInner = ty > .07f && ty < .7f && tx < (.7f - ty) * .62f - .02f;
                bool bar = tx < .035f && ty > .28f && ty < .58f, dot = (new Vector2(u - .5f, ty - .19f)).magnitude < .045f;
                if (!inTri) return new Color(0, 0, 0, 0);
                var c = inInner && !bar && !dot ? new Color(.9f, .68f, .06f) : new Color(.05f, .05f, .05f);
                return new Color(c.r, c.g, c.b, wear > .7f ? .35f : .95f);
            }, false, false, true, true);
            AssetDatabase.SaveAssets();
            return "textures ok";
        }

        // ---------------- materials ----------------
        public static Material Mat(string name, Action<Material> setup)
        {
            Directory.CreateDirectory(Mats);
            string path = Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(ShaderName); if (!shader) throw new Exception("Shader " + ShaderName + " not found (compile errors?)");
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader; m.shaderKeywords = new string[0];
            m.SetTexture("_NoiseMap", T("VO_Noise")); m.enableInstancing = true; m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            setup(m); EditorUtility.SetDirty(m); return m;
        }
        public static void Feature(Material m, string keyword, string toggle, bool on) { m.SetFloat(toggle, on ? 1 : 0); if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword); }
        public static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Mats + "/" + name + ".mat");

        static Texture2D Bunker(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(BunkerArt + "/Textures/" + n + ".png");

        static void Env(Material m, string tex, float metres, float smooth, float metal, bool triplanar, bool wear, float dirt)
        {
            m.SetTexture("_BaseMap", Bunker(tex + "_Albedo")); m.SetTexture("_BumpMap", Bunker(tex + "_Normal")); Feature(m, "_NORMALMAP", "_UseNormalMap", true);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); m.SetFloat("_BumpScale", 1);
            Feature(m, "_TRIPLANAR", "_UseTriplanar", triplanar); m.SetFloat("_TriplanarScale", metres); m.SetTextureScale("_BaseMap", Vector2.one / metres);
            Feature(m, "_DIRT", "_UseDirt", true); m.SetFloat("_DirtAmount", dirt); m.SetFloat("_DirtScale", 2.2f);
            Feature(m, "_EDGEWEAR", "_UseEdgeWear", true); m.SetFloat("_WearAmount", wear ? .55f : 0f);
        }

        static void Panel(Material m, Color emission, float pulseSpeed, float pulse)
        {
            m.SetColor("_BaseColor", new Color(.25f, .25f, .25f)); m.SetFloat("_Smoothness", .6f); m.SetFloat("_Metallic", 0);
            Feature(m, "_PANELS", "_UsePanels", true); m.SetTexture("_MaskMap", T("VO_Mask_PanelsOnly")); m.SetColor("_EmissionColor", emission);
            m.SetFloat("_PulseSpeed", pulseSpeed); m.SetFloat("_PulseAmount", pulse);
        }

        public static string BuildMaterials()
        {
            // Environment, triplanar (meshes whose UVs are not world-scaled: MainScene template pieces).
            Mat("VO_Env_FloorTri", m => Env(m, "Bunker_ConcreteFloor", 4, .25f, 0, true, false, .45f));
            Mat("VO_Env_WallTri", m => Env(m, "Bunker_ConcreteWall", 3, .15f, 0, true, false, .35f));
            Mat("VO_Env_MetalTri", m => Env(m, "Bunker_RustMetal", 2, .35f, .55f, true, true, .3f));
            Mat("VO_Env_HazardTri", m => Env(m, "Bunker_Hazard", 1.5f, .3f, .1f, true, true, .2f));
            // Environment, UV (world-scaled UV tiles of the bunker scenes; cheaper than triplanar).
            Mat("VO_Env_Floor", m => Env(m, "Bunker_ConcreteFloor", 4, .25f, 0, false, false, .45f));
            Mat("VO_Env_Wall", m => Env(m, "Bunker_ConcreteWall", 3, .15f, 0, false, false, .35f));
            Mat("VO_Env_Ceiling", m => Env(m, "Bunker_ConcreteWall", 4, .1f, 0, false, false, .2f));
            Mat("VO_Env_RustMetal", m => Env(m, "Bunker_RustMetal", 2, .35f, .55f, false, true, .3f));
            Mat("VO_Env_Hazard", m => Env(m, "Bunker_Hazard", 1.5f, .3f, .1f, false, true, .2f));
            Mat("VO_Env_Grating", m => Env(m, "Bunker_Grating", 2, .4f, .6f, false, true, .25f));
            // Props (procedural meshes with their own UVs).
            Mat("VO_Prop_Pipe", m => { Env(m, "Bunker_RustMetal", 1, .45f, .7f, false, true, .25f); m.SetColor("_BaseColor", new Color(.75f, .78f, .8f)); m.SetFloat("_WearCurvature", 9); });
            Mat("VO_Prop_Cable", m => { m.SetColor("_BaseColor", new Color(.05f, .05f, .055f)); m.SetFloat("_Smoothness", .45f); m.SetFloat("_Metallic", 0); Feature(m, "_DIRT", "_UseDirt", true); m.SetFloat("_DirtAmount", .25f); });
            Mat("VO_Prop_Housing", m => { Env(m, "Bunker_RustMetal", 1, .3f, .6f, false, true, .3f); m.SetColor("_BaseColor", new Color(.45f, .47f, .5f)); });
            Mat("VO_Light_Cold", m => Panel(m, new Color(.75f, .9f, 1f) * 6f, .15f, .05f));
            Mat("VO_Light_Red", m => Panel(m, new Color(1f, .1f, .04f) * 7f, .9f, .6f));
            Mat("VO_Light_Amber", m => Panel(m, new Color(1f, .5f, .08f) * 6f, .5f, .35f));
            Mat("VO_Light_Dead", m => Panel(m, new Color(.75f, .9f, 1f) * .05f, 0, 0));
            // Suit (player modules): same palette as the protagonist: bone ceramic plates, graphite joints,
            // orange accents and cyan lights.
            void Suit(Material m, Color tint, float smooth, float metal)
            {
                m.SetTexture("_BaseMap", T("VO_Suit_Albedo")); m.SetTexture("_BumpMap", T("VO_Suit_Normal")); m.SetTexture("_MaskMap", T("VO_Suit_Mask"));
                Feature(m, "_NORMALMAP", "_UseNormalMap", true); m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
                Feature(m, "_EDGEWEAR", "_UseEdgeWear", true); m.SetFloat("_WearFromMask", .7f); m.SetFloat("_WearAmount", .5f); m.SetColor("_WearColor", new Color(.3f, .3f, .32f));
            }
            Mat("VO_Suit_Plate", m => { Suit(m, Color.white, .45f, .05f); Feature(m, "_PANELS", "_UsePanels", true); m.SetColor("_EmissionColor", new Color(.1f, .9f, 1.1f) * 2.5f); m.SetFloat("_PulseSpeed", .4f); m.SetFloat("_PulseAmount", .25f); });
            Mat("VO_Suit_Joint", m => { Suit(m, new Color(.2f, .2f, .22f), .55f, .6f); m.SetColor("_WearColor", new Color(.55f, .55f, .55f)); });
            Mat("VO_Suit_Accent", m => Suit(m, new Color(1f, .45f, .12f), .4f, .05f));
            Mat("VO_Suit_Light", m => Panel(m, new Color(.1f, .9f, 1.2f) * 4f, .5f, .25f));
            Mat("VO_Suit_Ring", m => { Panel(m, new Color(.1f, .85f, 1.2f) * 1.6f, .3f, .15f); m.SetColor("_BaseColor", new Color(.12f, .14f, .15f)); m.SetFloat("_Metallic", .6f); });
            AssetDatabase.SaveAssets();
            return "materials ok: " + Directory.GetFiles(Mats, "*.mat").Length;
        }

        // Converts a URP/Lit (or Simple Lit) material to an equivalent HardSurface material (cached by name).
        public static Material ConvertLit(Material source, string folder, bool rim, bool wear, float damage = 0, string suffix = "")
        {
            if (!source) return null;
            if (source.shader && source.shader.name == ShaderName && damage <= 0) return source;
            Directory.CreateDirectory(folder);
            string path = folder + "/" + source.name.Replace("/", "_") + "_HS" + suffix + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(ShaderName);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader; m.shaderKeywords = new string[0]; m.enableInstancing = true; m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Texture Tex(string p) => source.HasProperty(p) ? source.GetTexture(p) : null;
            Color Col(string p, Color d) => source.HasProperty(p) ? source.GetColor(p) : d;
            float F(string p, float d) => source.HasProperty(p) ? source.GetFloat(p) : d;
            var baseMap = Tex("_BaseMap") ?? Tex("_MainTex");
            m.SetTexture("_BaseMap", baseMap); m.SetTextureScale("_BaseMap", source.HasProperty("_BaseMap") ? source.GetTextureScale("_BaseMap") : Vector2.one); m.SetTextureOffset("_BaseMap", source.HasProperty("_BaseMap") ? source.GetTextureOffset("_BaseMap") : Vector2.zero);
            m.SetColor("_BaseColor", Col("_BaseColor", Col("_Color", Color.white)));
            var bump = Tex("_BumpMap"); m.SetTexture("_BumpMap", bump); Feature(m, "_NORMALMAP", "_UseNormalMap", bump != null); m.SetFloat("_BumpScale", F("_BumpScale", 1));
            var mg = Tex("_MetallicGlossMap"); m.SetTexture("_MetallicGlossMap", mg);
            m.SetFloat("_Metallic", mg ? 1 : F("_Metallic", 0)); m.SetFloat("_Smoothness", F("_Smoothness", F("_Glossiness", .5f)));
            bool emissive = source.IsKeywordEnabled("_EMISSION") && Col("_EmissionColor", Color.black).maxColorComponent > .01f;
            Feature(m, "_PANELS", "_UsePanels", true);
            if (emissive) { m.SetTexture("_EmissionMap", Tex("_EmissionMap")); m.SetTexture("_MaskMap", T("VO_Mask_PanelsOnly")); m.SetColor("_EmissionColor", Col("_EmissionColor", Color.black)); m.SetFloat("_PulseSpeed", .35f); m.SetFloat("_PulseAmount", .3f); }
            else { m.SetTexture("_MaskMap", null); m.SetColor("_EmissionColor", Color.black); }
            Feature(m, "_EDGEWEAR", "_UseEdgeWear", wear); m.SetFloat("_WearAmount", .35f); m.SetFloat("_WearCurvature", 2.2f); m.SetColor("_WearColor", new Color(.42f, .4f, .37f));
            Feature(m, "_RIM", "_UseRim", rim); m.SetColor("_RimColor", new Color(.12f, .22f, .32f)); m.SetFloat("_RimPower", 4f);
            m.SetFloat("_Damage", damage);
            m.SetFloat("_Cull", F("_Cull", 2));
            m.SetTexture("_NoiseMap", T("VO_Noise"));
            EditorUtility.SetDirty(m);
            return m;
        }

        // One keyword set for every robot material (NORMALMAP if it has one, PANELS, EDGEWEAR, RIM) so SRP Batcher batches stay long.
        public static string UnifyEnemyKeywords()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Mats + "/Enemies" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)); if (!m || m.shader.name != ShaderName) continue;
                if (!m.IsKeywordEnabled("_PANELS")) { Feature(m, "_PANELS", "_UsePanels", true); if (!m.GetTexture("_EmissionMap")) m.SetColor("_EmissionColor", Color.black); }
                Feature(m, "_RIM", "_UseRim", true); Feature(m, "_EDGEWEAR", "_UseEdgeWear", true);
                // dense robot meshes: curvature wear only on real edges, darker steel (avoids a chrome look)
                m.SetFloat("_WearCurvature", 2.2f); m.SetFloat("_WearAmount", .35f); m.SetColor("_WearColor", new Color(.42f, .4f, .37f)); m.SetColor("_RimColor", new Color(.1f, .18f, .26f));
                EditorUtility.SetDirty(m); n++;
            }
            AssetDatabase.SaveAssets(); return "unified " + n;
        }

        // ---------------- mesh kit ----------------
        public sealed class MeshKit
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector3> N = new List<Vector3>(); public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<List<int>> Sub = new List<List<int>>();
            public MeshKit(int submeshes) { for (int i = 0; i < submeshes; i++) Sub.Add(new List<int>()); }
            // Revolve a profile (x = radius, y = position along the axis) around local Z; hard edges between profile segments.
            public void Lathe(Matrix4x4 m, Vector2[] profile, int segments, int sub, float uvScale = 1, bool capStart = false, bool capEnd = false)
            {
                for (int p = 0; p < profile.Length - 1; p++)
                {
                    Vector2 a = profile[p], b = profile[p + 1]; var d = b - a; var pn = new Vector2(d.y, -d.x).normalized; // outward in (r, z)
                    int start = V.Count; float len = d.magnitude, vA = Accum(profile, p) * uvScale, vB = vA + len * uvScale;
                    for (int s = 0; s <= segments; s++)
                    {
                        float ang = s / (float)segments * Mathf.PI * 2; float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        var nrm = new Vector3(cs * pn.x, sn * pn.x, pn.y);
                        Add(m, new Vector3(cs * a.x, sn * a.x, a.y), nrm, new Vector2(s / (float)segments * uvScale * 2, vA));
                        Add(m, new Vector3(cs * b.x, sn * b.x, b.y), nrm, new Vector2(s / (float)segments * uvScale * 2, vB));
                    }
                    for (int s = 0; s < segments; s++) { int i = start + s * 2; Tri(sub, i, i + 1, i + 3); Tri(sub, i, i + 3, i + 2); }
                }
                if (capStart) Cap(m, profile[0], segments, sub, -1);
                if (capEnd) Cap(m, profile[profile.Length - 1], segments, sub, 1);
            }
            static float Accum(Vector2[] p, int upto) { float s = 0; for (int i = 0; i < upto; i++) s += (p[i + 1] - p[i]).magnitude; return s; }
            void Cap(Matrix4x4 m, Vector2 ring, int segments, int sub, int dir)
            {
                int c = V.Count; Add(m, new Vector3(0, 0, ring.y), new Vector3(0, 0, dir), new Vector2(.5f, .5f));
                for (int s = 0; s <= segments; s++) { float ang = s / (float)segments * Mathf.PI * 2; Add(m, new Vector3(Mathf.Cos(ang) * ring.x, Mathf.Sin(ang) * ring.x, ring.y), new Vector3(0, 0, dir), new Vector2(.5f + Mathf.Cos(ang) * .5f, .5f + Mathf.Sin(ang) * .5f)); }
                for (int s = 0; s < segments; s++) Tri(sub, c, c + 1 + s, c + 2 + s);
            }
            public void Box(Matrix4x4 m, Vector3 size, int sub, float uvScale = 1)
            {
                void Face(Vector3 n, Vector3 up)
                {
                    var right = Vector3.Cross(up, n); int b = V.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = (i == 1 || i == 2) ? .5f : -.5f, sy = i >= 2 ? .5f : -.5f;
                        var p = Vector3.Scale(n * .5f + right * sx + up * sy, size);
                        Add(m, p, n, new Vector2(Vector3.Dot(p, right) * uvScale, Vector3.Dot(p, up) * uvScale));
                    }
                    bool cw = Vector3.Dot(Vector3.Cross(V[b + 2] - V[b], V[b + 1] - V[b]), m.MultiplyVector(n)) > 0;
                    Sub[sub].AddRange(cw ? new[] { b, b + 2, b + 1, b, b + 3, b + 2 } : new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
                Face(Vector3.up, Vector3.forward); Face(Vector3.down, Vector3.back); Face(Vector3.forward, Vector3.up); Face(Vector3.back, Vector3.up); Face(Vector3.right, Vector3.up); Face(Vector3.left, Vector3.up);
            }
            // Tube following a polyline (cables, bent pipes). Smooth normals around, uv.y along the length.
            public void Tube(Matrix4x4 m, Vector3[] path, float radius, int segments, int sub)
            {
                float along = 0;
                for (int p = 0; p < path.Length; p++)
                {
                    var fwd = (p < path.Length - 1 ? path[p + 1] - path[p] : path[p] - path[p - 1]).normalized;
                    if (p > 0 && p < path.Length - 1) fwd = ((path[p + 1] - path[p]).normalized + (path[p] - path[p - 1]).normalized).normalized;
                    var side = Vector3.Cross(Mathf.Abs(fwd.y) > .95f ? Vector3.right : Vector3.up, fwd).normalized; var up = Vector3.Cross(fwd, side);
                    if (p > 0) along += (path[p] - path[p - 1]).magnitude;
                    for (int s = 0; s <= segments; s++) { float a = s / (float)segments * Mathf.PI * 2; var n = side * Mathf.Cos(a) + up * Mathf.Sin(a); Add(m, path[p] + n * radius, n, new Vector2(s / (float)segments, along)); }
                }
                int ring = segments + 1, start = V.Count - ring * path.Length;
                for (int p = 0; p < path.Length - 1; p++) for (int s = 0; s < segments; s++)
                { int i = start + p * ring + s; Tri(sub, i, i + ring, i + ring + 1); Tri(sub, i, i + ring + 1, i + 1); }
            }
            // Front faces: Cross(b - a, c - a) along the vertex normals (same convention as the bunker tiles).
            void Tri(int sub, int a, int b, int c)
            {
                var n = N[a] + N[b] + N[c]; var cr = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (cr.sqrMagnitude < 1e-14f) return;
                if (Vector3.Dot(cr, n) >= 0) Sub[sub].AddRange(new[] { a, b, c }); else Sub[sub].AddRange(new[] { a, c, b });
            }
            void Add(Matrix4x4 m, Vector3 p, Vector3 n, Vector2 uv) { V.Add(m.MultiplyPoint3x4(p)); N.Add(m.MultiplyVector(n).normalized); UV.Add(uv); }
            public Mesh Save(string name)
            {
                Directory.CreateDirectory(Meshes);
                string path = Meshes + "/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool create = !mesh; if (create) mesh = new Mesh(); else mesh.Clear();
                mesh.name = name; mesh.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.subMeshCount = Sub.Count;
                for (int i = 0; i < Sub.Count; i++) mesh.SetTriangles(Sub[i], i);
                mesh.RecalculateTangents(); mesh.RecalculateBounds(); mesh.UploadMeshData(false);
                if (create) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
                return mesh;
            }
        }

        public static Matrix4x4 TRS(Vector3 p, Vector3 euler, Vector3 s) => Matrix4x4.TRS(p, Quaternion.Euler(euler), s);
        public static Matrix4x4 TR(Vector3 p, Vector3 euler) => Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one);

        // Modular environment pieces, reused many times (static batching / instancing).
        public static string BuildPropMeshes()
        {
            // Pipe run: 2 m straight pipe along X with two flanges and wall brackets. sub0 pipe, sub1 brackets.
            var pipe = new MeshKit(2);
            pipe.Lathe(TR(new Vector3(-1, 0, 0), new Vector3(0, 90, 0)), new[] { new Vector2(.09f, 0), new Vector2(.09f, 2) }, 14, 0, 1);
            foreach (float x in new[] { -.96f, .96f }) pipe.Lathe(TR(new Vector3(x - .03f, 0, 0), new Vector3(0, 90, 0)), new[] { new Vector2(.09f, 0), new Vector2(.13f, 0), new Vector2(.13f, .06f), new Vector2(.09f, .06f) }, 14, 0, 1);
            foreach (float x in new[] { -.5f, .5f }) { pipe.Box(TR(new Vector3(x, 0, -.13f), Vector3.zero), new Vector3(.06f, .24f, .12f), 1); pipe.Box(TR(new Vector3(x, 0, -.2f), Vector3.zero), new Vector3(.18f, .28f, .02f), 1); }
            pipe.Save("VO_PipeRun2m");
            // Cable bundle: three sagging cables over 4 m, clipped at both ends.
            var cable = new MeshKit(2);
            for (int c = 0; c < 3; c++)
            {
                var pts = new Vector3[17]; float off = (c - 1) * .07f, sag = .25f + c * .08f;
                for (int i = 0; i < pts.Length; i++) { float t = i / 16f; pts[i] = new Vector3(-2 + t * 4, -sag * 4 * t * (1 - t), off); }
                cable.Tube(Matrix4x4.identity, pts, .022f + c * .004f, 6, 0);
            }
            foreach (float x in new[] { -2f, 2f }) cable.Box(TR(new Vector3(x, 0, 0), Vector3.zero), new Vector3(.08f, .1f, .26f), 1);
            cable.Save("VO_CableBundle4m");
            // Wall vent grate 1.2 x 0.8: frame + 7 slats (sub0) + dark back plate (sub1).
            var vent = new MeshKit(2);
            vent.Box(TR(new Vector3(0, .37f, 0), Vector3.zero), new Vector3(1.2f, .06f, .08f), 0); vent.Box(TR(new Vector3(0, -.37f, 0), Vector3.zero), new Vector3(1.2f, .06f, .08f), 0);
            vent.Box(TR(new Vector3(.57f, 0, 0), Vector3.zero), new Vector3(.06f, .8f, .08f), 0); vent.Box(TR(new Vector3(-.57f, 0, 0), Vector3.zero), new Vector3(.06f, .8f, .08f), 0);
            for (int i = 0; i < 7; i++) vent.Box(TR(new Vector3(0, -.27f + i * .09f, .01f), new Vector3(35, 0, 0)), new Vector3(1.1f, .06f, .012f), 0);
            vent.Box(TR(new Vector3(0, 0, .05f), Vector3.zero), new Vector3(1.14f, .74f, .01f), 1);
            vent.Save("VO_WallVent");
            // Ceiling light panel 2.4 x 0.5: housing (sub0) + emissive diffuser (sub1) facing down.
            var lamp = new MeshKit(2);
            lamp.Box(TR(Vector3.zero, Vector3.zero), new Vector3(2.5f, .12f, .55f), 0);
            lamp.Box(TR(new Vector3(0, -.065f, 0), Vector3.zero), new Vector3(2.3f, .012f, .4f), 1);
            lamp.Save("VO_CeilingLight");
            var housing = new MeshKit(1); housing.Box(TR(Vector3.zero, Vector3.zero), new Vector3(2.5f, .12f, .55f), 0); housing.Save("VO_CeilingLightHousing");
            var lens = new MeshKit(1); lens.Box(TR(new Vector3(0, -.065f, 0), Vector3.zero), new Vector3(2.3f, .012f, .4f), 0); lens.Save("VO_CeilingLightLens");
            // Wall emergency light: back plate + caged lens (sub1 lens).
            var em = new MeshKit(2);
            em.Box(TR(Vector3.zero, Vector3.zero), new Vector3(.3f, .3f, .06f), 0);
            em.Lathe(TR(new Vector3(0, 0, -.03f), new Vector3(180, 0, 0)), new[] { new Vector2(.1f, 0), new Vector2(.1f, .08f), new Vector2(.06f, .13f), new Vector2(0, .14f) }, 12, 1);
            foreach (float a in new[] { 0f, 60f, 120f }) em.Box(TR(new Vector3(0, 0, -.1f), new Vector3(0, 0, a)), new Vector3(.24f, .012f, .012f), 0);
            em.Save("VO_EmergencyLight");
            AssetDatabase.SaveAssets();
            return "prop meshes ok";
        }

        public static string BuildAll() => BuildTextures() + "; " + BuildMaterials() + "; " + BuildPropMeshes();
    }
}
