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
    /// Pase visual 12 · wall decals of Map01 as plain alpha-blended quads 1 cm off the wall (no renderer feature, no pipeline change).
    ///  Place    rebuilds the "VP12_Decals" root of every room of the decal's zone. Deterministic per room.
    ///  Remove   deletes that root from every room.
    /// One material per decal texture; quads cast no shadows.
    public static class VisualOverhaulMap01Decals
    {
        const string Source = "Assets/IronRequiem/Art/Map01/Textures/Source/";
        const string Out = "Assets/IronRequiem/Art/Map01/Textures/Decals/";
        const string Mats = "Assets/IronRequiem/Art/Map01/Materials/Decals/";
        const string Scenes = "Assets/IronRequiem/Scenes/Map01_Bunker/";
        const string RootName = "VP12_Decals";

        // texture, zone, quads per room, base size (m), tint (darkens the painted colour so it sits in the wall)
        static readonly (string name, int tramo, int perRoom, float size, Color tint)[] Wall =
        {
            ("M01_T2_Decal_RustStreak", 2, 6, 1.7f, new Color(.5f, .4f, .36f, .9f)),
        };

        public static string Place() => Run(true);
        public static string Remove() => Run(false);

        static string Run(bool place)
        {
            var log = new StringBuilder();
            var mats = new Dictionary<string, Material>();
            if (place) foreach (var d in Wall) mats[d.name] = MaterialFor(d.name, d.tint, log);
            foreach (var file in Directory.GetFiles(Scenes, "Map01_R*.unity"))
            {
                string path = Scenes + Path.GetFileName(file);
                try { log.AppendLine(Room(path, place, mats)); }
                catch (Exception e) { log.AppendLine("SALTADO " + path + ": " + e.Message); }
            }
            return log.ToString();
        }

        static Material MaterialFor(string name, Color tint, StringBuilder log)
        {
            Directory.CreateDirectory(Out); Directory.CreateDirectory(Mats);
            string tex = Out + name + ".png";
            File.Copy(Source + name + ".png", tex, true); // the source keeps its alpha; nothing is resampled
            AssetDatabase.Refresh();
            var imp = (TextureImporter)AssetImporter.GetAtPath(tex);
            imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaIsTransparency = true; imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.maxTextureSize = 1024; imp.SaveAndReimport();
            string path = Mats + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool fresh = !m;
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (!shader) throw new InvalidOperationException("Sin URP/Lit");
            if (fresh) m = new Material(shader) { name = name };
            m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex)); m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_Smoothness", .05f); m.SetFloat("_Metallic", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ReceiveShadows", 1); m.SetFloat("_SpecularHighlights", 0); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = 3000; m.SetShaderPassEnabled("ShadowCaster", false); m.SetShaderPassEnabled("DepthOnly", false);
            m.enableInstancing = true;
            if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssets();
            log.AppendLine((fresh ? "creado " : "actualizado ") + path);
            return m;
        }

        static int Number(string path)
        {
            var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(path), "_R([0-9]+)_");
            return m.Success ? int.Parse(m.Groups[1].Value) : 0;
        }

        static bool IsWall(string mat) => mat.Contains("_Wall") || mat.Contains("Shaft") || mat.Contains("Tank") || mat.Contains("Colector_Wet") || mat.Contains("Shield");
        static bool IsFloor(string mat) => mat.Contains("Floor") || mat.Contains("Slag");

        static string Room(string path, bool place, Dictionary<string, Material> mats)
        {
            int num = Number(path), tramo = num <= 5 ? 1 : num <= 13 ? 2 : 3;
            Scene opened = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == path) opened = SceneManager.GetSceneAt(i);
            if (opened.IsValid() && opened.isDirty) throw new InvalidOperationException("escena abierta con cambios sin guardar");
            var scene = opened.IsValid() ? opened : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false; int placed = 0;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == RootName) { UnityEngine.Object.DestroyImmediate(root); changed = true; }
                if (place)
                {
                    var walls = new List<MeshRenderer>(); var all = new List<Bounds>(); var wallB = new List<Bounds>(); var floors = new List<Bounds>();
                    foreach (var r0 in scene.GetRootGameObjects())
                        foreach (var r in r0.GetComponentsInChildren<MeshRenderer>(false))
                        {
                            var m = r.sharedMaterial; if (!m) continue;
                            all.Add(r.bounds);
                            if (PrefabUtility.IsPartOfPrefabInstance(r)) continue;
                            if (IsWall(m.name)) { walls.Add(r); wallB.Add(r.bounds); } else if (IsFloor(m.name)) floors.Add(r.bounds);
                        }
                    GameObject holder = null; var rng = new System.Random(1200 + num); var spots = new List<Vector3>();
                    var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                    foreach (var d in Wall)
                    {
                        if (d.tramo != tramo || walls.Count == 0) continue;
                        int made = 0;
                        for (int attempt = 0; attempt < 1500 && made < d.perRoom; attempt++)
                        {
                            var w = walls[rng.Next(walls.Count)]; var t = w.transform; var s = t.lossyScale;
                            bool thinX = s.x < s.z; float thick = thinX ? s.x : s.z, width = thinX ? s.z : s.x;
                            if (thick > 1.5f || s.y < 1.2f || width < .8f) continue;
                            Vector3 n = (thinX ? t.right : t.forward) * (rng.Next(2) == 0 ? 1 : -1), along = thinX ? t.forward : t.right;
                            float size = d.size * (float)(.75 + rng.NextDouble() * .55), half = size * .5f;
                            var p = t.position + n * (thick * .5f) + along * (float)((rng.NextDouble() - .5) * width) + t.up * (float)((rng.NextDouble() - .5) * s.y);
                            // the streak (central third of the texture) must lie on wall; the transparent margin may overhang
                            bool onWall = true;
                            foreach (var c in new[] { new Vector2(-.3f, -.85f), new Vector2(.3f, -.85f), new Vector2(-.3f, .85f), new Vector2(.3f, .85f) })
                                if (!Inside(wallB, p - n * .05f + along * (c.x * half) + t.up * (c.y * half), -1)) { onWall = false; break; }
                            if (!onWall) continue;
                            // room side: free space in front, and a floor under it
                            if (Inside(all, p + n * .3f, -1) || Inside(all, p + n * 1f, -1) || Inside(all, p + n * .3f + t.up * (half * .8f), -1) || Inside(all, p + n * .3f - t.up * (half * .8f), -1)) continue;
                            if (!FloorBelow(floors, p + n * .7f)) continue;
                            bool near = false; foreach (var q in spots) if ((q - p).sqrMagnitude < 9f) { near = true; break; }
                            if (near) continue;
                            if (!holder) holder = new GameObject(RootName);
                            var g = new GameObject(d.name.Replace("M01_", "") + "_" + made);
                            g.transform.SetParent(holder.transform, false);
                            g.transform.SetPositionAndRotation(p + n * .012f, Quaternion.LookRotation(-n, Vector3.up));
                            g.transform.localScale = new Vector3(size * (rng.Next(2) == 0 ? 1 : -1), size, 1); // mirrored half of the time
                            g.AddComponent<MeshFilter>().sharedMesh = quad;
                            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[d.name];
                            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                            spots.Add(p); made++; placed++;
                        }
                    }
                    if (holder) { SceneManager.MoveGameObjectToScene(holder, scene); changed = true; }
                }
                if (changed) { EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("no se pudo guardar"); }
                return (changed ? "GUARDADO " : "sin cambios ") + Path.GetFileName(path) + " · decales " + placed;
            }
            finally { if (!opened.IsValid()) EditorSceneManager.CloseScene(scene, true); }
        }

        static bool Inside(List<Bounds> list, Vector3 p, int skip)
        {
            for (int i = 0; i < list.Count; i++) if (i != skip && list[i].Contains(p)) return true;
            return false;
        }

        static bool FloorBelow(List<Bounds> floors, Vector3 p)
        {
            foreach (var b in floors)
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && b.max.y <= p.y + .1f && b.max.y >= p.y - 8f) return true;
            return false;
        }
    }
}
