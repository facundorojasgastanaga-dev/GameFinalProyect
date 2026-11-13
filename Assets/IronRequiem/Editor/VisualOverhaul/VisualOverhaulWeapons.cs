using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using K = IronRequiem.Editor.VisualOverhaulAssets;

namespace IronRequiem.Editor
{
    // Visual overhaul (07/11/2026): forearm-module presentation of the two player weapons, as prefab VARIANTS.
    // Original design (not a copy of any existing character): bone-white ceramic plates, graphite joints, orange
    // accent band and cyan status slits, the same palette as the protagonist's suit (VO_Suit_* materials).
    //  * Continuous cannon (variant of Weapon_IR_ArmModule_v07_ExtendedHeat): rear docking collar, plated housing with
    //    the four heat capsules seated on top, long single barrel with four emissive rings (glow while firing).
    //  * Shotgun (variant of Weapon_Shotgun_Secondary_Thermal: 4 charges + bank reload + heat): plated housing with the
    //    four energy cells in a cradle on top (they still sink, hiss and vent on reload), clustered 2x2 barrels.
    // Weapon stats, muzzle, projectile, reload, heat and the rear "Attachment" are unchanged. The new geometry lives
    // under GunRoot/VO_Module, which ArmModuleVisual moves for the dock/undock switch; muzzle and Attachment stay put.
    public static class VisualOverhaulWeapons
    {
        public const string CannonSource = "Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_ArmModule_v07_ExtendedHeat.prefab";
        public const string ShotgunSource = "Assets/IronRequiem/Prefabs/Weapons/Weapon_Shotgun_Secondary_Thermal.prefab";
        public static string Cannon => VisualOverhaulEnemies.Folder + "/Weapon_IR_ArmCannon_VO.prefab";
        public static string Shotgun => VisualOverhaulEnemies.Folder + "/Weapon_Shotgun_ArmModule_VO.prefab";

