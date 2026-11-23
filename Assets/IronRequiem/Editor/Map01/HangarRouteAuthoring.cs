using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor.Map01
{
    // ---------------- data (Assets/IronRequiem/Levels/Map01/Data/Hangar/route.json, WORLD coordinates) ----------------
    public sealed class HangarRouteRules
    {
        public float[] capsuleCenter, laneStart, laneGoal, doorProbe, routeZone;
        public float capsuleKeepOut = 6f, scrapPointKeepOut = 1.5f, scrapPointKeepOutDecor = 1f, laneMinWidth = 4f;
        public int maxNewLights = 4, maxShadowLights = 1;
        public float snagGapMin = .3f, snagGapMax = 1.3f;
    }
    public sealed class HangarRouteShot { public string name; public bool pod; public float face, right, up; public float? belt; public float halfAngle = 20f; }
    public sealed class HangarRouteItem
    {
        public string id, type, material, material2, source, mode, text;
        public float[] pos, euler, size, lean, target, color;
        public bool collider, shadows, bench, flicker;
        public int seed, count, levels;
        public float radius, height, length, scale, intensity, range, angle, spacing, rate;
    }
    public sealed class HangarRouteData { public int version; public string notes; public HangarRouteRules rules; public List<HangarRouteShot> shots; public List<HangarRouteItem> items; }

    /// Map01 · Hangar (18/11/2026). Rediseño del tramo de Room00_Hangar entre el punto de pie tras la cinemática y la puerta
    /// «TRÁNSITO C-04 · PUENTE NORTE» (FrontWall, mundo z -85, hueco x ±4): una chatarrería de los Carroñeros.
    /// Todo lo nuevo cuelga de la raíz HG_ScrapRoute (mundo, yaw 0). Idempotente: restaura lo que desactivó la vez anterior,
    /// borra HG_ScrapRoute, lo reconstruye desde route.json, valida (radio de la cápsula, ScrapPoints, conos de los Shot_*,
    /// pasillo ≥ 4 m, huecos que enganchan), rehornea el NavMesh existente del hangar con los ajustes del horneado original
    /// y comprueba rutas. Si algo falla deshace todo (Undo) y no guarda. Nunca toca la cinemática ni los scripts HangarIntro.
    public static class HangarRouteAuthoring
    {
        const string Menu = Map01Paths.MenuRoot + "Hangar/";
        public const string ScenePath = "Assets/IronRequiem/Scenes/Campaign/Room00_Hangar.unity";
        public const string DataPath = Map01Paths.DataRoot + "/Hangar/route.json";
        public const string GeneratedFolder = Map01Paths.GeneratedRoot + "/Hangar";
        public const string ReportFolder = "Tools/IronRequiem/Reports/Map01/Hangar";
        public const string RootName = "HG_ScrapRoute";
        const string DisabledRecord = "_DisabledByRoute";
        const string NavPath = "Assets/IronRequiem/Art/Levels/Campaign/Room00_Hangar_NavMesh.asset";
        static readonly string[] MaterialFolders = { "Assets/IronRequiem/Art/Menu/Materials", "Assets/IronRequiem/Art/Environment/Bunker", "Assets/IronRequiem/Art/VisualOverhaul/Materials" };
        static readonly string[] ScrapPrefabs = { "Assets/IronRequiem/Prefabs/Scrap/Scrap_RustedWreckage.prefab", "Assets/IronRequiem/Prefabs/Scrap/Scrap_MachineWreck.prefab", "Assets/IronRequiem/Prefabs/Scrap/Scrap_MilitaryMech.prefab" };
        static readonly Dictionary<string, string> WreckPrefabs = new Dictionary<string, string>
        {
            { "T07", "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab" },
            { "BigBot", "Assets/IronRequiem/Prefabs/Campaign/BigBot_Current.prefab" },
            { "SawBot", "Assets/IronRequiem/Prefabs/Campaign/SawBot_Current.prefab" },
        };
        const string CableMeshPath = "Assets/IronRequiem/Art/VisualOverhaul/Meshes/VO_CableBundle4m.asset";
        const string EmergencyMeshPath = "Assets/IronRequiem/Art/VisualOverhaul/Meshes/VO_EmergencyLight.asset";
        // Same agent/voxel/bounds as the original hangar bake (Editor/HangarIntro/HangarSectorAuthoring.Bake, read only).
        const float NavRadius = .6f, NavHeight = 1.6f, NavClimb = .3f, NavSlope = 35f, NavVoxel = .1f;
        static readonly Bounds NavLocalBounds = new Bounds(new Vector3(0, 1.5f, 3), new Vector3(34, 5, 56));
        static readonly string[] ProtectedUnderSector = { "Capsule_C04", "PodBase", "PodRim", "PodStrut", "OverheadLamp", "CctvFeed", "HG_PostVolume", "Bridge", "ConveyorPit", "City", "CinematicLookTarget", "CinematicBeltTarget" };
        static readonly string[] Types = { "container", "rack", "bin", "crates", "cart", "cover", "hopper", "wreck", "pile", "loose", "crane", "hang", "sign", "decal", "marker", "emergency", "light", "fx", "litter" };
        static readonly string[] SkipNameKeys = { "glow", "flash", "muzzle", "laser", "beam", "trail", "vfx", "shield", "charge", "eye", "visor", "lens", "projectile", "indicator", "light", "spark", "smoke", "hitbox", "debug", "tracer" };

        static Mesh cubeMesh, cylMesh, quadMesh;

        sealed class Ctx
        {
            public HangarRouteData data; public HangarRouteRules rules; public Scene scene;
            public Transform root, sector, shots, stand, pod, disabled;
            public readonly StringBuilder log = new StringBuilder();
            public readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
            public GameObject[] scrap; public Mesh cable, emergency;
            public readonly List<ShotCone> cones = new List<ShotCone>();
            public readonly List<Vector3> points = new List<Vector3>();
            public Vector2 capsule;
            public readonly List<Collider> newColliders = new List<Collider>();
            public readonly List<string> errors = new List<string>(), warnings = new List<string>(), disabledPaths = new List<string>();
            public readonly Dictionary<string, Mesh> wrecks = new Dictionary<string, Mesh>();
            public readonly Dictionary<string, Material[]> wreckMats = new Dictionary<string, Material[]>();
            public float beamBottom = float.NaN;
            public float laneWidth;
        }

        struct ShotCone { public string name; public Vector3 origin, dir; public float length, tan; }

        // =====================================================================================================================
        [MenuItem(Menu + "Build scrap route", priority = 10)]
        public static void BuildMenu()
        {
            try { var r = Build(); Debug.Log("[Map01 Hangar] Build scrap route OK -> " + r); }
            catch (Exception e)
            {
                Debug.LogError("[Map01 Hangar] Build scrap route falló (no se guardó nada): " + e);
                try { WriteReport("route_build_error.txt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + e); } catch (Exception) { }
            }
        }

        /// Returns the report path. Throws (after reverting every scene change) when a rule fails.
        public static string Build()
        {
            var c = new Ctx();
            c.log.AppendLine("Map01 Hangar · Build scrap route · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            c.data = LoadData(); c.rules = c.data.rules;
            c.scene = OpenHangar(c.log);
            Backup(c);
            FindScene(c);
            LoadAssets(c);
            string protectedBefore = ProtectedHash(c);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build HG_ScrapRoute");
            try
            {
                RestoreAndClear(c);
                var rootGo = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(rootGo, "HG_ScrapRoute");
                SceneManager.MoveGameObjectToScene(rootGo, c.scene);
                c.root = rootGo.transform; c.root.SetPositionAndRotation(Vector3.zero, Quaternion.identity); c.root.localScale = Vector3.one;
                c.disabled = new GameObject(DisabledRecord).transform; c.disabled.SetParent(c.root, false);
                BuildCones(c);
                var crane = c.data.items.FirstOrDefault(i => i.type == "crane");
                if (crane != null) c.beamBottom = crane.pos[1] - .45f;
                // geometry first, then the self-avoiding scatter (markers, litter) once the colliders exist
                foreach (var it in c.data.items.Where(i => i.type != "marker" && i.type != "litter")) BuildItem(c, it);
                Physics.SyncTransforms();
                foreach (var it in c.data.items.Where(i => i.type == "marker" || i.type == "litter")) BuildItem(c, it);
                SetStatic(c.root);
                Physics.SyncTransforms();
                ValidatePlacement(c);
                DisableOverlapped(c);
                Physics.SyncTransforms();
                LaneCheck(c);
                SnagCheck(c);
                if (c.errors.Count > 0) throw new InvalidOperationException("Validación de HG_ScrapRoute:\n - " + string.Join("\n - ", c.errors));
                string protectedAfter = ProtectedHash(c);
                c.log.AppendLine("cinemática protegida: hash antes " + protectedBefore + " · después " + protectedAfter);
                if (protectedAfter != protectedBefore) throw new InvalidOperationException("Un objeto protegido de la cinemática cambió durante la autoría (hash " + protectedBefore + " -> " + protectedAfter + ").");
                BakeAndCheckNav(c);
                if (c.errors.Count > 0) throw new InvalidOperationException("NavMesh tras la ruta:\n - " + string.Join("\n - ", c.errors));
                Stats(c);
                EditorSceneManager.MarkSceneDirty(c.scene);
                if (!EditorSceneManager.SaveScene(c.scene)) throw new InvalidOperationException("No se pudo guardar " + ScenePath);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                c.log.AppendLine("guardado " + ScenePath);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                c.log.AppendLine("REVERTIDO (Undo) · errores: " + c.errors.Count);
                foreach (var e in c.errors) c.log.AppendLine("  ERROR " + e);
                try { WriteReport("route_build.txt", c.log.ToString()); } catch (Exception) { }
                throw;
            }
            if (c.warnings.Count > 0) { c.log.AppendLine("AVISOS (" + c.warnings.Count + "):"); foreach (var w in c.warnings) c.log.AppendLine("  " + w); }
            return WriteReport("route_build.txt", c.log.ToString());
        }

        // ------------------------------------------------------------------ data
        static HangarRouteData LoadData()
        {
            var d = Map01Json.Load<HangarRouteData>(DataPath);
            if (d == null) throw new InvalidDataException(DataPath + " vacío");
            if (d.rules == null) throw new InvalidDataException(DataPath + ": falta 'rules'");
            var r = d.rules;
            Need(r.capsuleCenter, 2, "rules", "capsuleCenter"); Need(r.laneStart, 2, "rules", "laneStart"); Need(r.laneGoal, 2, "rules", "laneGoal");
            Need(r.doorProbe, 3, "rules", "doorProbe"); Need(r.routeZone, 4, "rules", "routeZone");
            if (r.capsuleKeepOut < 6f) throw new InvalidDataException("rules.capsuleKeepOut debe ser ≥ 6 m (regla de la cinemática)");
            if (d.shots == null || d.shots.Count == 0) throw new InvalidDataException(DataPath + ": falta 'shots'");
            if (d.items == null || d.items.Count == 0) throw new InvalidDataException(DataPath + ": falta 'items'");
            var ids = new HashSet<string>();
            int lights = 0, shadows = 0;
            foreach (var it in d.items)
            {
                if (string.IsNullOrEmpty(it.id)) throw new InvalidDataException("route.json: item sin 'id'");
                if (!ids.Add(it.id)) throw new InvalidDataException("route.json: id repetido '" + it.id + "'");
                if (string.IsNullOrEmpty(it.type) || !Types.Contains(it.type)) throw new InvalidDataException("route.json item '" + it.id + "': 'type' desconocido '" + it.type + "'");
                Need(it.pos, 3, it.id, "pos");
                switch (it.type)
                {
                    case "container": case "rack": case "bin": case "crates": case "cart": case "cover": case "hopper": Need(it.size, 3, it.id, "size"); break;
                    case "wreck":
                        if (string.IsNullOrEmpty(it.source) || !WreckPrefabs.ContainsKey(it.source)) throw new InvalidDataException("route.json item '" + it.id + "': 'source' debe ser T07, BigBot o SawBot");
                        Positive(it.length, it.id, "length"); break;
                    case "pile": Positive(it.radius, it.id, "radius"); Positive(it.height, it.id, "height"); break;
                    case "loose": Positive(it.scale, it.id, "scale"); break;
                    case "crane": Positive(it.length, it.id, "length"); Need(it.target, 2, it.id, "target"); break;
                    case "hang": Positive(it.length, it.id, "length"); if (it.mode != "cable" && it.mode != "chain" && it.mode != "hook") throw new InvalidDataException("route.json item '" + it.id + "': 'mode' debe ser cable, chain o hook"); break;
                    case "sign": Need(it.size, 2, it.id, "size"); if (string.IsNullOrEmpty(it.text)) throw new InvalidDataException("route.json item '" + it.id + "': falta 'text'"); break;
                    case "decal": Need(it.size, 2, it.id, "size"); NeedText(it.material, it.id, "material"); break;
                    case "marker": Need(it.target, 2, it.id, "target"); Need(it.size, 3, it.id, "size"); Positive(it.radius, it.id, "radius"); Positive(it.spacing, it.id, "spacing"); NeedText(it.material, it.id, "material"); break;
                    case "light":
                        if (it.mode != "Point" && it.mode != "Spot") throw new InvalidDataException("route.json item '" + it.id + "': 'mode' debe ser Point o Spot");
                        Need(it.color, 3, it.id, "color"); Positive(it.intensity, it.id, "intensity"); Positive(it.range, it.id, "range");
                        if (it.mode == "Spot") Positive(it.angle, it.id, "angle");
                        lights++; if (it.shadows) shadows++; break;
                    case "fx": if (it.mode != "sparks" && it.mode != "smoke") throw new InvalidDataException("route.json item '" + it.id + "': 'mode' debe ser sparks o smoke"); break;
                    case "litter": Need(it.size, 3, it.id, "size"); if (it.count <= 0) throw new InvalidDataException("route.json item '" + it.id + "': 'count' > 0"); break;
                }
            }
            if (lights > r.maxNewLights) throw new InvalidDataException("route.json: " + lights + " luces nuevas > máximo " + r.maxNewLights);
            if (shadows > r.maxShadowLights) throw new InvalidDataException("route.json: " + shadows + " luces con sombra > máximo " + r.maxShadowLights);
            return d;
        }
        static void Need(float[] a, int n, string id, string field) { if (a == null || a.Length < n) throw new InvalidDataException("route.json item '" + id + "': '" + field + "' necesita " + n + " valores"); }
        static void Positive(float v, string id, string field) { if (!(v > 0)) throw new InvalidDataException("route.json item '" + id + "': '" + field + "' debe ser > 0"); }
        static void NeedText(string v, string id, string field) { if (string.IsNullOrEmpty(v)) throw new InvalidDataException("route.json item '" + id + "': falta '" + field + "'"); }
        static Vector3 V3(float[] a) => a == null || a.Length < 3 ? Vector3.zero : new Vector3(a[0], a[1], a[2]);
        static Vector2 V2(float[] a) => a == null || a.Length < 2 ? Vector2.zero : new Vector2(a[0], a[1]);
        static Quaternion Rot(float[] e) => e == null || e.Length < 3 ? Quaternion.identity : Quaternion.Euler(e[0], e[1], e[2]);
        static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        // ------------------------------------------------------------------ scene
        static Scene OpenHangar(StringBuilder log)
        {
            Map01Scenes.GuardForeignDirtyScenes();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isDirty) throw new InvalidOperationException("La escena '" + s.path + "' tiene cambios sin guardar. Esta herramienta no guarda otras escenas ni descarta cambios: guárdala o ciérrala tú.");
            }
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Falta " + ScenePath);
            var active = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount == 1 && active.path == ScenePath) { log.AppendLine("escena ya abierta " + ScenePath); return active; }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            log.AppendLine("abierta " + ScenePath + " (Single)");
            return scene;
        }

        static void Backup(Ctx c)
        {
            var dir = "Tools/IronRequiem/Backups/" + DateTime.Now.ToString("yyyyMMdd") + "/hangar-route-" + DateTime.Now.ToString("HHmmss");
            Directory.CreateDirectory(dir);
            foreach (var f in new[] { ScenePath, NavPath, DataPath })
            {
                if (!File.Exists(f)) continue;
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
                if (File.Exists(f + ".meta")) File.Copy(f + ".meta", Path.Combine(dir, Path.GetFileName(f) + ".meta"), true);
            }
            c.log.AppendLine("respaldo " + dir);
        }

        static Transform Required(Transform t, string what) { if (!t) throw new InvalidOperationException("Room00_Hangar: falta '" + what + "'"); return t; }

        static void FindScene(Ctx c)
        {
            GameObject Root(string n) => c.scene.GetRootGameObjects().FirstOrDefault(g => g.name == n);
            var sector = Root("HangarSector"); c.sector = Required(sector ? sector.transform : null, "HangarSector");
            var cine = Root("WakeUpCinematic"); var cineT = Required(cine ? cine.transform : null, "WakeUpCinematic");
            c.stand = Required(cineT.Find("StandPoint"), "WakeUpCinematic/StandPoint");
            c.shots = Required(cineT.Find("Shots"), "WakeUpCinematic/Shots");
            c.pod = Required(c.sector.Find("Capsule_C04"), "HangarSector/Capsule_C04");
            c.capsule = V2(c.rules.capsuleCenter);
            var podXZ = new Vector2(c.pod.position.x, c.pod.position.z);
            if (Vector2.Distance(podXZ, c.capsule) > .3f) c.warnings.Add("rules.capsuleCenter " + c.capsule + " difiere de Capsule_C04 " + podXZ + "; se usa la más restrictiva (ambas)");
            var pts = Required(c.sector.Find("ScrapPoints"), "HangarSector/ScrapPoints");
            foreach (Transform p in pts) c.points.Add(p.position);
            if (c.points.Count == 0) throw new InvalidOperationException("HangarSector/ScrapPoints no tiene puntos");
            c.log.AppendLine("stand " + F(c.stand.position) + " forward " + F(c.stand.forward) + " · cápsula " + F(c.pod.position) + " · ScrapPoints " + c.points.Count);
        }

        static void LoadAssets(Ctx c)
        {
            cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cylMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (!cubeMesh || !cylMesh || !quadMesh) throw new InvalidOperationException("Mallas integradas Cube/Cylinder/Quad no disponibles");
            c.scrap = ScrapPrefabs.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
            for (int i = 0; i < c.scrap.Length; i++) if (!c.scrap[i]) throw new FileNotFoundException("Falta el prefab de chatarra " + ScrapPrefabs[i]);
            c.cable = AssetDatabase.LoadAssetAtPath<Mesh>(CableMeshPath); if (!c.cable) throw new FileNotFoundException("Falta " + CableMeshPath);
            c.emergency = AssetDatabase.LoadAssetAtPath<Mesh>(EmergencyMeshPath); if (!c.emergency) throw new FileNotFoundException("Falta " + EmergencyMeshPath);
            // fail early on every material named in the data
            foreach (var it in c.data.items) { if (!string.IsNullOrEmpty(it.material)) Mat(c, it.material); if (!string.IsNullOrEmpty(it.material2)) Mat(c, it.material2); }
        }

        static Material Mat(Ctx c, string name)
        {
            if (c.mats.TryGetValue(name, out var m)) return m;
            foreach (var f in MaterialFolders) { m = AssetDatabase.LoadAssetAtPath<Material>(f + "/" + name + ".mat"); if (m) break; }
            if (!m) throw new FileNotFoundException("Material '" + name + "' no encontrado en " + string.Join(", ", MaterialFolders));
            c.mats[name] = m; return m;
        }

        static string PathOf(Transform t) { var parts = new List<string>(); for (var p = t; p; p = p.parent) parts.Add(p.name); parts.Reverse(); return string.Join("/", parts); }
        static Transform FindPath(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
            if (!root) return null;
            return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
        }

        static void RestoreAndClear(Ctx c)
        {
            foreach (var old in c.scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray())
            {
                var rec = old.transform.Find(DisabledRecord);
                if (rec)
                    foreach (Transform r in rec)
                    {
                        var t = FindPath(c.scene, r.name);
                        if (t && !t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "Restore " + t.name); t.gameObject.SetActive(true); c.log.AppendLine("reactivado (build anterior) " + r.name); }
                    }
                Undo.DestroyObjectImmediate(old);
                c.log.AppendLine("borrado " + RootName + " anterior");
            }
        }

        // ------------------------------------------------------------------ cinematic cones
        static void BuildCones(Ctx c)
        {
            var S = c.stand.position; var up = Vector3.up;
            var face = Vector3.ProjectOnPlane(c.stand.forward, up).normalized; var right = Vector3.Cross(up, face);
            var belt = c.sector.Find("CinematicBeltTarget");
            var all = c.shots.GetComponentsInChildren<Transform>(true).Where(t => t != c.shots).ToList();
            var covered = new HashSet<Transform>();
            foreach (var sh in c.data.shots)
            {
                Vector3 target;
                if (sh.pod) target = c.pod.position;
                else if (sh.belt.HasValue) target = Vector3.Lerp(S + up * sh.up, belt ? belt.position : S, sh.belt.Value);
                else target = S + face * sh.face + right * sh.right + up * sh.up;
                int n = 0;
                foreach (var t in all.Where(t => t.name.Contains(sh.name)))
                {
                    var d = target - t.position; if (d.magnitude < .1f) continue;
                    c.cones.Add(new ShotCone { name = t.name, origin = t.position, dir = d.normalized, length = d.magnitude + .75f, tan = Mathf.Tan(sh.halfAngle * Mathf.Deg2Rad) });
                    covered.Add(t); n++;
                }
                c.log.AppendLine("cono " + sh.name + ": " + n + " anclajes, objetivo " + F(target) + ", semiángulo " + sh.halfAngle + "°");
                if (n == 0) c.warnings.Add("ningún transform de Shots contiene '" + sh.name + "'");
            }
            foreach (var t in all.Where(t => !covered.Contains(t)))
            {
                c.cones.Add(new ShotCone { name = t.name, origin = t.position, dir = t.forward, length = 6f, tan = Mathf.Tan(20f * Mathf.Deg2Rad) });
                c.warnings.Add("Shot sin definición en route.json: " + t.name + " (cono genérico 20°, 6 m)");
            }
        }

        static bool ConeHit(Ctx c, Bounds b, out string which)
        {
            foreach (var k in c.cones)
                for (float t = .3f; t <= k.length; t += .25f)
                {
                    var p = k.origin + k.dir * t; float r = t * k.tan;
                    if (b.SqrDistance(p) <= r * r) { which = k.name + " a " + t.ToString("0.0", CultureInfo.InvariantCulture) + " m"; return true; }
                }
            which = null; return false;
        }

        static float XZDistance(Vector2 p, Bounds b)
        {
            float dx = Mathf.Max(b.min.x - p.x, 0, p.x - b.max.x), dz = Mathf.Max(b.min.z - p.y, 0, p.y - b.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// True when a spot (bounds) breaks a placement rule: used by the self-avoiding scatter (markers, litter).
        static bool Forbidden(Ctx c, Bounds b, bool decor, out string why)
        {
            float keep = c.rules.capsuleKeepOut + .2f;
            if (XZDistance(c.capsule, b) < keep || XZDistance(new Vector2(c.pod.position.x, c.pod.position.z), b) < keep) { why = "cápsula"; return true; }
            float pk = decor ? c.rules.scrapPointKeepOutDecor : c.rules.scrapPointKeepOut;
            foreach (var p in c.points) if (XZDistance(new Vector2(p.x, p.z), b) < pk) { why = "ScrapPoint"; return true; }
            if (ConeHit(c, b, out var w)) { why = "cono " + w; return true; }
            why = null; return false;
        }

        // ------------------------------------------------------------------ primitives
        static Transform Group(Transform parent, string name, Vector3 lp, Quaternion lr)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = lp; t.localRotation = lr; return t;
        }

        static GameObject Prim(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 lp, Quaternion lr, Vector3 ls, bool shadows)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = lp; go.transform.localRotation = lr; go.transform.localScale = ls;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return go;
        }
        static GameObject Box(Transform parent, string name, Material m, Vector3 lp, Vector3 size, Vector3 euler = default, bool shadows = false)
            => Prim(parent, name, cubeMesh, new[] { m }, lp, Quaternion.Euler(euler), size, shadows);
        static GameObject Cyl(Transform parent, string name, Material m, Vector3 lp, float diameter, float height, Vector3 euler = default, bool shadows = false)
            => Prim(parent, name, cylMesh, new[] { m }, lp, Quaternion.Euler(euler), new Vector3(diameter, height * .5f, diameter), shadows);

        static BoxCollider AddBox(Ctx c, GameObject host, Vector3 center, Vector3 size)
        {
            var b = host.AddComponent<BoxCollider>(); b.center = center; b.size = size; host.layer = 0;
            c.newColliders.Add(b); return b;
        }

        static GameObject PlaceScrap(Ctx c, Transform parent, int pick, Vector3 lp, Quaternion lr, float size, bool shadows, string name)
        {
            var prefab = c.scrap[Mathf.Abs(pick) % c.scrap.Length];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, c.scene);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = lp; go.transform.localRotation = lr; go.transform.localScale = Vector3.one * size;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // ------------------------------------------------------------------ builders
        static void BuildItem(Ctx c, HangarRouteItem it)
        {
            var g = Group(c.root, it.id, V3(it.pos), Rot(it.euler));
            switch (it.type)
            {
                case "container": BuildContainer(c, it, g); break;
                case "rack": BuildRack(c, it, g); break;
                case "bin": BuildBins(c, it, g); break;
                case "crates": BuildCrates(c, it, g); break;
                case "cart": BuildCart(c, it, g); break;
                case "cover": BuildCover(c, it, g); break;
                case "hopper": BuildHopper(c, it, g); break;
                case "wreck": g.localRotation = Quaternion.identity; BuildWreck(c, it, g); break;
                case "pile": g.localRotation = Quaternion.identity; BuildPile(c, it, g); break;
                case "loose": g.localRotation = Quaternion.identity; PlaceScrap(c, g, it.seed, Vector3.down * .03f, Rot(it.euler), it.scale, false, "Scrap"); break;
                case "crane": g.localRotation = Quaternion.identity; BuildCrane(c, it, g); break;
                case "hang": BuildHang(c, it, g); break;
                case "sign": BuildSign(c, it, g); break;
                case "decal": Prim(g, "Decal", quadMesh, new[] { Mat(c, it.material) }, Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(it.size[0], it.size[1], 1), false); break;
                case "marker": g.localPosition = Vector3.zero; g.localRotation = Quaternion.identity; BuildMarkers(c, it, g); break;
                case "emergency": Prim(g, "Fixture", c.emergency, new[] { Mat(c, "VO_Prop_Housing"), Mat(c, "VO_Light_Amber") }, Vector3.zero, Quaternion.identity, Vector3.one, false); break;
                case "light": BuildLight(c, it, g); break;
                case "fx": g.localRotation = Quaternion.identity; BuildFx(c, it, g); break;
                case "litter": g.localPosition = Vector3.zero; g.localRotation = Quaternion.identity; BuildLitter(c, it, g); break;
            }
        }

        static void BuildContainer(Ctx c, HangarRouteItem it, Transform g)
        {
            float w = it.size[0], h = it.size[1], l = it.size[2]; int count = Mathf.Max(1, it.count);
            var trim = Mat(c, "HG_Trim"); var door = Mat(c, "HG_PlateDark");
            for (int k = 0; k < count; k++)
            {
                var unit = Group(g, "Unit" + k, new Vector3(k == 0 ? 0 : (k % 2 == 1 ? .22f : -.18f), h * k, k == 0 ? 0 : -.15f * k), Quaternion.Euler(0, k == 0 ? 0 : 4f * k, 0));
                var m = k == 0 || string.IsNullOrEmpty(it.material2) ? Mat(c, it.material) : Mat(c, it.material2);
                Box(unit, "Body", m, new Vector3(0, h * .5f, 0), new Vector3(w, h, l), default, it.shadows);
                for (int i = 0; i < 4; i++)
                {
                    float z = -l * .5f + l * (i + .5f) / 4f;
                    foreach (var s in new[] { -1f, 1f }) Box(unit, "Rib", m, new Vector3(s * (w * .5f + .02f), h * .5f, z), new Vector3(.05f, h * .92f, .14f));
                }
                foreach (var s in new[] { -1f, 1f })
                {
                    Box(unit, "Door", door, new Vector3(s * w * .25f, h * .5f, l * .5f + .03f), new Vector3(w * .5f - .05f, h - .12f, .04f));
                    Box(unit, "LockBar", trim, new Vector3(s * w * .12f, h * .5f, l * .5f + .07f), new Vector3(.04f, h - .25f, .04f));
                }
            }
            if (it.collider) AddBox(c, g.gameObject, new Vector3(0, h * count * .5f, 0), new Vector3(w + .1f, h * count, l + .1f));
        }

        static void BuildRack(Ctx c, HangarRouteItem it, Transform g)
        {
            float L = it.size[0], H = it.size[1], D = it.size[2]; int levels = Mathf.Max(2, it.levels);
            int bays = Mathf.Max(1, Mathf.RoundToInt(L / 2.4f)); float bay = L / bays;
            var frame = Mat(c, it.material ?? "VO_Env_MetalTri"); var board = Mat(c, it.material2 ?? "Bunker_Grating");
            var rnd = new System.Random(it.seed);
            Material[] parts = { Mat(c, "HG_Plate"), Mat(c, "Bunker_RustMetal"), Mat(c, "VO_Prop_Housing"), Mat(c, "HG_PlateDark") };
            Material[] labels = { Mat(c, "HG_CyanGlow"), Mat(c, "HG_OrangeGlow"), Mat(c, "HG_RedGlow") };
            var trim = Mat(c, "HG_Trim"); var hose = Mat(c, "HG_Hose");
            for (int b = 0; b <= bays; b++)
            {
                float x = Mathf.Clamp(-L * .5f + bay * b, -L * .5f + .04f, L * .5f - .04f);
                foreach (var s in new[] { -1f, 1f }) Box(g, "Upright", frame, new Vector3(x, H * .5f, s * (D * .5f - .04f)), new Vector3(.08f, H, .08f));
                if (b < bays)
                {
                    float diag = Mathf.Sqrt(bay * bay + H * H), ang = Mathf.Atan2(H, bay) * Mathf.Rad2Deg * (b % 2 == 0 ? 1 : -1);
                    Box(g, "Brace", trim, new Vector3(x + bay * .5f, H * .5f, -D * .5f + .03f), new Vector3(diag, .04f, .02f), new Vector3(0, 0, ang));
                }
            }
            float gap = (H - .25f) / (levels - 1);
            for (int j = 0; j < levels; j++)
            {
                float y = .1f + gap * j;
                Box(g, "Shelf", board, new Vector3(0, y, 0), new Vector3(L, .05f, D));
                Box(g, "Lip", trim, new Vector3(0, y + .03f, D * .5f - .015f), new Vector3(L, .07f, .03f));
                float room = j == levels - 1 ? .55f : gap - .14f;
                for (int b = 0; b < bays; b++)
                {
                    int n = 1 + rnd.Next(2);
                    for (int k = 0; k < n; k++)
                    {
                        float xc = -L * .5f + bay * (b + (k + .5f) / n) + Rf(rnd, -.15f, .15f), zc = Rf(rnd, -D * .15f, D * .15f);
                        double p = rnd.NextDouble();
                        if (p < .5)
                        {
                            float bw = Rf(rnd, .3f, Mathf.Min(.7f, bay / n - .1f)), bh = Rf(rnd, .18f, Mathf.Min(.45f, room)), bd = Rf(rnd, .25f, D - .15f);
                            Box(g, "PartsBox", parts[rnd.Next(parts.Length)], new Vector3(xc, y + .025f + bh * .5f, zc), new Vector3(bw, bh, bd), new Vector3(0, Rf(rnd, -10, 10), 0));
                        }
                        else if (p < .75)
                        {
                            float d = Mathf.Min(Rf(rnd, .18f, .34f), room);
                            Cyl(g, "Drum", rnd.Next(2) == 0 ? trim : hose, new Vector3(xc, y + .025f + d * .5f, zc), d, Rf(rnd, .3f, .6f), new Vector3(0, Rf(rnd, -20, 20), 90));
                        }
                        else PlaceScrap(c, g, rnd.Next(3), new Vector3(xc, y + .02f, zc), Quaternion.Euler(Rf(rnd, -15, 15), Rf(rnd, 0, 360), Rf(rnd, -15, 15)), Mathf.Min(Rf(rnd, .35f, .55f), room / .66f), false, "ShelfScrap");
                    }
                    if (rnd.NextDouble() < .35) Box(g, "Label", labels[rnd.Next(labels.Length)], new Vector3(-L * .5f + bay * (b + .5f), y + .03f, D * .5f + .002f), new Vector3(.16f, .045f, .01f));
                }
            }
            if (it.collider) AddBox(c, g.gameObject, new Vector3(0, H * .5f, 0), new Vector3(L, H, D));
        }

        static void BuildBins(Ctx c, HangarRouteItem it, Transform g)
        {
            float w = it.size[0], h = it.size[1], d = it.size[2]; int n = Mathf.Max(1, it.count);
            var m = Mat(c, it.material ?? "Bunker_RustMetal"); var fill = Mat(c, "HG_Debris");
            Material[] labels = { Mat(c, "HG_CyanGlow"), Mat(c, "HG_OrangeGlow"), Mat(c, "HG_RedGlow") };
            var rnd = new System.Random(it.seed);
            for (int i = 0; i < n; i++)
            {
                float x = -n * w * .5f + w * (i + .5f);
                var bin = Group(g, "Bin" + i, new Vector3(x, 0, 0), Quaternion.identity);
                Box(bin, "Bottom", m, new Vector3(0, .04f, 0), new Vector3(w - .04f, .08f, d - .04f));
                foreach (var s in new[] { -1f, 1f })
                {
                    Box(bin, "WallZ", m, new Vector3(0, h * .5f, s * (d * .5f - .025f)), new Vector3(w - .04f, h, .05f));
                    Box(bin, "WallX", m, new Vector3(s * (w * .5f - .045f), h * .5f, 0), new Vector3(.05f, h, d - .04f));
                }
                Box(bin, "Fill", fill, new Vector3(0, h * .72f, 0), new Vector3(w - .14f, .05f, d - .14f));
                PlaceScrap(c, bin, rnd.Next(3), new Vector3(Rf(rnd, -.1f, .1f), h * .6f, Rf(rnd, -.1f, .1f)), Quaternion.Euler(Rf(rnd, -30, 30), Rf(rnd, 0, 360), Rf(rnd, -30, 30)), Rf(rnd, .45f, .6f), false, "BinScrap");
                Box(bin, "Label", labels[i % labels.Length], new Vector3(0, h * .8f, d * .5f + .006f), new Vector3(w * .55f, .1f, .012f));
            }
            if (it.collider) AddBox(c, g.gameObject, new Vector3(0, h * .5f, 0), new Vector3(n * w, h, d));
        }

        static void Crate(Ctx c, Transform parent, string name, Material m, Vector3 lp, Vector3 size, float yaw)
        {
            var t = Group(parent, name, lp, Quaternion.Euler(0, yaw, 0));
            Box(t, "Body", m, new Vector3(0, size.y * .5f, 0), size);
            var trim = Mat(c, "HG_Trim");
            Box(t, "Band", trim, new Vector3(0, size.y * .2f, 0), new Vector3(size.x + .02f, .06f, size.z + .02f));
            Box(t, "Band", trim, new Vector3(0, size.y * .8f, 0), new Vector3(size.x + .02f, .06f, size.z + .02f));
            Box(t, "Stencil", Mat(c, "HG_Hazard"), new Vector3(0, size.y * .5f, size.z * .5f + .006f), new Vector3(size.x * .5f, size.y * .22f, .01f));
        }

        static void BuildCrates(Ctx c, HangarRouteItem it, Transform g)
        {
            var s = V3(it.size); var m = Mat(c, it.material ?? "Bunker_RustMetal");
            Crate(c, g, "CrateA", m, new Vector3(-s.x * .5f - .015f, 0, 0), s, 3);
            Crate(c, g, "CrateB", m, new Vector3(s.x * .5f + .015f, 0, .02f), s, -2);
            if (it.count >= 3) Crate(c, g, "CrateTop", m, new Vector3(.08f, s.y, .03f), s * .92f, 9);
            if (it.collider)
            {
                AddBox(c, g.gameObject, new Vector3(0, s.y * .5f, 0), new Vector3(s.x * 2 + .03f, s.y, s.z));
                if (it.count >= 3) AddBox(c, g.gameObject, new Vector3(.08f, s.y * 1.46f, .03f), new Vector3(s.x, s.y * .92f, s.z));
            }
        }

        static void BuildCart(Ctx c, HangarRouteItem it, Transform g)
        {
            float w = it.size[0], h = it.size[1], l = it.size[2], y0 = .22f, bh = h - y0;
            var m = Mat(c, it.material ?? "Bunker_RustMetal"); var trim = Mat(c, "HG_Trim"); var rubber = Mat(c, "HG_Hose");
            var rnd = new System.Random(it.seed);
            Box(g, "Floor", m, new Vector3(0, y0 + .03f, 0), new Vector3(w, .06f, l), default, true);
            foreach (var s in new[] { -1f, 1f })
            {
                Box(g, "SideX", m, new Vector3(s * (w * .5f - .03f), y0 + bh * .5f, 0), new Vector3(.06f, bh, l));
                Box(g, "SideZ", m, new Vector3(0, y0 + bh * .5f, s * (l * .5f - .03f)), new Vector3(w, bh, .06f));
                foreach (var e in new[] { -1f, 1f }) Cyl(g, "Wheel", rubber, new Vector3(s * (w * .5f + .02f), .175f, e * (l * .5f - .3f)), .35f, .1f, new Vector3(0, 0, 90));
            }
            Box(g, "Rim", Mat(c, "HG_Hazard"), new Vector3(0, h - .03f, l * .5f - .03f), new Vector3(w + .02f, .08f, .08f));
            foreach (var e in new[] { -1f, 1f }) Box(g, "Axle", trim, new Vector3(0, .175f, e * (l * .5f - .3f)), new Vector3(w + .06f, .05f, .05f));
            Box(g, "TowBar", trim, new Vector3(0, .3f, -(l * .5f + .28f)), new Vector3(.07f, .07f, .56f));
            Box(g, "Fill", Mat(c, "HG_Debris"), new Vector3(0, h - .18f, 0), new Vector3(w - .12f, .06f, l - .12f));
            PlaceScrap(c, g, rnd.Next(3), new Vector3(-.18f, h - .32f, .35f), Quaternion.Euler(Rf(rnd, -25, 25), Rf(rnd, 0, 360), Rf(rnd, -25, 25)), .85f, false, "CartScrap");
            PlaceScrap(c, g, rnd.Next(3), new Vector3(.2f, h - .3f, -.4f), Quaternion.Euler(Rf(rnd, -25, 25), Rf(rnd, 0, 360), Rf(rnd, -25, 25)), .7f, false, "CartScrap");
            if (it.collider) AddBox(c, g.gameObject, new Vector3(0, h * .5f, 0), new Vector3(w, h, l));
        }

        static void BuildCover(Ctx c, HangarRouteItem it, Transform g)
        {
            float w = it.size[0], h = it.size[1], d = it.size[2];
            var m = Mat(c, it.material ?? "Bunker_RustMetal"); var rnd = new System.Random(it.seed);
            Box(g, "Pallet", Mat(c, "Bunker_Grating"), new Vector3(0, .06f, 0), new Vector3(w, .12f, d), default, true);
            Crate(c, g, "CrateA", m, new Vector3(-w * .22f, .12f, -d * .18f), new Vector3(.8f, Mathf.Min(.7f, h - .25f), .75f), 6);
            Crate(c, g, "CrateB", m, new Vector3(w * .2f, .12f, d * .2f), new Vector3(.75f, Mathf.Min(.64f, h - .3f), .7f), -10);
            Box(g, "LeanPlate", Mat(c, "HG_Plate"), new Vector3(w * .05f, .62f, -d * .43f), new Vector3(w * .9f, .95f, .05f), new Vector3(-14, 0, 0), true);
            PlaceScrap(c, g, rnd.Next(3), new Vector3(w * .12f, h - .6f, 0), Quaternion.Euler(Rf(rnd, -20, 20), Rf(rnd, 0, 360), Rf(rnd, -20, 20)), .95f, false, "CoverScrap");
            PlaceScrap(c, g, rnd.Next(3), new Vector3(-w * .3f, .1f, d * .3f), Quaternion.Euler(Rf(rnd, -40, 40), Rf(rnd, 0, 360), Rf(rnd, -40, 40)), .7f, false, "CoverScrap");
            if (it.collider) AddBox(c, g.gameObject, new Vector3(0, h * .5f, 0), new Vector3(w, h, d));
        }

        static void BuildHopper(Ctx c, HangarRouteItem it, Transform g)
        {
            float w = it.size[0], h = it.size[1], l = it.size[2];
            var m = Mat(c, it.material ?? "VO_Env_MetalTri"); var trim = Mat(c, "HG_Trim"); var haz = Mat(c, "HG_Hazard");
            var rnd = new System.Random(it.seed);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Box(g, "Leg", trim, new Vector3(sx * (w * .5f - .08f), .6f, sz * (l * .5f - .08f)), new Vector3(.12f, 1.2f, .12f));
            Box(g, "Throat", m, new Vector3(0, 1.0f, 0), new Vector3(w * .45f, .4f, l * .45f), default, true);
            float top = h - 1.2f, yc = 1.2f + top * .5f;
            foreach (var s in new[] { -1f, 1f })
            {
                Box(g, "FunnelX", m, new Vector3(s * w * .34f, yc, 0), new Vector3(.06f, top, l * .95f), new Vector3(0, 0, -s * 20f), true);
                Box(g, "FunnelZ", m, new Vector3(0, yc, s * l * .34f), new Vector3(w * .95f, top, .06f), new Vector3(s * 20f, 0, 0), true);
                Box(g, "RimZ", haz, new Vector3(0, h - .05f, s * l * .5f), new Vector3(w, .1f, .08f));
                Box(g, "RimX", haz, new Vector3(s * w * .5f, h - .05f, 0), new Vector3(.08f, .1f, l));
            }
            Box(g, "Chute", m, new Vector3(0, .62f, l * .3f), new Vector3(.45f, .06f, .9f), new Vector3(25, 0, 0));
            PlaceScrap(c, g, rnd.Next(3), new Vector3(-.15f, h - .55f, .35f), Quaternion.Euler(Rf(rnd, -30, 30), Rf(rnd, 0, 360), Rf(rnd, -30, 30)), .85f, false, "HopperScrap");
            PlaceScrap(c, g, rnd.Next(3), new Vector3(.18f, h - .5f, -.4f), Quaternion.Euler(Rf(rnd, -30, 30), Rf(rnd, 0, 360), Rf(rnd, -30, 30)), .75f, false, "HopperScrap");
            if (it.collider) AddBox(c, g.gameObject, new Vector3(0, h * .5f, 0), new Vector3(w, h, l));
        }

        static void BuildWreck(Ctx c, HangarRouteItem it, Transform g)
        {
            var mesh = Wreck(c, it.source, out var mats);
            var b = mesh.bounds; float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            var rot = Rot(it.euler);
            Bounds Rotated(float sc)
            {
                var r = new Bounds(rot * (b.center * sc), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    r.Encapsulate(rot * (corner * sc));
                }
                return r;
            }
            float s = Mathf.Min(1f, it.length / Mathf.Max(.01f, maxDim));
            var A = Rotated(s);
            if (it.size != null && it.size.Length >= 3)
            {
                float cap = Mathf.Min(it.size[0] / Mathf.Max(.01f, A.size.x), Mathf.Min(it.size[1] / Mathf.Max(.01f, A.size.y), it.size[2] / Mathf.Max(.01f, A.size.z)));
                if (cap < 1f) { s *= cap; A = Rotated(s); }
            }
            float sink = it.bench ? 0f : Mathf.Min(.06f, A.size.y * .04f);
            var hull = Prim(g, "Hull_" + it.source, mesh, mats, new Vector3(-A.center.x, -A.min.y - sink, -A.center.z), rot, Vector3.one * s, it.shadows);
            c.log.AppendLine($"wreck {it.id}: {it.source} escala {s:0.00} caja {F(A.size)} materiales {mats.Length}");
            float topY = A.size.y - sink;
            if (it.bench)
            {
                float y = it.pos[1], bx = A.size.x + .35f, bz = A.size.z + .35f;
                var trim = Mat(c, "HG_Trim");
                Box(g, "BenchTop", Mat(c, "Bunker_Grating"), new Vector3(0, -.04f, 0), new Vector3(bx, .08f, bz), default, true);
                Box(g, "BenchFrame", trim, new Vector3(0, -.12f, 0), new Vector3(bx - .1f, .08f, bz - .1f));
                foreach (var sx in new[] { -1f, 1f })
                    foreach (var sz in new[] { -1f, 1f })
                        Box(g, "BenchLeg", trim, new Vector3(sx * (bx * .5f - .08f), -y * .5f, sz * (bz * .5f - .08f)), new Vector3(.1f, y - .08f, .1f));
                Box(g, "Vise", Mat(c, "HG_Hazard"), new Vector3(0, .06f, bz * .5f - .1f), new Vector3(.3f, .12f, .1f));
                Box(g, "Vise", Mat(c, "HG_Hazard"), new Vector3(0, .06f, -bz * .5f + .1f), new Vector3(.3f, .12f, .1f));
                if (it.collider) { float top = Mathf.Min(A.size.y, .7f); AddBox(c, g.gameObject, new Vector3(0, (top - y) * .5f, 0), new Vector3(bx, y + top, bz)); }
            }
            else if (it.collider) AddBox(c, g.gameObject, new Vector3(0, topY * .5f, 0), new Vector3(A.size.x * .85f, topY, A.size.z * .85f));
        }

        /// Static decoration mesh of a current enemy prefab: every visible MeshRenderer/SkinnedMeshRenderer (bind pose, LOD0,
        /// no FX, no transparent or glowing parts) merged into one mesh with one submesh per material. The prefab is only read
        /// (LoadPrefabContents/UnloadPrefabContents): no scripts, no AI, nothing saved to it. Kept in Generated/Hangar (GUID kept).
        static Mesh Wreck(Ctx c, string src, out Material[] mats)
        {
            if (c.wrecks.TryGetValue(src, out var cached)) { mats = c.wreckMats[src]; return cached; }
            var path = WreckPrefabs[src];
            if (!File.Exists(path)) throw new FileNotFoundException("Falta el prefab de enemigo " + path);
            EnsureFolder(GeneratedFolder);
            var contents = PrefabUtility.LoadPrefabContents(path);
            var temps = new List<Mesh>();
            try
            {
                var excluded = new HashSet<Renderer>();
                foreach (var lg in contents.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) excluded.Add(r);
                }
                var w2l = contents.transform.worldToLocalMatrix;
                var byMat = new Dictionary<Material, List<CombineInstance>>(); var order = new List<Material>();
                int used = 0; var skipped = new List<string>();
                foreach (var r in contents.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    var why = SkipReason(r, excluded);
                    if (why != null) { skipped.Add(r.name + "(" + why + ")"); continue; }
                    Mesh m; Matrix4x4 mx;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        m = new Mesh(); smr.BakeMesh(m, true); temps.Add(m);
                        mx = w2l * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    }
                    else { m = r.GetComponent<MeshFilter>().sharedMesh; mx = w2l * r.transform.localToWorldMatrix; }
                    var sm = r.sharedMaterials;
                    for (int s = 0; s < m.subMeshCount; s++)
                    {
                        var mat = sm[Mathf.Min(s, sm.Length - 1)];
                        if (!byMat.TryGetValue(mat, out var list)) { list = new List<CombineInstance>(); byMat[mat] = list; order.Add(mat); }
                        list.Add(new CombineInstance { mesh = m, subMeshIndex = s, transform = mx });
                    }
                    used++;
                }
                if (order.Count == 0) throw new InvalidOperationException("El prefab " + path + " no tiene renderers utilizables para el casco");
                var parts = new List<CombineInstance>();
                foreach (var mat in order)
                {
                    var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                    part.CombineMeshes(byMat[mat].ToArray(), true, true, false);
                    temps.Add(part);
                    parts.Add(new CombineInstance { mesh = part, subMeshIndex = 0, transform = Matrix4x4.identity });
                }
                var combined = new Mesh { name = "HGR_Wreck_" + src, indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(parts.ToArray(), false, false, false);
                combined.RecalculateBounds();
                string assetPath = GeneratedFolder + "/HGR_Wreck_" + src + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (existing) { EditorUtility.CopySerialized(combined, existing); existing.name = "HGR_Wreck_" + src; Object.DestroyImmediate(combined); combined = existing; EditorUtility.SetDirty(existing); }
                else AssetDatabase.CreateAsset(combined, assetPath);
                long tris = 0; for (int s = 0; s < combined.subMeshCount; s++) tris += combined.GetIndexCount(s) / 3;
                c.log.AppendLine($"casco {src}: {used} renderers fundidos, {order.Count} materiales, {tris} triángulos -> {assetPath}");
                if (skipped.Count > 0) c.log.AppendLine("  omitidos: " + string.Join(", ", skipped.Take(30)) + (skipped.Count > 30 ? " …(+" + (skipped.Count - 30) + ")" : ""));
                mats = order.ToArray();
                c.wrecks[src] = combined; c.wreckMats[src] = mats;
                return combined;
            }
            finally
            {
                foreach (var t in temps) if (t) Object.DestroyImmediate(t);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static string SkipReason(Renderer r, HashSet<Renderer> excluded)
        {
            if (excluded.Contains(r)) return "lod";
            if (!r.enabled || !r.gameObject.activeInHierarchy) return "inactivo";
            if (r is SkinnedMeshRenderer s && !s.sharedMesh) return "sin malla";
            if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) return "sin malla"; }
            var n = r.name.ToLowerInvariant();
            if (n == "fx" || n.StartsWith("fx_") || n.StartsWith("fx")) return "nombre";
            foreach (var k in SkipNameKeys) if (n.Contains(k)) return "nombre";
            var ms = r.sharedMaterials;
            if (ms.Length == 0) return "sin material";
            foreach (var m in ms)
            {
                if (!m) return "sin material";
                if (m.renderQueue >= 2500) return "transparente";
                var mn = m.name.ToLowerInvariant();
                if (mn.Contains("glow") || mn.Contains("emiss") || mn.Contains("flash") || mn.Contains("laser")) return "brillo";
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// Free or leaning scrap heap from the hangar's own wreck prefabs (same recipe and collider shape as the yard piles).
        static void BuildPile(Ctx c, HangarRouteItem it, Transform pile)
        {
            float r = it.radius, h = it.height; var rnd = new System.Random(it.seed);
            var lean = V2(it.lean); var leanDir = lean.sqrMagnitude > 1e-4f ? new Vector3(lean.x, 0, lean.y).normalized : Vector3.zero;
            var tiltAxis = leanDir == Vector3.zero ? Vector3.right : Vector3.Cross(Vector3.up, leanDir);
            double upright = leanDir == Vector3.zero ? .35 : .6;
            Quaternion PieceRot(float tilt) => Quaternion.AngleAxis(tilt, tiltAxis) * Quaternion.Euler(Rf(rnd, -35, 35) + (rnd.NextDouble() < upright ? 90 : 0), Rf(rnd, 0, 360), Rf(rnd, -40, 40));
            bool leaning = leanDir != Vector3.zero;
            int baseCount = 3 + rnd.Next(2);
            for (int k = 0; k < baseCount; k++)
            {
                float size = r * Rf(rnd, 1.8f, 2.4f);
                var dir = Quaternion.Euler(0, k * (360f / baseCount) + Rf(rnd, -30, 30), 0) * Vector3.forward;
                var pos = dir * r * Rf(rnd, .05f, .3f) + leanDir * r * .15f + Vector3.down * size * Rf(rnd, .04f, .12f);
                PlaceScrap(c, pile, rnd.Next(3), pos, PieceRot(leaning ? Rf(rnd, 8, 22) : 0), size, it.shadows, "Base" + k);
            }
            int midCount = 2 + rnd.Next(2);
            for (int k = 0; k < midCount; k++)
            {
                float size = r * Rf(rnd, 1.3f, 1.7f);
                var dir = Quaternion.Euler(0, Rf(rnd, 0, 360), 0) * Vector3.forward;
                var pos = dir * r * Rf(rnd, 0, .45f) + leanDir * r * .35f + Vector3.up * (h * Rf(rnd, .3f, .42f) - size * .12f);
                PlaceScrap(c, pile, rnd.Next(3), pos, PieceRot(leaning ? Rf(rnd, 18, 32) : 0), size, false, "Mid" + k);
            }
            int topCount = 1 + rnd.Next(2);
            for (int k = 0; k < topCount; k++)
            {
                float size = r * Rf(rnd, .9f, 1.3f);
                var dir = Quaternion.Euler(0, Rf(rnd, 0, 360), 0) * Vector3.forward;
                var pos = dir * r * Rf(rnd, 0, .3f) + leanDir * r * .55f + Vector3.up * (h * Rf(rnd, .66f, .8f) - size * .08f);
                PlaceScrap(c, pile, rnd.Next(3), pos, PieceRot(leaning ? Rf(rnd, 25, 40) : 0), size, false, "Top" + k);
            }
            int loose = 2 + rnd.Next(2);
            for (int k = 0; k < loose; k++)
            {
                var dir = Quaternion.Euler(0, Rf(rnd, 0, 360), 0) * Vector3.forward;
                if (leaning && Vector3.Dot(dir, leanDir) > .3f) dir = -dir;
                var pos = dir * r * Rf(rnd, 1.35f, 1.8f) + Vector3.down * Rf(rnd, .02f, .08f);
                PlaceScrap(c, pile, rnd.Next(3), pos, Quaternion.Euler(Rf(rnd, -70, 70), Rf(rnd, 0, 360), Rf(rnd, -70, 70)), Rf(rnd, .35f, .7f), false, "Loose" + k);
            }
            if (it.collider)
            {
                AddBox(c, pile.gameObject, new Vector3(0, h * .25f, 0), new Vector3(r * 2.6f, h * .5f, r * 2.6f));
                AddBox(c, pile.gameObject, leanDir * r * .35f + Vector3.up * h * .65f, new Vector3(r * 1.7f, h * .5f, r * 1.7f));
            }
        }

        static void BuildCrane(Ctx c, HangarRouteItem it, Transform g)
        {
            float L = it.length, tx = it.target[0] - it.pos[0], ym = it.target[1] - it.pos[1];
            var dark = Mat(c, "HG_PlateDark"); var trim = Mat(c, "HG_Trim"); var haz = Mat(c, "HG_Hazard");
            Box(g, "Beam", dark, Vector3.zero, new Vector3(L, .9f, .6f));
            Box(g, "FlangeTop", trim, new Vector3(0, .47f, 0), new Vector3(L, .06f, .75f));
            Box(g, "FlangeBottom", trim, new Vector3(0, -.47f, 0), new Vector3(L, .06f, .75f));
            for (int k = -2; k <= 2; k++) Box(g, "HazardPlate", haz, new Vector3(k * L / 6f, 0, -.31f), new Vector3(1.6f, .5f, .02f));
            foreach (var s in new[] { -1f, 1f })
            {
                Box(g, "EndTruck", trim, new Vector3(s * (L * .5f - .5f), .1f, 0), new Vector3(1.0f, .7f, 1.4f));
                Box(g, "WallRail", trim, new Vector3(s * (L * .5f - .15f), -.55f, 0), new Vector3(.5f, .4f, 14f));
            }
            Box(g, "Trolley", Mat(c, "Bunker_RustMetal"), new Vector3(tx, -.85f, 0), new Vector3(1.6f, .7f, 1.3f));
            Cyl(g, "Drum", trim, new Vector3(tx, -1.32f, 0), .45f, 1.2f, new Vector3(0, 0, 90));
            Prim(g, "PowerCable", c.cable, new[] { Mat(c, "VO_Prop_Cable"), Mat(c, "VO_Prop_Housing") }, new Vector3(tx + 2.3f, -.55f, -.38f), Quaternion.identity, Vector3.one, false);
            float cableTop = -1.55f, cableBottom = ym + .32f, cl = cableTop - cableBottom;
            if (cl > .1f) foreach (var s in new[] { -1f, 1f }) Cyl(g, "HoistCable", Mat(c, "HG_Hose"), new Vector3(tx + s * .35f, (cableTop + cableBottom) * .5f, 0), .05f, cl);
            var magnet = Group(g, "Magnet", new Vector3(tx, ym, 0), Quaternion.identity);
            Cyl(magnet, "Disc", dark, Vector3.zero, 2.2f, .45f, default, it.shadows);
            Cyl(magnet, "Rim", haz, new Vector3(0, -.1f, 0), 2.28f, .12f, default, it.shadows);
            Cyl(magnet, "Cap", trim, new Vector3(0, .35f, 0), .9f, .3f, default, it.shadows);
            Cyl(magnet, "ChargeBand", Mat(c, "HG_OrangeGlow"), new Vector3(0, .07f, 0), 2.205f, .05f);
            Box(magnet, "Shackle", trim, new Vector3(0, .6f, 0), new Vector3(.5f, .25f, .12f));
            // load stuck to the magnet's face (its top touching the disc)
            var rnd = new System.Random(it.seed); float faceY = it.target[1] - .225f;
            for (int k = 0; k < 3; k++)
            {
                var piece = PlaceScrap(c, magnet, rnd.Next(3), new Vector3(Rf(rnd, -.5f, .5f), -.6f, Rf(rnd, -.4f, .4f)), Quaternion.Euler(Rf(rnd, -40, 40), Rf(rnd, 0, 360), Rf(rnd, -40, 40)), Rf(rnd, .9f, 1.3f), it.shadows, "Load" + k);
                var b = RendererBounds(piece);
                piece.transform.position += Vector3.up * (faceY + .06f - b.max.y);
            }
            c.log.AppendLine($"grúa: viga y {it.pos[1]:0.0} z {it.pos[2]:0.0}, imán ({it.target[0]:0.0}; {it.target[1]:0.0}) cara inferior y {faceY:0.00}");
        }

        static void BuildHang(Ctx c, HangarRouteItem it, Transform g)
        {
            float len = it.length;
            if (it.mode == "cable")
            {
                Prim(g, "Cable", c.cable, new[] { Mat(c, "VO_Prop_Cable"), Mat(c, "VO_Prop_Housing") }, new Vector3(0, -len * .5f, 0), Quaternion.Euler(0, 0, 90), new Vector3(len / 4f, 1, 1), false);
                return;
            }
            var trim = Mat(c, "HG_Trim");
            Cyl(g, "Chain", trim, new Vector3(0, -len * .5f, 0), .045f, len);
            if (it.mode == "hook")
            {
                Box(g, "HookBlock", Mat(c, "HG_Hazard"), new Vector3(0, -len - .15f, 0), new Vector3(.22f, .3f, .14f));
                Box(g, "HookShank", trim, new Vector3(0, -len - .42f, 0), new Vector3(.06f, .25f, .06f));
                Box(g, "HookBow", trim, new Vector3(.08f, -len - .55f, 0), new Vector3(.2f, .06f, .06f));
                Box(g, "HookTip", trim, new Vector3(.17f, -len - .48f, 0), new Vector3(.06f, .16f, .06f));
            }
            else Cyl(g, "Ring", trim, new Vector3(0, -len - .05f, 0), .14f, .04f, new Vector3(90, 0, 0));
        }

        static void BuildSign(Ctx c, HangarRouteItem it, Transform g)
        {
            float w = it.size[0], h = it.size[1];
            Box(g, "Plate", Mat(c, "HG_PlateDark"), new Vector3(0, 0, .03f), new Vector3(w, h, .06f));
            Box(g, "BarTop", Mat(c, "HG_Hazard"), new Vector3(0, h * .5f + .04f, .03f), new Vector3(w + .06f, .08f, .08f));
            Box(g, "BarBottom", Mat(c, "HG_Hazard"), new Vector3(0, -h * .5f - .04f, .03f), new Vector3(w + .06f, .08f, .08f));
            Box(g, "GlowStrip", Mat(c, "HG_OrangeGlow"), new Vector3(0, -h * .5f + .09f, -.005f), new Vector3(w - .3f, .035f, .01f));
            var tgo = new GameObject("Text"); tgo.transform.SetParent(g, false); tgo.transform.localPosition = new Vector3(0, .05f, -.012f);
            var tmp = tgo.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = it.text; tmp.alignment = TextAlignmentOptions.Center; tmp.color = new Color(1f, .62f, .25f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap; tmp.enableAutoSizing = true; tmp.fontSizeMin = .5f; tmp.fontSizeMax = 6f; tmp.characterSpacing = 4;
            tmp.rectTransform.sizeDelta = new Vector2(w - .3f, h - .25f);
            tmp.ForceMeshUpdate();
            var tr = tgo.GetComponent<MeshRenderer>(); if (tr) tr.shadowCastingMode = ShadowCastingMode.Off;
            float top = float.IsNaN(c.beamBottom) ? 2f : c.beamBottom - it.pos[1];
            float y0 = h * .5f + .08f;
            if (top > y0 + .05f) foreach (var s in new[] { -1f, 1f }) Cyl(g, "Chain", Mat(c, "HG_Trim"), new Vector3(s * (w * .5f - .3f), (y0 + top) * .5f, .03f), .035f, top - y0);
        }

        static void BuildMarkers(Ctx c, HangarRouteItem it, Transform g)
        {
            var a = new Vector2(it.pos[0], it.pos[2]); var b = V2(it.target); var dir = b - a; float L = dir.magnitude;
            if (L < .5f) return;
            dir /= L; var n = new Vector2(dir.y, -dir.x); var size = V3(it.size);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; var rot = Quaternion.Euler(0, yaw, 0);
            var glow = Mat(c, it.material); var trim = Mat(c, "HG_Trim");
            int placed = 0, skipped = 0; var reasons = new Dictionary<string, int>();
            for (int k = 0; k * it.spacing <= L + .01f; k++)
                foreach (var side in new[] { -1f, 1f })
                {
                    var p2 = a + dir * (k * it.spacing) + n * (it.radius * side);
                    var p = new Vector3(p2.x, it.pos[1], p2.y);
                    var probe = new Bounds(p + Vector3.up * size.y * .5f, new Vector3(Mathf.Max(size.x, size.z) + .1f, size.y + .02f, Mathf.Max(size.x, size.z) + .1f));
                    string why = null;
                    if (!Forbidden(c, probe, true, out why) && Physics.CheckBox(p + Vector3.up * .3f, new Vector3(.25f, .25f, .35f), rot, ~0, QueryTriggerInteraction.Ignore)) why = "collider";
                    if (why != null) { skipped++; var key = why.StartsWith("cono") ? "cono" : why; reasons[key] = reasons.TryGetValue(key, out var v) ? v + 1 : 1; continue; }
                    var m = Group(g, "Marker" + k + (side < 0 ? "L" : "R"), p, rot);
                    Box(m, "Frame", trim, new Vector3(0, .006f, 0), new Vector3(size.x + .06f, .012f, size.z + .06f));
                    Box(m, "Glow", glow, new Vector3(0, size.y * .5f + .004f, 0), size);
                    placed++;
                }
            c.log.AppendLine($"marcas de suelo {it.id}: {placed} colocadas, {skipped} omitidas ({string.Join(", ", reasons.Select(kv => kv.Key + "=" + kv.Value))})");
        }

        static void BuildLight(Ctx c, HangarRouteItem it, Transform g)
        {
            var l = g.gameObject.AddComponent<Light>();
            l.type = it.mode == "Spot" ? LightType.Spot : LightType.Point;
            l.color = new Color(it.color[0], it.color[1], it.color[2]); l.intensity = it.intensity; l.range = it.range;
            if (l.type == LightType.Spot) { l.spotAngle = it.angle; l.innerSpotAngle = it.angle * .55f; }
            l.shadows = it.shadows ? LightShadows.Soft : LightShadows.None;
            if (it.shadows) { l.shadowStrength = .85f; l.shadowResolution = LightShadowResolution.Low; }
            l.renderMode = LightRenderMode.Auto; l.lightmapBakeType = LightmapBakeType.Realtime;
            if (it.flicker) g.gameObject.AddComponent<LightFlicker>().Configure(l, null, null, null, it.seed);
        }

        static ParticleSystem.MinMaxGradient Fade(Color col, float peak)
        {
            var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(col, 0), new GradientColorKey(col, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(peak, .15f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(gr);
        }

        static void BuildFx(Ctx c, HangarRouteItem it, Transform g)
        {
            var go = new GameObject(it.mode == "sparks" ? "Sparks" : "Smoke"); go.transform.SetParent(g, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var pr = go.GetComponent<ParticleSystemRenderer>(); pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var e = ps.emission; var sh = ps.shape; var cl = ps.colorOverLifetime; cl.enabled = true;
            float rate = it.rate > 0 ? it.rate : 1f;
            if (it.mode == "sparks")
            {
                pr.sharedMaterial = Mat(c, "HG_FxSpark");
                main.duration = 3f; main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .6f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
                main.startSize = new ParticleSystem.MinMaxCurve(.015f, .04f); main.gravityModifier = 1.4f; main.maxParticles = 40;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .85f, .5f), new Color(.6f, .85f, 1f));
                e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(.4f, (short)6, (short)14, 3, Mathf.Max(.05f, .35f / rate)) { probability = .75f } });
                sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = .1f; sh.rotation = new Vector3(-90, 0, 0);
                pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = .05f; pr.lengthScale = 1.1f;
                cl.color = Fade(Color.white, 1f);
            }
            else
            {
                pr.sharedMaterial = Mat(c, "HG_FxSmoke");
                go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                main.prewarm = true; main.duration = 5f; main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f); main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .5f);
                main.startSize = new ParticleSystem.MinMaxCurve(.6f, 1.2f); main.maxParticles = 30; main.startColor = new Color(.55f, .58f, .62f, .12f);
                e.rateOverTime = rate;
                sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14; sh.radius = .2f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .5f, 1, 2.4f));
                cl.color = Fade(Color.white, .8f);
            }
        }

        static void BuildLitter(Ctx c, HangarRouteItem it, Transform g)
        {
            var rnd = new System.Random(it.seed); float sx = it.size[0], sz = it.size[2];
            Material[] mats = { Mat(c, "HG_Debris"), Mat(c, "Bunker_RustMetal"), Mat(c, "HG_Trim"), Mat(c, "HG_Plate"), Mat(c, "VO_Env_RustMetal") };
            var trim = Mat(c, "HG_Trim");
            int placed = 0;
            for (int tries = 0; tries < it.count * 40 && placed < it.count; tries++)
            {
                var p = new Vector3(it.pos[0] + Rf(rnd, -sx * .5f, sx * .5f), 0, it.pos[2] + Rf(rnd, -sz * .5f, sz * .5f));
                var probe = new Bounds(p + Vector3.up * .15f, new Vector3(1.7f, .3f, 1.7f));   // ≥ the longest part (bar 1.5 m) at any yaw
                if (Forbidden(c, probe, true, out _)) continue;
                if (Physics.CheckBox(p + Vector3.up * .35f, new Vector3(.45f, .3f, .45f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                var m = mats[rnd.Next(mats.Length)]; float yaw = Rf(rnd, 0, 360); var piece = Group(g, "Part" + placed, p, Quaternion.Euler(0, yaw, 0));
                switch (rnd.Next(5))
                {
                    case 0: Box(piece, "Plate", m, new Vector3(0, .02f, 0), new Vector3(Rf(rnd, .35f, .8f), .03f, Rf(rnd, .25f, .5f)), new Vector3(Rf(rnd, -6, 6), 0, Rf(rnd, -6, 6))); break;
                    case 1: Cyl(piece, "Rod", m, new Vector3(0, .04f, 0), Rf(rnd, .05f, .09f), Rf(rnd, .6f, 1.3f), new Vector3(90, 0, 0)); break;
                    case 2: Cyl(piece, "Gear", m, new Vector3(0, .035f, 0), Rf(rnd, .28f, .5f), .07f, new Vector3(Rf(rnd, -8, 8), 0, 0)); Cyl(piece, "Hub", trim, new Vector3(0, .06f, 0), .1f, .1f); break;
                    case 3: { float h = Rf(rnd, .1f, .24f); Box(piece, "Block", m, new Vector3(0, h * .5f, 0), new Vector3(Rf(rnd, .15f, .35f), h, Rf(rnd, .15f, .35f))); break; }
                    default: Box(piece, "Bar", m, new Vector3(0, .07f, 0), new Vector3(Rf(rnd, .8f, 1.5f), .14f, .08f), new Vector3(0, 0, Rf(rnd, -4, 4))); break;
                }
                placed++;
            }
            c.log.AppendLine($"piezas sueltas {it.id}: {placed}/{it.count}");
            if (placed < it.count) c.warnings.Add(it.id + ": solo " + placed + "/" + it.count + " piezas (el resto caía en zonas vetadas)");
        }

        static void SetStatic(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                bool isDynamic = t.GetComponent<ParticleSystem>() != null || t.GetComponent<Light>() != null || t.GetComponent<TMP_Text>() != null || t.GetComponent<LightFlicker>() != null;
                var flags = isDynamic ? (StaticEditorFlags)0 : (StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        // ------------------------------------------------------------------ validation
        static string ItemOf(Ctx c, Transform t) { for (var p = t; p; p = p.parent) if (p.parent == c.root) return p.name; return t.name; }

        static void ValidatePlacement(Ctx c)
        {
            var pod2 = new Vector2(c.pod.position.x, c.pod.position.z);
            int checkedRenderers = 0;
            var reported = new HashSet<string>();
            void Err(string s) { if (reported.Add(s)) c.errors.Add(s); }
            foreach (var r in c.root.GetComponentsInChildren<Renderer>(true))
            {
                var b = r is ParticleSystemRenderer ? new Bounds(r.transform.position, Vector3.one * .8f) : r.bounds;
                var item = ItemOf(c, r.transform); checkedRenderers++;
                float d = Mathf.Min(XZDistance(c.capsule, b), XZDistance(pod2, b));
                if (d < c.rules.capsuleKeepOut) Err(item + "/" + r.name + ": a " + d.ToString("0.00") + " m de la cápsula (< " + c.rules.capsuleKeepOut + ")");
                if (b.min.y < 2f) foreach (var p in c.points) { float dp = XZDistance(new Vector2(p.x, p.z), b); if (dp < c.rules.scrapPointKeepOutDecor) Err(item + ": a " + dp.ToString("0.00") + " m del ScrapPoint " + F(p)); }
                if (ConeHit(c, b, out var w)) Err(item + "/" + r.name + ": dentro del cono de " + w);
            }
            foreach (var col in c.newColliders)
            {
                var b = col.bounds; var item = ItemOf(c, col.transform);
                if (b.min.y < 2f) foreach (var p in c.points) { float dp = XZDistance(new Vector2(p.x, p.z), b); if (dp < c.rules.scrapPointKeepOut) Err(item + " (collider): a " + dp.ToString("0.00") + " m del ScrapPoint " + F(p) + " (< " + c.rules.scrapPointKeepOut + ")"); }
            }
            foreach (var l in c.root.GetComponentsInChildren<Light>(true))
            {
                var p = new Vector2(l.transform.position.x, l.transform.position.z);
                if (Vector2.Distance(p, c.capsule) < c.rules.capsuleKeepOut) Err(l.name + ": luz a menos de " + c.rules.capsuleKeepOut + " m de la cápsula");
            }
            c.log.AppendLine($"validación: {checkedRenderers} renderers, {c.newColliders.Count} colliders, {c.cones.Count} conos de Shot, {c.points.Count} ScrapPoints · errores {c.errors.Count}");
        }

        /// Existing loose scrap (FloorDebris/Debris*, ScrapPile*/Loose*) that a new collider would swallow is deactivated, never deleted.
        static void DisableOverlapped(Ctx c)
        {
            var zones = c.sector.Find("ScrapZones"); if (!zones) return;
            foreach (var t in zones.GetComponentsInChildren<Transform>(true))
            {
                if (!t.gameObject.activeInHierarchy) continue;
                if (!(t.name.StartsWith("Debris", StringComparison.Ordinal) || t.name.StartsWith("Loose", StringComparison.Ordinal))) continue;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                var b = RendererBounds(t.gameObject); if (b.size == Vector3.zero) continue;
                b.extents *= .8f;
                var hit = c.newColliders.FirstOrDefault(col => col.bounds.Intersects(b));
                if (!hit) continue;
                var path = PathOf(t);
                Undo.RecordObject(t.gameObject, "Disable " + t.name);
                t.gameObject.SetActive(false);
                new GameObject(path).transform.SetParent(c.disabled, false);
                c.disabledPaths.Add(path + " (bajo " + ItemOf(c, hit.transform) + ")");
            }
            c.log.AppendLine("desactivados (sustituidos): " + c.disabledPaths.Count);
            foreach (var p in c.disabledPaths) c.log.AppendLine("  - " + p);
        }

        static bool Obstacle(Collider col)
        {
            if (!col || col.isTrigger) return false;
            if (col is CharacterController) return false;
            if (col.attachedRigidbody && !col.attachedRigidbody.isKinematic) return false;
            if (col.GetComponentInParent<NavMeshAgent>()) return false;
            if (col.transform.root.name == "Scavengers" || col.transform.root.name == "WakeUpCinematic") return false;
            return true;
        }

        /// Grid (0.25 m) of the route zone at body height (0.15-1.65 m): is there a path from laneStart to laneGoal on which a
        /// disc of laneMinWidth fits? Reports the bottleneck width.
        static void LaneCheck(Ctx c)
        {
            var z = c.rules.routeZone; const float cs = .25f;
            int nx = Mathf.CeilToInt((z[2] - z[0]) / cs), nz = Mathf.CeilToInt((z[3] - z[1]) / cs);
            var blocked = new bool[nx, nz]; var blk = new List<Vector2>(); var buf = new Collider[24];
            var half = new Vector3(cs * .5f, .75f, cs * .5f);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector3(z[0] + (i + .5f) * cs, .9f, z[1] + (j + .5f) * cs);
                    int n = Physics.OverlapBoxNonAlloc(p, half, buf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++) if (Obstacle(buf[k])) { blocked[i, j] = true; blk.Add(new Vector2(p.x, p.z)); break; }
                }
            var clear = new float[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (blocked[i, j]) { clear[i, j] = -1f; continue; }
                    var p = new Vector2(z[0] + (i + .5f) * cs, z[1] + (j + .5f) * cs); float m = 1e6f;
                    foreach (var q in blk) { float d = (p - q).sqrMagnitude; if (d < m) m = d; }
                    clear[i, j] = Mathf.Sqrt(m) - cs * .5f;
                }
            Vector2Int Cell(float[] v) => new Vector2Int(Mathf.Clamp((int)((v[0] - z[0]) / cs), 0, nx - 1), Mathf.Clamp((int)((v[1] - z[1]) / cs), 0, nz - 1));
            var st = Cell(c.rules.laneStart); var gl = Cell(c.rules.laneGoal);
            bool Reach(float thr)
            {
                if (clear[st.x, st.y] < thr || clear[gl.x, gl.y] < thr) return false;
                var seen = new bool[nx, nz]; var q = new Queue<Vector2Int>(); q.Enqueue(st); seen[st.x, st.y] = true;
                while (q.Count > 0)
                {
                    var p = q.Dequeue(); if (p == gl) return true;
                    for (int di = -1; di <= 1; di++)
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int a = p.x + di, b = p.y + dj;
                            if (a < 0 || b < 0 || a >= nx || b >= nz || seen[a, b] || clear[a, b] < thr) continue;
                            seen[a, b] = true; q.Enqueue(new Vector2Int(a, b));
                        }
                }
                return false;
            }
            float lo = .01f, hi = 8;
            if (!Reach(lo)) { c.errors.Add("No hay ningún camino libre entre laneStart y laneGoal"); c.laneWidth = 0; return; }
            for (int it = 0; it < 14; it++) { float mid = (lo + hi) * .5f; if (Reach(mid)) lo = mid; else hi = mid; }
            c.laneWidth = lo * 2;
            c.log.AppendLine($"pasillo: cuello de botella {c.laneWidth:0.00} m (mínimo {c.rules.laneMinWidth}) · rejilla {nx}x{nz}, {blk.Count} celdas ocupadas");
            if (c.laneWidth < c.rules.laneMinWidth) c.errors.Add($"El pasillo hasta la puerta mide {c.laneWidth:0.00} m en su punto más estrecho (< {c.rules.laneMinWidth} m)");
        }

        static bool ClosestPointSupported(Collider col) => col is BoxCollider || col is SphereCollider || col is CapsuleCollider || (col is MeshCollider mc && mc.convex);

        /// Gaps between a new collider and any other collider that are wide enough to step into but too narrow to pass
        /// comfortably (snagGapMin..snagGapMax). Warnings only; a gap filled by a third collider is ignored.
        static void SnagCheck(Ctx c)
        {
            var buf = new Collider[64]; int found = 0;
            var done = new HashSet<string>();
            foreach (var a in c.newColliders)
            {
                var ab = a.bounds; if (ab.max.y < .25f || ab.min.y > 1.6f) continue;
                var ext = ab.extents + new Vector3(c.rules.snagGapMax, 0, c.rules.snagGapMax);
                int n = Physics.OverlapBoxNonAlloc(ab.center, ext, buf, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < n; k++)
                {
                    var b = buf[k];
                    if (b == a || !Obstacle(b) || !ClosestPointSupported(b)) continue;
                    var bb = b.bounds; if (bb.max.y < .25f || bb.min.y > 1.6f) continue;
                    if (b.transform.IsChildOf(a.transform) && a.gameObject == b.gameObject) continue;
                    var key = string.CompareOrdinal(PathOf(a.transform), PathOf(b.transform)) < 0 ? PathOf(a.transform) + "|" + PathOf(b.transform) + b.GetHashCode() : PathOf(b.transform) + "|" + PathOf(a.transform) + a.GetHashCode();
                    if (!done.Add(key)) continue;
                    var pa = a.ClosestPoint(bb.center); var pb = b.ClosestPoint(pa);
                    for (int i = 0; i < 6; i++) { pa = a.ClosestPoint(pb); pb = b.ClosestPoint(pa); }
                    float gap = Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z));
                    if (gap <= c.rules.snagGapMin || gap >= c.rules.snagGapMax) continue;
                    var mid = (pa + pb) * .5f; mid.y = .9f;
                    bool filled = Physics.OverlapSphere(mid, .05f, ~0, QueryTriggerInteraction.Ignore).Any(x => x != a && x != b && Obstacle(x));
                    if (filled) continue;
                    c.warnings.Add($"hueco de {gap:0.00} m entre {ItemOf(c, a.transform)} y {PathOf(b.transform)} en {F(mid)}");
                    found++;
                }
            }
            c.log.AppendLine("huecos que podrían enganchar: " + found + " (avisos)");
        }

        // ------------------------------------------------------------------ navmesh
        static Dictionary<string, bool> NavChecks(Ctx c, string label)
        {
            var res = new Dictionary<string, bool>(); var path = new NavMeshPath();
            bool Sample(Vector3 p, float r, out Vector3 q) { if (NavMesh.SamplePosition(p, out var hit, r, NavMesh.AllAreas)) { q = hit.position; return true; } q = p; return false; }
            bool door = Sample(V3(c.rules.doorProbe), 1.0f, out var doorP);
            res["puerta en la malla"] = door;
            var face = Vector3.ProjectOnPlane(c.stand.forward, Vector3.up).normalized;
            bool stand = Sample(c.stand.position, 1.5f, out var standP) || Sample(c.stand.position + face * 1.5f, 1.5f, out standP);
            res["StandPoint -> puerta"] = door && stand && NavMesh.CalculatePath(standP, doorP, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            for (int i = 0; i < c.points.Count; i++)
            {
                bool on = Sample(c.points[i], .6f, out var q);
                res["ScrapPoint" + i + " en la malla"] = on;
                res["ScrapPoint" + i + " -> puerta"] = on && door && NavMesh.CalculatePath(q, doorP, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            }
            var tri = NavMesh.CalculateTriangulation();
            c.log.AppendLine($"NavMesh {label}: {tri.indices.Length / 3} triángulos · " + string.Join(" · ", res.Select(kv => kv.Key + "=" + (kv.Value ? "ok" : "NO"))));
            return res;
        }

        /// Rebake of the hangar's existing NavMeshSurface data with the original settings (agent .6/1.6, climb .3, slope 35,
        /// voxel .1, physics colliders of HangarSector minus CeilingBlocker) plus the colliders of HG_ScrapRoute. The new data
        /// is tested in memory first; only if every route that worked before still works is it copied into the same asset.
        static void BakeAndCheckNav(Ctx c)
        {
            var surface = c.sector.GetComponent<NavMeshSurface>();
            if (!surface) throw new InvalidOperationException("HangarSector no tiene NavMeshSurface");
            var data = surface.navMeshData;
            if (!data) throw new InvalidOperationException("El NavMeshSurface de HangarSector no tiene NavMeshData");
            var dataPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(dataPath)) throw new InvalidOperationException("El NavMeshData del hangar no es un asset");
            if (dataPath != NavPath) c.warnings.Add("NavMeshData del hangar en " + dataPath + " (se esperaba " + NavPath + "); se rehornea ese mismo asset");
            if (!surface.isActiveAndEnabled) surface.AddData();
            var before = NavChecks(c, "antes");

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = NavRadius; settings.agentHeight = NavHeight; settings.agentClimb = NavClimb; settings.agentSlope = NavSlope;
            settings.overrideVoxelSize = true; settings.voxelSize = NavVoxel;
            var markups = new List<NavMeshBuildMarkup>();
            var ceiling = c.sector.Find("SectorShell/CeilingBlocker");
            if (ceiling) markups.Add(new NavMeshBuildMarkup { root = ceiling, ignoreFromBuild = true, applyToChildren = true });
            else c.warnings.Add("SectorShell/CeilingBlocker no encontrado: el horneado no puede excluirlo como el original");
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(c.sector, -1, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            int sectorSources = sources.Count;
            var extra = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(c.root, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), extra);
            sources.AddRange(extra);
            var bounds = data.sourceBounds.size.sqrMagnitude > 1f ? data.sourceBounds : NavLocalBounds;
            c.log.AppendLine($"NavMesh: fuentes HangarSector {sectorSources} + HG_ScrapRoute {extra.Count} · bounds {F(bounds.center)} {F(bounds.size)} (original {F(NavLocalBounds.center)} {F(NavLocalBounds.size)}) · origen {F(data.position)} {F(data.rotation.eulerAngles)}");
            var built = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, data.position, data.rotation);
            if (!built) throw new InvalidOperationException("NavMeshBuilder.BuildNavMeshData devolvió null");

            surface.RemoveData();
            var instance = NavMesh.AddNavMeshData(built, data.position, data.rotation);
            Dictionary<string, bool> after;
            try { after = NavChecks(c, "después (en memoria)"); }
            finally { NavMesh.RemoveNavMeshData(instance); }
            foreach (var kv in before)
            {
                after.TryGetValue(kv.Key, out var now);
                if (kv.Value && !now) c.errors.Add("NavMesh: '" + kv.Key + "' funcionaba antes y deja de funcionar");
                else if (!kv.Value) c.warnings.Add("NavMesh: '" + kv.Key + "' ya fallaba antes de la ruta (" + (now ? "ahora funciona" : "sigue fallando") + ")");
            }
            if (c.errors.Count > 0) { surface.AddData(); Object.DestroyImmediate(built); c.log.AppendLine("NavMesh: NO se escribe (rutas rotas); se conserva el anterior"); return; }
            built.name = data.name;
            EditorUtility.CopySerialized(built, data);
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(data);
            surface.navMeshData = data;
            surface.AddData();
            AssetDatabase.SaveAssetIfDirty(data);
            c.log.AppendLine("NavMesh: rehorneado en " + dataPath + " (mismo asset y GUID, mismo NavMeshSurface)");
        }

        // ------------------------------------------------------------------ protection, stats, report
        static string ProtectedHash(Ctx c)
        {
            var sb = new StringBuilder();
            void Add(Transform t)
            {
                foreach (var x in t.GetComponentsInChildren<Transform>(true))
                {
                    sb.Append(PathOf(x)).Append('|').Append(F(x.position, "F4")).Append(F(x.rotation.eulerAngles, "F3")).Append(F(x.lossyScale, "F4")).Append(x.gameObject.activeSelf ? '1' : '0');
                    foreach (var l in x.GetComponents<Light>()) sb.Append(l.enabled).Append(l.intensity.ToString("F3", CultureInfo.InvariantCulture)).Append(l.range.ToString("F3", CultureInfo.InvariantCulture)).Append(l.color);
                    foreach (var r in x.GetComponents<Renderer>()) sb.Append(r.enabled).Append(r.shadowCastingMode);
                    sb.Append('\n');
                }
            }
            var cine = c.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "WakeUpCinematic"); if (cine) Add(cine.transform);
            foreach (var n in ProtectedUnderSector) { var t = c.sector.Find(n); if (t) Add(t); else sb.Append("missing ").Append(n).Append('\n'); }
            foreach (var g in c.scene.GetRootGameObjects())
                foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb && (mb.GetType().Name == "MenuFlicker" || mb.GetType().Name == "MenuSpin")) Add(mb.transform);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").Substring(0, 16);
        }

        static long Tris(Mesh m) { if (!m) return 0; long t = 0; for (int s = 0; s < m.subMeshCount; s++) if (m.GetTopology(s) == MeshTopology.Triangles) t += m.GetIndexCount(s) / 3; return t; }

        static void Stats(Ctx c)
        {
            var renderers = c.root.GetComponentsInChildren<Renderer>(true);
            int meshR = renderers.Count(r => r is MeshRenderer), psR = renderers.Count(r => r is ParticleSystemRenderer);
            long tris = c.root.GetComponentsInChildren<MeshFilter>(true).Sum(f => Tris(f.sharedMesh));
            int statics = c.root.GetComponentsInChildren<Transform>(true).Count(t => t.GetComponent<MeshRenderer>() && GameObjectUtility.AreStaticEditorFlagsSet(t.gameObject, StaticEditorFlags.BatchingStatic));
            var lights = c.root.GetComponentsInChildren<Light>(true); int shadowLights = lights.Count(l => l.shadows != LightShadows.None);
            int sceneLights = c.scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<Light>(true).Length);
            int sceneShadow = c.scene.GetRootGameObjects().Sum(g => g.GetComponentsInChildren<Light>(true).Count(l => l.shadows != LightShadows.None));
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
            int shadowCasters = renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off);
            int scrapInstances = c.root.GetComponentsInChildren<Transform>(true).Count(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            c.log.AppendLine("---- rendimiento HG_ScrapRoute ----");
            c.log.AppendLine($"renderers {renderers.Length} (MeshRenderer {meshR}, partículas {psR}) · BatchingStatic {statics} · proyectan sombra {shadowCasters}");
            c.log.AppendLine($"triángulos (índices de las mallas, incluye todos los niveles de mesh LOD si existen) {tris:N0}");
            c.log.AppendLine($"instancias de chatarra (prefabs Scrap) {scrapInstances} · materiales compartidos {materials.Count}: {string.Join(", ", materials.Select(m => m.name))}");
            c.log.AppendLine($"luces nuevas {lights.Length} (con sombra {shadowLights}) · luces en la escena {sceneLights} (con sombra {sceneShadow}) · colliders nuevos {c.newColliders.Count}");
            foreach (Transform item in c.root)
            {
                if (item == c.disabled) continue;
                var rs = item.GetComponentsInChildren<Renderer>(true);
                long t = item.GetComponentsInChildren<MeshFilter>(true).Sum(f => Tris(f.sharedMesh));
                c.log.AppendLine($"  {item.name,-18} renderers {rs.Length,4} · tris {t,9:N0}");
            }
        }

        static string WriteReport(string name, string text)
        {
            Directory.CreateDirectory(ReportFolder);
            var p = ReportFolder + "/" + name; File.WriteAllText(p, text); return p;
        }

        static string F(Vector3 v, string fmt = "F2") => "(" + v.x.ToString(fmt, CultureInfo.InvariantCulture) + "; " + v.y.ToString(fmt, CultureInfo.InvariantCulture) + "; " + v.z.ToString(fmt, CultureInfo.InvariantCulture) + ")";

        // =====================================================================================================================
        [MenuItem(Menu + "Capture route views", priority = 11)]
        public static void CaptureMenu()
        {
            try { Debug.Log("[Map01 Hangar] Capture route views -> " + Capture()); }
            catch (Exception e) { Debug.LogError("[Map01 Hangar] Capture route views falló: " + e); }
        }

        /// Four fixed views (standing at the wake-up point facing the door, half way, the cinematic E_Reveal frame and a top view)
        /// into Tools/IronRequiem/Reports/Map01/Hangar/route-<view>-<before|after>.png. Temporary objects only; nothing kept.
        public static string Capture()
        {
            var log = new StringBuilder("Map01 Hangar · capturas · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            var data = LoadData();
            var scene = OpenHangar(log);
            var cine = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "WakeUpCinematic");
            if (!cine) throw new InvalidOperationException("Room00_Hangar: falta WakeUpCinematic");
            var stand = Required(cine.transform.Find("StandPoint"), "StandPoint");
            var reveal = cine.transform.Find("Shots/Shot_E_Reveal");
            bool after = scene.GetRootGameObjects().Any(g => g.name == RootName);
            string tag = after ? "after" : "before";
            Directory.CreateDirectory(ReportFolder);
            var door = V3(data.rules.doorProbe) + Vector3.up * 1.6f;
            var face = Vector3.ProjectOnPlane(stand.forward, Vector3.up).normalized;
            var eye = stand.position + Vector3.up * 1.65f + face * .2f;
            var mid = new Vector3((data.rules.laneStart[0] + data.rules.laneGoal[0]) * .5f + 1.0f, 1.65f, (data.rules.laneStart[1] + data.rules.laneGoal[1]) * .5f);
            var temps = new List<Object>(); var hidden = new List<Renderer>();
            try
            {
                var camGo = new GameObject("HGR_CaptureCamera") { hideFlags = HideFlags.HideAndDontSave }; temps.Add(camGo);
                var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.nearClipPlane = .05f; cam.farClipPlane = 260f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.1f, .13f, .17f, 1);
                var urp = cam.GetUniversalAdditionalCameraData(); urp.renderPostProcessing = true; urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                void Shot(string name, Vector3 pos, Vector3 look, float fov)
                {
                    cam.orthographic = false; cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                    var path = ReportFolder + "/route-" + name + "-" + tag + ".png"; FrameGrab.Save(cam, path, 1920, 1080); log.AppendLine(path);
                }
                Shot("01-stand", eye, door, 70f);
                Shot("02-mid", mid, door, 70f);
                if (reveal)
                {
                    var vc = reveal.GetComponent<Unity.Cinemachine.CinemachineCamera>(); float fov = vc ? vc.Lens.FieldOfView : 36f;
                    cam.orthographic = false; cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(reveal.position, reveal.rotation);
                    var path = ReportFolder + "/route-03-reveal-" + tag + ".png"; FrameGrab.Save(cam, path, 1920, 1080); log.AppendLine(path + " (encuadre del Shot_E_Reveal, fov " + fov + ")");
                }
                else log.AppendLine("Shot_E_Reveal no encontrado: sin captura 03");
                // top view: roofs and anything above 11 m hidden for the render only (forceRenderingOff is not serialized)
                foreach (var g in scene.GetRootGameObjects())
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                        if (r.enabled && !r.forceRenderingOff && r.bounds.min.y > 11f) { r.forceRenderingOff = true; hidden.Add(r); }
                var lightGo = new GameObject("HGR_CaptureLight") { hideFlags = HideFlags.HideAndDontSave }; temps.Add(lightGo);
                var key = lightGo.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = .9f; key.shadows = LightShadows.None; lightGo.transform.rotation = Quaternion.Euler(65f, 25f, 0f);
                var zr = data.rules.routeZone; float cx = (zr[0] + zr[2]) * .5f, cz = (zr[1] + zr[3]) * .5f, sx = zr[2] - zr[0] + 2f, sz = zr[3] - zr[1] + 4f;
                cam.orthographic = true; cam.orthographicSize = sz * .5f; urp.renderPostProcessing = false;
                cam.transform.SetPositionAndRotation(new Vector3(cx, 40f, cz), Quaternion.Euler(90f, 0f, 0f));
                int w = 2048, h = Mathf.Max(64, Mathf.RoundToInt(2048 * sz / sx));
                var top = ReportFolder + "/route-04-top-" + tag + ".png"; FrameGrab.Save(cam, top, w, h); log.AppendLine(top);
            }
            finally
            {
                foreach (var r in hidden) if (r) r.forceRenderingOff = false;
                foreach (var o in temps) if (o) Object.DestroyImmediate(o);
            }
            if (scene.isDirty) { EditorSceneManager.SaveScene(scene); log.AppendLine("la escena quedó marcada por los objetos temporales; se volvió a guardar sin cambios de contenido"); }
            return WriteReport("route_captures.txt", log.ToString());
        }
    }
}
