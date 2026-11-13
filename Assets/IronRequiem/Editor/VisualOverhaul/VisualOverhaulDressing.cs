using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): scene-agnostic "abandoned bunker" dressing for an OPEN variant scene.
    // Everything is derived from the scene's own NavMesh and colliders with a fixed seed, so it is reproducible:
    // fixtures under the ceiling, emergency lights and props on real walls, decals on real floor/walls, probes per
    // walkable area. Added objects have no colliders (gameplay, NavMesh and AI are unchanged).
    public static class VisualOverhaulDressing
    {
        public sealed class Area { public Bounds Walk; public float FloorY, CeilingY; public List<Vector3> Floor = new List<Vector3>(); public List<(Vector3 p, Vector3 n)> Walls = new List<(Vector3, Vector3)>(); }

        static System.Random rng;
        static float R() => (float)rng.NextDouble();

        // Walkable sample points (area-weighted) from the NavMesh triangulation.
        public static List<Vector3> NavSamples(int count, int seed)
        {
            rng = new System.Random(seed);
            var tri = NavMesh.CalculateTriangulation(); var pts = new List<Vector3>();
            if (tri.indices.Length == 0) return pts;
            var areas = new float[tri.indices.Length / 3]; float total = 0;
            for (int i = 0; i < areas.Length; i++) { var a = tri.vertices[tri.indices[i * 3]]; var b = tri.vertices[tri.indices[i * 3 + 1]]; var c = tri.vertices[tri.indices[i * 3 + 2]]; total += areas[i] = Vector3.Cross(b - a, c - a).magnitude * .5f; }
            for (int k = 0; k < count; k++)
            {
                float pick = R() * total; int t = 0; while (t < areas.Length - 1 && pick > areas[t]) { pick -= areas[t]; t++; }
                var a = tri.vertices[tri.indices[t * 3]]; var b = tri.vertices[tri.indices[t * 3 + 1]]; var c = tri.vertices[tri.indices[t * 3 + 2]];
                float u = R(), v = R(); if (u + v > 1) { u = 1 - u; v = 1 - v; }
                pts.Add(a + (b - a) * u + (c - a) * v);
            }
            return pts;
        }

        public static Bounds NavBounds()
        {
            var tri = NavMesh.CalculateTriangulation(); if (tri.vertices.Length == 0) return new Bounds();
            var b = new Bounds(tri.vertices[0], Vector3.zero); foreach (var v in tri.vertices) b.Encapsulate(v); return b;
        }

        static bool IsWorld(Collider c) => c && !c.isTrigger && !c.GetComponentInParent<Unity.FPS.Game.Health>() && !c.GetComponentInParent<CharacterController>() && !c.attachedRigidbody;

        // Walls: horizontal rays from walkable points at 1.6 m; ceiling: upward rays.
        public static void Survey(List<Vector3> floor, out List<(Vector3 p, Vector3 n)> walls, out float ceilingMedian, out float openSkyFraction)
        {
            walls = new List<(Vector3, Vector3)>(); var ceilings = new List<float>(); int open = 0;
            foreach (var p in floor)
            {
                if (Physics.Raycast(p + Vector3.up * .5f, Vector3.up, out var up, 40, ~0, QueryTriggerInteraction.Ignore) && IsWorld(up.collider)) ceilings.Add(up.point.y - p.y); else open++;
                float a0 = R() * 360;
                for (int k = 0; k < 2; k++)
                {
                    var dir = Quaternion.Euler(0, a0 + k * 180, 0) * Vector3.forward;
                    if (Physics.Raycast(p + Vector3.up * 1.6f, dir, out var hit, 18, ~0, QueryTriggerInteraction.Ignore) && IsWorld(hit.collider) && Mathf.Abs(hit.normal.y) < .2f)
                        walls.Add((hit.point, new Vector3(hit.normal.x, 0, hit.normal.z).normalized));
                }
            }
            ceilings.Sort(); ceilingMedian = ceilings.Count > 0 ? ceilings[ceilings.Count / 2] : 0; openSkyFraction = floor.Count > 0 ? open / (float)floor.Count : 1;
        }

        public static GameObject Piece(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot, bool isStatic = true, bool shadows = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(pos, rot);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveGI = ReceiveGI.LightProbes; r.lightProbeUsage = isStatic ? LightProbeUsage.Off : LightProbeUsage.BlendProbes; if (isStatic) r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        static Light AddLight(Transform parent, Vector3 pos, Color color, float range, float intensity, bool shadows)
        {
            var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(parent, false); l.transform.position = pos;
            l.type = LightType.Point; l.color = color; l.range = range; l.intensity = intensity; l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Mixed; l.bounceIntensity = 1; return l;
        }

        // Mesh decals (08/11/2026, replaces URP DecalProjector + DecalRendererFeature, ~0.4-0.8 ms CPU in the A/B):
        // a static quad laid on the surface with URP Lit transparent (alpha blend, no shadow casting, no depth write).
        // Static batching merges them; no extra renderer feature or pass.
        static Material DecalMat(string name, string tex)
        {
            return MatAsset(name, "Universal Render Pipeline/Lit", m =>
            {
                m.SetTexture("_BaseMap", VisualOverhaulAssets.T(tex)); m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", .18f); m.SetFloat("_EnvironmentReflections", 0); m.SetFloat("_SpecularHighlights", 1);
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_AlphaClip", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 2); m.SetFloat("_ReceiveShadows", 1);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.shaderKeywords = new[] { "_SURFACE_TYPE_TRANSPARENT", "_ENVIRONMENTREFLECTIONS_OFF" };
                m.SetShaderPassEnabled("ShadowCaster", false); m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("DepthNormals", false);
                m.renderQueue = (int)RenderQueue.Transparent - 50; m.enableInstancing = false;
            });
        }

        const float DecalLift = .02f;
        // Lays a quad on the first world surface hit from origin along dir. The four corners must land on the same
        // plane (within 5 cm), otherwise the quad shrinks (x0.7, twice) and is skipped if it still overhangs an edge.
        public static GameObject MeshDecal(Transform parent, string name, Material mat, Vector3 origin, Vector3 dir, Vector3 upHint, Vector2 size, float maxDist)
        {
            if (!Physics.Raycast(origin, dir, out var hit, maxDist, ~0, QueryTriggerInteraction.Ignore) || !IsWorld(hit.collider)) return null;
            var n = hit.normal; var up = Vector3.ProjectOnPlane(upHint, n); if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.forward, n); up.Normalize();
            var right = Vector3.Cross(n, up);
            for (int tries = 0; tries < 3; tries++, size *= .7f)
            {
                bool ok = true;
                for (int c = 0; c < 4 && ok; c++)
                {
                    var corner = hit.point + right * (size.x * .5f * ((c & 1) == 0 ? -1 : 1)) + up * (size.y * .5f * ((c & 2) == 0 ? -1 : 1));
                    ok = Physics.Raycast(corner + n * .25f, -n, out var ch, .35f, ~0, QueryTriggerInteraction.Ignore) && IsWorld(ch.collider) && Vector3.Dot(ch.normal, n) > .95f && Mathf.Abs(Vector3.Dot(ch.point - hit.point, n)) < .05f;
                }
                if (!ok) continue;
                var go = Piece(parent, name, Resources.GetBuiltinResource<Mesh>("Quad.fbx"), new[] { mat }, hit.point + n * DecalLift, Quaternion.LookRotation(-n, up));
                go.transform.localScale = new Vector3(size.x, size.y, 1); var r = go.GetComponent<MeshRenderer>(); r.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                return go;
            }
            return null;
        }

        // Converts the DecalProjectors of the open variant scene into mesh decals (same spots, sizes and materials).
        public static string ConvertProjectorsToMesh()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.path.StartsWith(VisualOverhaulScenes.Folder)) throw new Exception("Only variant scenes");
            var mats = new Dictionary<string, Material> { { "VO_Decal_Stain", DecalMat("VO_Decal_Stain", "VO_Decal_Stain") }, { "VO_Decal_Burn", DecalMat("VO_Decal_Burn", "VO_Decal_Burn") }, { "VO_Decal_Rust", DecalMat("VO_Decal_Rust", "VO_Decal_Rust") }, { "VO_Decal_Hazard", DecalMat("VO_Decal_Hazard", "VO_Decal_Hazard") } };
            var projectors = Object.FindObjectsByType<DecalProjector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int made = 0, skipped = 0;
            foreach (var d in projectors)
            {
                var t = d.transform; var mat = d.material && mats.TryGetValue(d.material.name, out var mm) ? mm : mats["VO_Decal_Stain"];
                var go = MeshDecal(t.parent, t.name, mat, t.position, t.forward, t.up, new Vector2(d.size.x, d.size.y), d.size.z + 1.2f);
                if (go) { go.transform.SetSiblingIndex(t.GetSiblingIndex()); made++; } else skipped++;
                Object.DestroyImmediate(d.gameObject);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            return $"projectors {projectors.Length} -> mesh decals {made} (skipped {skipped}: no flat surface under the quad)";
        }
        static Material MatAsset(string name, string shader, Action<Material> setup)
        {
            string path = VisualOverhaulAssets.Mats + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var s = Shader.Find(shader); if (!s) throw new Exception("Shader missing: " + shader);
            if (!m) { m = new Material(s); AssetDatabase.CreateAsset(m, path); }
            m.shader = s; setup(m); EditorUtility.SetDirty(m); return m;
        }

        public sealed class Options
        {
            public float LightSpacing = 12f, FixtureDrop = .35f; public int Seed = 20261107;
            public float CeilingY = float.NaN; // world height of fixtures; NaN = surveyed
            public int Decals = 60, Pipes = 18, Cables = 10, Vents = 8, Emergency = 8, Flicker = 3, SteamVents = 3;
            public Color Fog = new Color(.055f, .06f, .07f); public float FogDensity = .009f;
            public bool AddCeiling; public float CeilingThickness = .5f;
            public float ReflectionCell = 24f; public bool Fixtures = true; public float MaxFixtureHeightAboveFloor = 5.5f, FixtureRange = 12f, FixtureIntensity = 8f;
        }

        // Adds the dressing to the active scene. Returns a report.
        public static string Dress(Options o, out List<Light> lights)
        {
            var log = new StringBuilder(); lights = new List<Light>();
            var floor = NavSamples(900, o.Seed); var nav = NavBounds();
            Survey(floor, out var walls, out float ceilMedian, out float openSky);
            float floorY = floor.Count > 0 ? floor.Select(p => p.y).OrderBy(y => y).ElementAt(floor.Count / 10) : nav.min.y;
            float ceilingY = !float.IsNaN(o.CeilingY) ? o.CeilingY : floorY + (ceilMedian > 2.5f ? Mathf.Min(ceilMedian, 9f) : 7f);
            log.AppendLine($"nav bounds {nav.center:F1}/{nav.size:F1}; floor samples {floor.Count}; wall hits {walls.Count}; ceiling median {ceilMedian:F2} m; open sky {openSky:P0}; fixtures at y={ceilingY - o.FixtureDrop:F2}");
            var root = new GameObject("VO_Dressing").transform;
            // Dressing is presentation only: excluded from NavMesh builds that collect render meshes.
            var mod = root.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>(); mod.ignoreFromBuild = true;
            var lightsRoot = new GameObject("VO_Lights").transform; lightsRoot.SetParent(root, false);
            var props = new GameObject("VO_Props").transform; props.SetParent(root, false);
            var decals = new GameObject("VO_Decals").transform; decals.SetParent(root, false);
            var fx = new GameObject("VO_Ambience").transform; fx.SetParent(root, false);

            Mesh M(string n) => AssetDatabase.LoadAssetAtPath<Mesh>(VisualOverhaulAssets.Meshes + "/" + n + ".asset");
            Material L(string n) => VisualOverhaulAssets.Load(n);

            // Optional closed ceiling (render only, no collider, no shadows) over the walkable bounds.
            if (o.AddCeiling)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.name = "VO_Ceiling"; Object.DestroyImmediate(c.GetComponent<Collider>()); c.transform.SetParent(root, false);
                c.transform.position = new Vector3(nav.center.x, ceilingY + o.CeilingThickness * .5f, nav.center.z); c.transform.localScale = new Vector3(nav.size.x + 30, o.CeilingThickness, nav.size.z + 30);
                var cr = c.GetComponent<MeshRenderer>(); cr.sharedMaterial = L("VO_Env_WallTri"); cr.shadowCastingMode = ShadowCastingMode.Off; cr.receiveGI = ReceiveGI.LightProbes; GameObjectUtility.SetStaticEditorFlags(c, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                log.AppendLine("ceiling added (render only) at " + ceilingY.ToString("F2"));
            }

            // Cold ceiling fixtures on a grid, only where the floor below is walkable.
            var fixtureMats = new[] { L("VO_Prop_Housing"), L("VO_Light_Cold") }; var deadMats = new[] { L("VO_Prop_Housing"), L("VO_Light_Dead") };
            var fixtures = new List<(GameObject go, Light l)>();
            if (o.Fixtures) for (float x = nav.min.x + o.LightSpacing * .5f; x < nav.max.x; x += o.LightSpacing)
                for (float z = nav.min.z + o.LightSpacing * .5f; z < nav.max.z; z += o.LightSpacing)
                {
                    var near = floor.Where(p => Mathf.Abs(p.x - x) < o.LightSpacing * .4f && Mathf.Abs(p.z - z) < o.LightSpacing * .4f).ToList(); if (near.Count < 3) continue;
                    var c = near[near.Count / 2]; float y = Mathf.Min(ceilingY - o.FixtureDrop, c.y + o.MaxFixtureHeightAboveFloor);
                    if (Physics.Raycast(c + Vector3.up * .5f, Vector3.up, out var hit, 40, ~0, QueryTriggerInteraction.Ignore) && IsWorld(hit.collider)) y = Mathf.Min(y, hit.point.y - .15f);
                    if (y - c.y < 2.6f) continue; // too low (stairs under a slab): skip
                    var go = Piece(props, "VO_CeilingLight", M("VO_CeilingLight"), fixtureMats, new Vector3(c.x, y, c.z), Quaternion.Euler(0, (fixtures.Count % 2) * 90, 0));
                    var l = AddLight(go.transform, new Vector3(c.x, y - .3f, c.z), new Color(.75f, .88f, 1f), Mathf.Min(o.LightSpacing * 1.45f, o.FixtureRange), o.FixtureIntensity, false); lights.Add(l); fixtures.Add((go, l));
                }
            // Two shadowed key lights: the fixtures closest to the walkable centre of mass.
            var centre = floor.Aggregate(Vector3.zero, (a, b) => a + b) / Mathf.Max(1, floor.Count);
            foreach (var f in fixtures.OrderBy(f => (f.go.transform.position - centre).sqrMagnitude).Take(2)) { f.l.shadows = LightShadows.Soft; f.l.intensity *= 1.2f; }
            // A few failing tubes (cheap flicker, no GC); never the shadowed ones.
            rng = new System.Random(o.Seed + 1);
            foreach (var f in fixtures.Where(f => f.l.shadows == LightShadows.None).OrderBy(_ => R()).Take(o.Flicker))
            { var fl = f.go.AddComponent<LightFlicker>(); fl.Configure(f.l, f.go.GetComponent<MeshRenderer>(), null, null, (int)(f.go.transform.position.x * 13 + f.go.transform.position.z * 7) & 1023); f.l.lightmapBakeType = LightmapBakeType.Realtime; SetFlickerMats(fl, fixtureMats, deadMats); }
            log.AppendLine($"ceiling fixtures {fixtures.Count} (2 shadowed, {Math.Min(o.Flicker, fixtures.Count)} flickering)");

            // Wall pieces: emergency lights (red/amber), pipes, vents, rust streaks, hazard signs.
            rng = new System.Random(o.Seed + 2);
            var wallPool = walls.OrderBy(_ => R()).ToList(); int wi = 0;
            (Vector3 p, Vector3 n)? NextWall(float minGap, List<Vector3> used)
            {
                for (; wi < wallPool.Count; wi++) { var w = wallPool[wi]; if (used.All(u => (u - w.p).sqrMagnitude > minGap * minGap)) { wi++; return w; } }
                return null;
            }
            var usedEm = new List<Vector3>();
            for (int i = 0; i < o.Emergency; i++)
            {
                var w = NextWall(10, usedEm); if (w == null) break; usedEm.Add(w.Value.p);
                bool red = i % 2 == 0; float h = Mathf.Min(ceilingY - 1.2f, w.Value.p.y + 2.2f);
                var pos = new Vector3(w.Value.p.x, h, w.Value.p.z) + w.Value.n * .03f; var rot = Quaternion.LookRotation(-w.Value.n);
                var go = Piece(props, red ? "VO_EmergencyRed" : "VO_EmergencyAmber", M("VO_EmergencyLight"), new[] { L("VO_Prop_Housing"), L(red ? "VO_Light_Red" : "VO_Light_Amber") }, pos, rot);
                lights.Add(AddLight(go.transform, pos + w.Value.n * .4f, red ? new Color(1f, .14f, .07f) : new Color(1f, .55f, .14f), 7.5f, red ? 4.5f : 4f, false));
            }
            var usedPipe = new List<Vector3>();
            for (int i = 0; i < o.Pipes; i++)
            {
                var w = NextWall(5, usedPipe); if (w == null) break; usedPipe.Add(w.Value.p);
                float h = Mathf.Min(ceilingY - .6f, w.Value.p.y + 3.6f + (i % 3) * .35f); var along = Vector3.Cross(Vector3.up, w.Value.n);
                var rot = Quaternion.LookRotation(-w.Value.n) ; var pos = new Vector3(w.Value.p.x, h, w.Value.p.z) + w.Value.n * .2f;
                for (int s = -1; s <= 1; s++) Piece(props, "VO_Pipe", M("VO_PipeRun2m"), new[] { L("VO_Prop_Pipe"), L("VO_Prop_Housing") }, pos + along * s * 2f, rot * Quaternion.Euler(0, 180, 0));
            }
            var usedVent = new List<Vector3>(); var ventSpots = new List<(Vector3, Vector3)>();
            for (int i = 0; i < o.Vents; i++)
            {
                var w = NextWall(8, usedVent); if (w == null) break; usedVent.Add(w.Value.p);
                var pos = new Vector3(w.Value.p.x, w.Value.p.y + .3f + (i % 2) * 1.6f, w.Value.p.z) + w.Value.n * .04f;
                Piece(props, "VO_Vent", M("VO_WallVent"), new[] { L("VO_Prop_Housing"), L("VO_Prop_Cable") }, pos, Quaternion.LookRotation(-w.Value.n)); ventSpots.Add((pos, w.Value.n));
            }
            // Cables sag under the ceiling between fixtures.
            rng = new System.Random(o.Seed + 3);
            foreach (var f in fixtures.OrderBy(_ => R()).Take(o.Cables))
                Piece(props, "VO_Cables", M("VO_CableBundle4m"), new[] { L("VO_Prop_Cable"), L("VO_Prop_Housing") }, f.go.transform.position + new Vector3(2.6f, .05f, 1.2f), Quaternion.Euler(0, R() * 180, 0));
            log.AppendLine($"emergency {usedEm.Count}, pipe runs {usedPipe.Count}x3, vents {usedVent.Count}, cable bundles {Math.Min(o.Cables, fixtures.Count)}");

            // Mesh decals (static quads, Lit transparent): floor stains/burns, wall rust and hazard signs.
            var stain = DecalMat("VO_Decal_Stain", "VO_Decal_Stain"); var burn = DecalMat("VO_Decal_Burn", "VO_Decal_Burn"); var rust = DecalMat("VO_Decal_Rust", "VO_Decal_Rust"); var hazard = DecalMat("VO_Decal_Hazard", "VO_Decal_Hazard");
            rng = new System.Random(o.Seed + 4); int dc = 0, tried = 0;
            foreach (var p in floor.OrderBy(_ => R()).Take(o.Decals * 2 / 3))
            {
                var q = Quaternion.Euler(90, R() * 360, 0); float s = 1.2f + R() * 2.4f; tried++;
                if (MeshDecal(decals, "VO_Decal", dc % 3 == 2 ? burn : stain, p + Vector3.up * .4f, q * Vector3.forward, q * Vector3.up, new Vector2(s, s), 1.6f)) dc++;
            }
            int signs = 0;
            foreach (var w in walls.OrderBy(_ => R()).Take(o.Decals / 3))
            {
                bool sign = signs < 8 && dc % 4 == 0; float h = sign ? 1.7f : 2.2f + R() * 1.5f; tried++;
                var size = sign ? new Vector2(.9f, .9f) : new Vector2(.8f + R(), 2.5f + R() * 2f);
                if (MeshDecal(decals, sign ? "VO_HazardSign" : "VO_RustStreak", sign ? hazard : rust, new Vector3(w.p.x, w.p.y - 1.6f + h, w.p.z) + w.n * .3f, -w.n, Vector3.up, size, 1.2f)) { if (sign) signs++; dc++; }
            }
            log.AppendLine($"mesh decals {dc}/{tried} ({signs} hazard signs)");

            // Ambience: one dust emitter that follows the camera (few particles, no collision) + steam at some vents.
            var dustMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("2d3fb4655ab03304eb074bfde7c94c42"));
            var dust = new GameObject("VO_Dust").AddComponent<ParticleSystem>(); dust.transform.SetParent(fx, false); dust.gameObject.AddComponent<FollowMainCamera>();
            { var main = dust.main; main.loop = true; main.startLifetime = 9; main.startSpeed = .05f; main.startSize = new ParticleSystem.MinMaxCurve(.015f, .04f); main.maxParticles = 70; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(.8f, .85f, .9f, .22f);
              var em = dust.emission; em.rateOverTime = 8; var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(16, 6, 16);
              var noise = dust.noise; noise.enabled = true; noise.strength = .08f; noise.frequency = .2f; var col = dust.collision; col.enabled = false;
              var pr = dust.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = dustMat; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false; }
            var steamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_AngrySteam.prefab");
            foreach (var v in ventSpots.Take(o.SteamVents))
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(steamPrefab, fx); s.name = "VO_VentSteam"; s.transform.SetPositionAndRotation(v.Item1 + v.Item2 * .1f, Quaternion.LookRotation(v.Item2 + Vector3.up * .6f)); s.transform.localScale = Vector3.one * .6f;
                foreach (var ps in s.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.loop = true; main.playOnAwake = true; main.maxParticles = Mathf.Min(main.maxParticles, 16); var col = ps.collision; col.enabled = false; }
            }

            // Fog and ambient (dark, cool).
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = o.FogDensity; RenderSettings.fogColor = o.Fog;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.27f, .29f, .33f); RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.reflectionIntensity = .6f;

            // Light probes: grid over walkable samples at 0.6 / 2.2 / 4 m (enemies and props sample these).
            var probePts = new List<Vector3>();
            for (float x = nav.min.x; x <= nav.max.x; x += 4) for (float z = nav.min.z; z <= nav.max.z; z += 4)
            {
                var near = floor.Where(p => Mathf.Abs(p.x - x) < 2 && Mathf.Abs(p.z - z) < 2).ToList(); if (near.Count == 0) continue;
                foreach (var lvl in near.Select(p => Mathf.Round(p.y * 2) / 2).Distinct()) foreach (float h in new[] { .6f, 2.2f, 4f }) probePts.Add(new Vector3(x, lvl + h, z));
            }
            var lpg = new GameObject("VO_LightProbes").AddComponent<LightProbeGroup>(); lpg.transform.SetParent(root, false); lpg.probePositions = probePts.ToArray();
            // Reflection probes: one per ~24 m cell with walkable floor, box projected, baked.
            int rp = 0;
            float rc = o.ReflectionCell; for (float x = nav.min.x; x < nav.max.x; x += rc) for (float z = nav.min.z; z < nav.max.z; z += rc)
            {
                var cell = floor.Where(p => p.x >= x && p.x < x + rc && p.z >= z && p.z < z + rc).ToList(); if (cell.Count < 12) continue;
                var cb = new Bounds(cell[0], Vector3.zero); foreach (var p in cell) cb.Encapsulate(p);
                var probe = new GameObject("VO_ReflectionProbe").AddComponent<ReflectionProbe>(); probe.transform.SetParent(root, false);
                float h = Mathf.Max(4, ceilingY - cb.min.y); probe.transform.position = new Vector3(cb.center.x, cb.min.y + Mathf.Min(2.2f, h * .5f), cb.center.z);
                probe.mode = ReflectionProbeMode.Baked; probe.boxProjection = true; probe.size = new Vector3(cb.size.x + 6, h + 1, cb.size.z + 6); probe.center = new Vector3(0, h * .5f - Mathf.Min(2.2f, h * .5f), 0);
                probe.resolution = 128; probe.hdr = true; probe.importance = 1; probe.blendDistance = 2; rp++;
            }
            log.AppendLine($"light probes {probePts.Count}, reflection probes {rp}, fog exp2 {o.FogDensity}");
            return log.ToString();
        }

        // LightFlicker swaps one sharedMaterial: the flickering fixture uses a housing-only mesh plus a lens-only child.
        static void SetFlickerMats(LightFlicker f, Material[] lit, Material[] dead)
        {
            Mesh M(string n) => AssetDatabase.LoadAssetAtPath<Mesh>(VisualOverhaulAssets.Meshes + "/" + n + ".asset");
            f.GetComponent<MeshFilter>().sharedMesh = M("VO_CeilingLightHousing"); f.GetComponent<MeshRenderer>().sharedMaterials = new[] { lit[0] };
            var lens = Piece(f.transform, "Lens", M("VO_CeilingLightLens"), new[] { lit[1] }, f.transform.position, f.transform.rotation, false);
            var so = new SerializedObject(f); so.FindProperty("fixture").objectReferenceValue = lens.GetComponent<MeshRenderer>(); so.FindProperty("litMaterial").objectReferenceValue = lit[1]; so.FindProperty("deadMaterial").objectReferenceValue = dead[1]; so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Counts additional lights overlapping each renderer (URP Forward keeps the 4 most important per object).
        public static string CheckLightBudget(IEnumerable<Renderer> renderers, IEnumerable<Light> lights, int limit, out List<Renderer> over)
        {
            var pts = lights.Where(l => l && l.type != LightType.Directional && l.enabled).ToList(); over = new List<Renderer>(); int max = 0;
            foreach (var r in renderers)
            {
                if (!r || !r.enabled) continue; var b = r.bounds; int n = 0;
                foreach (var l in pts) { float d = Mathf.Sqrt(b.SqrDistance(l.transform.position)); if (d < l.range) n++; }
                max = Math.Max(max, n); if (n > limit) over.Add(r);
            }
            return $"max additional lights on one renderer {max}; renderers over {limit}: {over.Count}";
        }

        // Splits a static MeshRenderer into tiles (triangles bucketed by centroid on a world XZ grid). The original
        // renderer is disabled; its collider and GameObject stay (physics/NavMesh unchanged).
        public static int SplitRenderer(MeshRenderer r, float cell, string meshFolder)
        {
            var mf = r.GetComponent<MeshFilter>(); var src = mf ? mf.sharedMesh : null; if (!src || !src.isReadable) return 0;
            var v = src.vertices; var n = src.normals; var uv = src.uv; var tan = src.tangents; var l2w = r.transform.localToWorldMatrix;
            var buckets = new Dictionary<(int, int, int), List<int>>();
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var tri = src.GetTriangles(s);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var c = l2w.MultiplyPoint3x4((v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f);
                    var key = (Mathf.FloorToInt(c.x / cell), Mathf.FloorToInt(c.z / cell), s);
                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<int>(); list.AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
                }
            }
            int tiles = 0;
            foreach (var cellKey in buckets.Keys.Select(k => (k.Item1, k.Item2)).Distinct())
            {
                var mesh = new Mesh { name = src.name + "_tile_" + cellKey.Item1 + "_" + cellKey.Item2 }; var map = new Dictionary<int, int>();
                var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<Vector4>(); var subs = new List<int[]>(); var mats = new List<Material>();
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    if (!buckets.TryGetValue((cellKey.Item1, cellKey.Item2, s), out var idx)) continue;
                    var outIdx = new int[idx.Count];
                    for (int i = 0; i < idx.Count; i++) { int o = idx[i]; if (!map.TryGetValue(o, out int m)) { m = nv.Count; map[o] = m; nv.Add(v[o]); if (n.Length > 0) nn.Add(n[o]); if (uv.Length > 0) nuv.Add(uv[o]); if (tan.Length > 0) nt.Add(tan[o]); } outIdx[i] = m; }
                    subs.Add(outIdx); mats.Add(r.sharedMaterials[Mathf.Min(s, r.sharedMaterials.Length - 1)]);
                }
                mesh.SetVertices(nv); if (nn.Count == nv.Count) mesh.SetNormals(nn); if (nuv.Count == nv.Count) mesh.SetUVs(0, nuv); if (nt.Count == nv.Count) mesh.SetTangents(nt);
                mesh.subMeshCount = subs.Count; for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s); mesh.RecalculateBounds();
                System.IO.Directory.CreateDirectory(meshFolder); AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(meshFolder + "/" + mesh.name + ".asset"));
                var go = new GameObject(r.name + "_Tile", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(r.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh; var tr = go.GetComponent<MeshRenderer>(); tr.sharedMaterials = mats.ToArray(); tr.shadowCastingMode = r.shadowCastingMode; tr.receiveGI = r.receiveGI; tr.lightProbeUsage = r.lightProbeUsage;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(r.gameObject)); go.layer = r.gameObject.layer; tiles++;
            }
            if (tiles > 0) r.enabled = false;
            return tiles;
        }
    }

}