        static GameObject Variant(string source, string dest)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            if (File.Exists(dest)) return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try { var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source), preview); go.name = Path.GetFileNameWithoutExtension(dest); return PrefabUtility.SaveAsPrefabAsset(go, dest); }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, params Material[] mats)
        {
            var old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.BlendProbes; return r;
        }

        static Matrix4x4 At(float x, float y, float z) => Matrix4x4.Translate(new Vector3(x, y, z));
        static Matrix4x4 AtE(float x, float y, float z, Vector3 euler) => Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(euler), Vector3.one);

        // Ring (annulus with thickness) around local Z at z.
        static void Ring(K.MeshKit k, Matrix4x4 m, float rIn, float rOut, float width, int sub, int seg = 20)
            => k.Lathe(m, new[] { new Vector2(rIn, 0), new Vector2(rOut, 0), new Vector2(rOut, width), new Vector2(rIn, width), new Vector2(rIn, 0) }, seg, sub);

        static Transform PrepareModule(GameObject root, WeaponController w, string[] hide, out Transform module, out AudioSource audio)
        {
            var gun = w.WeaponRoot.transform;
            var old = gun.Find("VO_Module"); if (old) { foreach (Transform c in old.Cast<Transform>().ToArray()) if (c.name.StartsWith("WeaponMesh")) c.SetParent(gun, true); Object.DestroyImmediate(old.gameObject); }
            module = new GameObject("VO_Module").transform; module.SetParent(gun, false);
            module.SetPositionAndRotation(root.transform.position, root.transform.rotation); module.localScale = new Vector3(1 / gun.lossyScale.x, 1 / gun.lossyScale.y, 1 / gun.lossyScale.z);
            // The capsule / cell assembly moves with the module (reload and heat scripts animate its children locally).
            var cells = gun.Find("WeaponMesh_Pistol"); if (cells) cells.SetParent(module, true);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) if (hide.Contains(r.name)) r.enabled = false;
            audio = root.GetComponent<AudioSource>();
            var src = root.transform.Find("VO_Audio")?.GetComponent<AudioSource>();
            if (!src) { var a = new GameObject("VO_Audio"); a.transform.SetParent(root.transform, false); src = a.AddComponent<AudioSource>(); }
            src.playOnAwake = false; src.spatialBlend = 0; src.loop = false; src.volume = .8f; if (audio) src.outputAudioMixerGroup = audio.outputAudioMixerGroup; audio = src;
            return gun;
        }

        static ParticleSystem Steam(Transform gun, Vector3 portWeaponSpace, GameObject root)
        {
            var old = gun.Find("VO_PortSteam"); if (old) Object.DestroyImmediate(old.gameObject);
            var s = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Prefabs/VFX/VFX_AngrySteam.prefab"), gun);
            PrefabUtility.UnpackPrefabInstance(s, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            s.name = "VO_PortSteam"; s.transform.position = root.transform.TransformPoint(portWeaponSpace); s.transform.rotation = root.transform.rotation * Quaternion.LookRotation(Vector3.up + Vector3.back * .3f);
            s.transform.localScale = Vector3.one * (.3f / gun.lossyScale.x);
            foreach (var ps in s.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = Math.Min(main.maxParticles, 24); var col = ps.collision; col.enabled = false; }
            return s.GetComponent<ParticleSystem>() ?? s.GetComponentInChildren<ParticleSystem>();
        }

        static Transform JointPlate(Transform module, string name, Vector3 hinge, float side, Material plate)
        {
            var pivot = new GameObject(name).transform; pivot.SetParent(module, false); pivot.localPosition = hinge;
            var k = new K.MeshKit(1); k.Box(At(side * .006f, -.026f, .026f), new Vector3(.01f, .052f, .052f), 0);
            Part(pivot, "Plate", k.Save(side < 0 ? "VO_JointPlate_L" : "VO_JointPlate_R"), plate); return pivot;
        }

        static void Configure(GameObject root, Transform module, Transform[] joints, Renderer[] lights, Renderer[] rings, AudioSource audio, ParticleSystem steam)
        {
            var v = root.GetComponent<ArmModuleVisual>() ?? root.AddComponent<ArmModuleVisual>();
            v.Configure(module, joints, lights, rings, audio,
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/UI/Notification_Snap.wav"),        // "clac" lock
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Player/Land.wav"),                  // hard dock thud
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FPS/Audio/SFX/Weapons/Weapon_Cooling.wav"), steam); // decompression hiss
        }

        public static string BuildCannon()
        {
            Variant(CannonSource, Cannon); var root = PrefabUtility.LoadPrefabContents(Cannon); var log = new StringBuilder();
            try
            {
                var w = root.GetComponent<WeaponController>();
                var gun = PrepareModule(root, w, new[] { "Primary_Weapon", "Primary_Weapon_Front_Piece_01", "Primary_Weapon_Front_Piece_02" }, out var module, out var audio);
                var att = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Attachment"); var port = root.transform.InverseTransformPoint(att.position);
                var muzzle = root.transform.InverseTransformPoint(w.WeaponMuzzle.position);
                float y = port.y, z0 = port.z, z1 = muzzle.z; float L = z1 - z0;
                Material plate = K.Load("VO_Suit_Plate"), joint = K.Load("VO_Suit_Joint"), accent = K.Load("VO_Suit_Accent"), ring = K.Load("VO_Suit_Ring"), light = K.Load("VO_Suit_Light");
                var k = new K.MeshKit(3);
                k.Lathe(At(0, y, z0), new[] { new Vector2(.044f, 0), new Vector2(.052f, .006f), new Vector2(.052f, .04f), new Vector2(.046f, .046f) }, 20, 1, 4, true);        // docking collar
                float hz0 = z0 + .035f, hz1 = z0 + L * .45f, hy = y - .012f;
                k.Box(At(0, hy, (hz0 + hz1) * .5f), new Vector3(.1f, .064f, hz1 - hz0), 0, 2.5f);                                                                        // plated housing
                foreach (int s in new[] { -1, 1 }) k.Box(AtE(s * .061f, hy + .004f, (hz0 + hz1) * .5f - .005f, new Vector3(0, 0, s * 8)), new Vector3(.012f, .062f, hz1 - hz0 - .03f), 0, 2.5f); // side armour
                foreach (int s in new[] { -1, 1 }) k.Box(At(s * .047f, hy + .041f, (hz0 + hz1) * .5f + .02f), new Vector3(.012f, .012f, hz1 - hz0 - .02f), 1, 2.5f);           // capsule cradle rails
                k.Box(At(0, hy - .041f, (hz0 + hz1) * .5f), new Vector3(.03f, .014f, hz1 - hz0 + .02f), 1, 2.5f);                                                             // spine
                k.Box(At(0, hy, hz1 - .012f), new Vector3(.116f, .074f, .014f), 2, 2.5f);                                                                                     // orange band
                k.Lathe(At(0, y, hz1), new[] { new Vector2(.042f, 0), new Vector2(.042f, .012f), new Vector2(.03f, .022f) }, 20, 1, 4);                                   // front collar
                k.Lathe(At(0, y, hz1 + .02f), new[] { new Vector2(.024f, 0), new Vector2(.024f, z1 - hz1 - .035f) }, 18, 1, 4);                                            // long barrel
                k.Lathe(At(0, y, z1 - .018f), new[] { new Vector2(.024f, 0), new Vector2(.032f, .004f), new Vector2(.032f, .016f), new Vector2(.014f, .019f), new Vector2(.014f, .006f) }, 18, 2, 4); // muzzle brake
                Part(module, "VO_Body", k.Save("VO_ArmCannon_Body"), plate, joint, accent);
                var rk = new K.MeshKit(1); float rz0 = hz1 + .04f, rz1 = z1 - .03f;
                for (int i = 0; i < 4; i++) Ring(rk, At(0, y, Mathf.Lerp(rz0, rz1, i / 3f)), .0245f, .031f, .007f, 0);
                var rings = Part(module, "VO_FiringRings", rk.Save("VO_ArmCannon_Rings"), ring);
                var lights = new Renderer[3];
                for (int i = 0; i < 3; i++) { var lk = new K.MeshKit(1); lk.Box(At(0, 0, 0), new Vector3(.003f, .009f, .02f), 0); var lr = Part(module, "VO_Light_" + i, lk.Save("VO_StatusSlit"), light); lr.transform.localPosition = new Vector3(-.0625f, hy + .006f, hz0 + .025f + i * .028f); lights[i] = lr; }
                var joints = new[] { JointPlate(module, "VO_Joint_L", new Vector3(-.05f, y + .03f, z0 + .004f), -1, plate), JointPlate(module, "VO_Joint_R", new Vector3(.05f, y + .03f, z0 + .004f), 1, plate) };
                var steam = Steam(gun, port + new Vector3(0, .03f, .02f), root);
                Configure(root, module, joints, lights, new Renderer[] { rings }, audio, steam);
                PrefabUtility.SaveAsPrefabAsset(root, Cannon);
                log.Append($"cannon: port z {z0:F3}, muzzle z {z1:F3} (barrel {L * 100:F0} cm), 4 rings, 3 status slits, 2 joint plates");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return log.ToString();
        }

        public static string BuildShotgun()
        {
            Variant(ShotgunSource, Shotgun); var root = PrefabUtility.LoadPrefabContents(Shotgun); var log = new StringBuilder();
            try
            {
                var w = root.GetComponent<WeaponController>();
                var gun = PrepareModule(root, w, new[] { "Primary_Weapon", "Cylinder", "Cylinder (1)", "Cylinder (2)", "Primary_Weapon_Front_Piece_01", "Primary_Weapon_Front_Piece_02" }, out var module, out var audio);
                var att = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Attachment"); var port = root.transform.InverseTransformPoint(att.position);
                var cells = root.GetComponent<WeaponFuelCellHandler>().FuelCells.Where(c => c).ToArray();
                var cb = new Bounds(root.transform.InverseTransformPoint(cells[0].transform.position), Vector3.zero);
                foreach (var c in cells) foreach (var r in c.GetComponentsInChildren<Renderer>(true)) { var b = r.bounds; cb.Encapsulate(root.transform.InverseTransformPoint(b.min)); cb.Encapsulate(root.transform.InverseTransformPoint(b.max)); }
                Material plate = K.Load("VO_Suit_Plate"), joint = K.Load("VO_Suit_Joint"), accent = K.Load("VO_Suit_Accent"), light = K.Load("VO_Suit_Light");
                float y = port.y, z0 = port.z; float top = cb.min.y + cb.size.y * .35f;   // cells protrude ~65 % above the housing
                float hz0 = z0 + .035f, hz1 = Mathf.Max(cb.max.z + .05f, z0 + .26f), hy0 = y - .04f, hcy = (hy0 + top) * .5f, hh = top - hy0;
                float barrelZ1 = hz1 + .26f, by = hcy;
                var k = new K.MeshKit(3);
                k.Lathe(At(0, y, z0), new[] { new Vector2(.044f, 0), new Vector2(.052f, .006f), new Vector2(.052f, .04f), new Vector2(.046f, .046f) }, 20, 1, 4, true); // docking collar
                k.Box(At(0, hcy, (hz0 + hz1) * .5f), new Vector3(.132f, hh, hz1 - hz0), 0, 2.5f);                                                                       // housing
                foreach (int s in new[] { -1, 1 }) k.Box(AtE(s * .07f, hcy + .005f, (hz0 + hz1) * .5f, new Vector3(0, 0, s * 6)), new Vector3(.012f, hh * .9f, hz1 - hz0 - .03f), 0, 2.5f);
                foreach (int s in new[] { -1, 1 }) k.Box(At(s * Mathf.Max(.052f, cb.extents.x + .012f), top + .008f, cb.center.z), new Vector3(.012f, .016f, cb.size.z + .03f), 1, 2.5f); // cell cradle
                k.Box(At(0, top + .006f, cb.min.z - .012f), new Vector3(.11f, .02f, .012f), 1, 2.5f); k.Box(At(0, top + .006f, cb.max.z + .012f), new Vector3(.11f, .02f, .012f), 1, 2.5f);
                k.Box(At(0, top + .002f, hz1 - .02f), new Vector3(.134f, .012f, .03f), 2, 2.5f);                                                                         // orange band
                // 2x2 barrel cluster, slight outward fan (1.2 deg).
                foreach (var o in new[] { new Vector2(-.026f, .026f), new Vector2(.026f, .026f), new Vector2(-.026f, -.026f), new Vector2(.026f, -.026f) })
                {
                    var rot = Quaternion.Euler(-o.y / .026f * 1.2f, o.x / .026f * 1.2f, 0);
                    var m = Matrix4x4.TRS(new Vector3(o.x, by + o.y, hz1), rot, Vector3.one);
                    k.Lathe(m, new[] { new Vector2(.018f, 0), new Vector2(.018f, barrelZ1 - hz1 - .012f), new Vector2(.021f, barrelZ1 - hz1 - .01f), new Vector2(.021f, barrelZ1 - hz1), new Vector2(.011f, barrelZ1 - hz1), new Vector2(.011f, barrelZ1 - hz1 - .008f) }, 14, 1, 4);
                }
                foreach (float z in new[] { hz1 + .05f, barrelZ1 - .07f }) Ring(k, At(0, by, z), .055f, .066f, .022f, 1, 24);                                          // clamp rings
                Part(module, "VO_Body", k.Save("VO_ArmShotgun_Body"), plate, joint, accent);
                var lights = new Renderer[4];
                for (int i = 0; i < 4; i++) { var lk = new K.MeshKit(1); lk.Box(At(0, 0, 0), new Vector3(.003f, .012f, .018f), 0); var lr = Part(module, "VO_Light_" + i, lk.Save("VO_StatusSlitTall"), light); lr.transform.localPosition = new Vector3(-.0715f, hcy + .01f, hz0 + .03f + i * .03f); lights[i] = lr; }
                var joints = new[] { JointPlate(module, "VO_Joint_L", new Vector3(-.055f, y + .03f, z0 + .004f), -1, plate), JointPlate(module, "VO_Joint_R", new Vector3(.055f, y + .03f, z0 + .004f), 1, plate) };
                var steam = Steam(gun, port + new Vector3(0, .03f, .02f), root);
                Configure(root, module, joints, lights, new Renderer[0], audio, steam);
                PrefabUtility.SaveAsPrefabAsset(root, Shotgun);
                log.Append($"shotgun: port z {z0:F3}, housing to {hz1:F3}, barrels to {barrelZ1:F3}, 4 tubes, cells {cells.Length} visible on top (bounds y {cb.min.y:F3}-{cb.max.y:F3}), 4 slits");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return log.ToString();
        }

        public static string BuildAll() { var s = BuildCannon() + "\n" + BuildShotgun(); AssetDatabase.SaveAssets(); File.WriteAllText(VisualOverhaulDriver.Reports + "/weapons.txt", s); return s; }
    }
}
